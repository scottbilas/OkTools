#Requires -Version 7

# TODO: split this better into "working with a template" and "working with an existing project"

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]$ProjectDir, # root for project
    [string]$Toolchain,                      # toolchain to use (defaults to newest non-debug toolchain found)
    [string]$Template,                       # template to create project from, opens existing project if missing
    [switch]$NukeCache,                      # set to nuke the global unity cache
    [switch]$NoSymbolDownload                # when running `pmltool bake`, tell it not to download pdb's via _NT_SYMBOL_PATH
)

if (!$ProjectDir) {
    throw "Missing -ProjectDir"
}

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
        #if (get-command byenow) { # scoop install byenow
        #    #byenow --staged --delete-ntapi --one-liner --list-errors --show-bytes --yolo --yes $where
        #}
        #else {
            # TODO: this runs into problems with paths like C:\Users\scott\AppData\Local\Unity\cache/packages/artifactory.prd.cds.internal.unity3d.com/artifactory/api/npm/upm-candidates/com.unity.netcode.gameobjects@1.0.0-pre.9/Tests/Runtime/NetworkAnimator/Resources
            # remove-item doesn't work, rm -rf doesn't work..explorer will do it. maybe cmd /c?
            # not sure if it's the length that is the problem or something else wrong. figure it out!
            remove-item -r $where
        #}
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
    throw "TODO: re-implement this..." # TODO: use $Toolchain (latest non-debug build) and okunity to get the build dir
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

if ($Template) {
    if (Test-Path $ProjectDir) {
        Write-Host "*** Deleting old test folder '$ProjectDir'"
        KillDir $ProjectDir
        mkdir $ProjectDir >$null
    }
    else {
        Write-Error "No project at $ProjectDir"
    }
}
else {
    Write-Host "*** Deleting old test artifacts in '$ProjectDir'"
    KillDir $ProjectDir/Temp/procmon
}

if ($NukeCache) {
    Write-Host "*** Nuking Unity global cache at '$Env:LOCALAPPDATA\Unity\cache'"
    KillDir $Env:LOCALAPPDATA\Unity\cache
}

$procmonPath = "$ProjectDir/Temp/procmon"
if (!(test-path $procmonPath)) {
    [void](mkdir $procmonPath)
}
$eventsPmlPath = "$procmonPath/events.pml"

Write-Host "*** Starting up Procmon; log=$eventsPmlPath, config=$PSScriptRoot\config.pmc"
sudo start-process procmon -WindowStyle Minimized "/accepteula /backingfile $eventsPmlPath /loadconfig $PSScriptRoot\config.pmc /minimized /quiet"

Write-Host "*** Waiting a bit for Procmon to stabilize"
Start-Sleep 5

Write-Host "*** Starting up Unity"
if ($Template) {
    # TODO: add support for -cloneFromTemplate to okunity
    $Env:UNITY_MIXED_CALLSTACK = 1
    $Env:UNITY_EXT_LOGGING = 1
    & "$UnityDir\Unity.exe" -logFile $ProjectDir\Temp\editor.log -createProject $ProjectDir -cloneFromTemplate $Template
}
else {
    if ((split-path -leaf (split-path $PSScriptRoot)) -eq 'Debug') {
        $oku = "$PSScriptRoot\..\..\..\Unity.Cli\Debug\okunity.exe"
    }
    else {
        $oku = "$PSScriptRoot\..\..\Unity.Cli\okunity.exe"
    }

    & $oku unity $ProjectDir --copy-pmips $procmonPath

    # jeez, how about making this easier in okunity..
    $UnityVersion = (& $oku info C:\UnitySrc\plastic\CreativeJuice\CreativeJuice -j | convertfrom-json).version
    $UnityDir = (& $oku toolchains -j | convertfrom-json | ?{ $_.version -eq $UnityVersion }).path
}

# TODO: have unity run script that waits until done loading project then waits a few sec more then shuts down
#
# ways to know it's done loading:
#
#   * pass command line arg to unity to run a static script method that can signal when it thinks it's done loading. will require copying a script into the project after unity starts creating it (but before compiling starts..)
#   * use some kind of existing remote control interface to unity (for test runner?) that i don't know about
#   * tail the editor log and wait for x seconds of inactivity or a certain pattern (like "Loaded scene") + x seconds

Write-Host "*** Terminating procmon cleanly"
sudo procmon /terminate
while (get-process procmon*) { start-sleep .1 }

Write-Host "*** Killing processes"
KillProcs

Write-Host "*** Baking"
& $PSScriptRoot\..\pmltool.exe bake $eventsPmlPath
