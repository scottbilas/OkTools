#Requires -Version 7

[CmdletBinding()]
param (
    [string]$which
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$loggo = 'Flog.Cli.Loggo\bin\release\net6.0\win-x64\loggo.exe'
$patternFile = 'Flog.Cli.Loggo\TestData\aoc.unity-editor.log'
$where = 'c:\temp\log.out'

switch ($which) {
    'u'  { & $loggo $where --pattern $patternFile --overwrite --size 1mb --delay 0 }
    'ul' { & $loggo $where --pattern $patternFile --overwrite --size 1mb --delay 50,200 --delete-on-exit }
    default {
        write-error "must be one of 'u' (unity) or 'ui' (unity live, for tailing)"
    }
}
