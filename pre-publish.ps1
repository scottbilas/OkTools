#requires -Version 7
#requires -Module Native

[CmdletBinding()]
param (
    [switch]$Core,
    [switch]$Terminal,
    [switch]$SkipBuild,
    [switch]$SkipTests,
    [switch]$SkipPack,
    [switch]$All)

Set-StrictMode -Version Latest

if ($All)
{
    $Core = $true
    $Terminal = $true
}

$rc = 0

function dowit($wat) {
    if (!$SkipBuild) {
        "*** BUILDING $($wat.ToUpper()) ***"
        iee dotnet build src/$wat/$wat.csproj -c Release --nologo
    }
    if (!$SkipTests) {
        "*** TESTING $($wat.ToUpper()) ***"
        iee dotnet test src/$wat/$wat-Tests.csproj --nologo
    }
    if (!$SkipPack) {
        "*** PACKING $($wat.ToUpper()) ***"
        iee dotnet pack src/$wat/$wat.csproj -c Release
    }

    "*** GENERATE PUBLIC API ***"

    iee dotnet tool restore

    $old = $env:MSBUILDTERMINALLOGGER
    try {
        $env:MSBUILDTERMINALLOGGER = 'off'
        "Running generate-public-api for $wat..."
        $out = iee dotnet tool run generate-public-api --target-frameworks net9.0 --assembly (resolve-path artifacts\build\bin\$wat\net9.0\OkTools.$wat.dll) | out-string
    }
    finally {
        $env:MSBUILDTERMINALLOGGER = $old
    }

    # MSBUILDTERMINALLOGGER=off ought to mask this, but keep the fix in case running a version of dotnet with the issue
    $esc = [char]27
    $out = $out.replace("$esc]9;4;3;$esc\$esc]9;4;0;$esc\", '')
    $out = $out.replace("`r`n", "`n")

    set-content api-$($wat.ToLower()).txt $out -nonew
    ""
}

if ($Core) { dowit Core }
if ($Terminal) { dowit Terminal }
