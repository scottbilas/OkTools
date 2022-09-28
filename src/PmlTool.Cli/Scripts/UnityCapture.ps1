#Requires -Version 7

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]$TestDir,  # root for project and other artifacts
    [Parameter(Mandatory=$true)]$UnityDir, # path to unity build
    [string]$Template,                     # template to create project from, opens existing project if missing
    [switch]$NukeCache,                    # set to nuke the global unity cache
    [switch]$NoSymbolDownload              # when running `pmltool bake`, tell it not to download pdb's via _NT_SYMBOL_PATH
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function KillProc($what, [switch]$waitAfter) {
    if (get-process -ea:silent $what) {
        pskill -nobanner $what
        if ($waitAfter) {
            start-sleep 1.0
        }
    }
}

function KillProcs {
    KillProc procmon
    KillProc procmon64
    KillProc unity $true # give time for other apps to notice Unity died (and exit cleanly) before trying to kill them too
    KillProc unitypackagemanager
    KillProc unity.licensing.client
    KillProc "unity hub"
}

function KillDir($where) {
    if (test-path $where) {
        if (get-command byenow) { # scoop install byenow
            remove-item -r $where
            #byenow --staged --delete-ntapi --one-liner --list-errors --show-bytes --yolo --yes $where
        }
        else {
            remove-item -r $where
        }
    }
}

function FindTemplate($name) {
    if (Test-Path $name) {
        return Resolve-Path $name
    }

    if ([io.path]::IsPathFullyQualified($name)) {
        Write-Error "Template $name does not exist"
    }

    $downloadCache = join-path $env:APPDATA unityhub\templates
    $buildCache = join-path $UnityDir Data\Resources\PackageManager\ProjectTemplates

    foreach ($root in $downloadCache, $buildCache) {
        # try as full tgz name
        $test = join-path $root $name
        if (Test-Path $test) { return $test }

        # try as just the template name
        $test = get-childitem (join-path $root "com.unity.template.$name-*.tgz") | select-object -first 1
        if ($test -and (Test-Path $test)) { return $test }
    }

    $templates = (get-childitem $downloadCache, $buildCache *.tgz) | foreach-object {
        $_.name -match 'com\.unity\.template\.(\w+)' >$null; $matches[1] } | sort-object -unique

    Write-Error "Can't find a template matching $name; available templates are $($templates -join ', ')"
}

if ($Template) {
    $Template = FindTemplate($Template)
}

Write-Host "*** Killing processes"
KillProcs

if ($template -and (Test-Path $TestDir)) {
    Write-Host "*** Deleting old test folder '$TestDir'"
    KillDir $TestDir
    mkdir $TestDir >$null
}
elseif (Test-Path $TestDir/project/ProjectSettings/ProjectVersion.txt) {
    Write-Host "*** Deleting old test artifacts in '$TestDir'"
    remove-item $TestDir/*.*
}
else {
    Write-Error "No project at $TestDir"
}

if ($NukeCache) {
    Write-Host "*** Nuking Unity global cache at '$Env:LOCALAPPDATA\Unity\cache'"
    KillDir $Env:LOCALAPPDATA\Unity\cache
}

$eventsPmlPath = join-path $TestDir events.pml

Write-Host "*** Starting up Procmon; log=$eventsPmlPath, config=$PSScriptRoot\config.pmc"
sudo procmon /accepteula /backingfile $eventsPmlPath /loadconfig $PSScriptRoot\config.pmc /profiling /minimized /quiet
Write-Host "*** Waiting a bit for it to get going"
start-sleep 5

Write-Host "*** Starting up Unity"
$Env:UNITY_MIXED_CALLSTACK = 1
$Env:UNITY_EXT_LOGGING = 1
if ($Template) {
    & "$UnityDir\Unity.exe" -logFile $TestDir\editor.log -createproject $TestDir\project -cloneFromTemplate $Template
}
else {
    & "$UnityDir\Unity.exe" -logFile $TestDir\editor.log -openproject $TestDir\project
}

# TODO: have unity run script that waits until loaded then copies its pmip and shuts down, kills procmon too
#
# ways to know it's done loading:
#
#   * pass command line arg to unity to run a static script method that can signal when it thinks it's done loading. will require copying a script into the project after unity starts creating it (but before compiling starts..)
#   * use some kind of existing remote control interface to unity (for test runner?) that i don't know about
#   * tail the editor log and wait for x seconds of inactivity or a certain pattern (like "Loaded scene") + x seconds

Write-Host -nonew ">>> Press any key when Unity done: "
[console]::ReadKey() >$null
Write-Host

Write-Host "*** Saving pmip log"
if (get-childitem $env:LOCALAPPDATA\Temp\pmip*.txt) {
    copy-item $env:LOCALAPPDATA\Temp\pmip*.txt $TestDir\
}
else {
    write-error "Unity was killed before I could save the pmip log!!"
}

Write-Host "*** Terminating procmon cleanly"
sudo procmon /terminate
while (get-process procmon*) { start-sleep .1 }

Write-Host "*** Killing processes"
KillProcs

Write-Host "*** Baking"
& $PSScriptRoot\..\pmltool.exe bake $eventsPmlPath
