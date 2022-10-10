$isDotSourced = $MyInvocation.InvocationName -eq '.' -or $MyInvocation.Line -eq ''
# TODO eliminate need to dot-source
if (!$isDotSourced) {
    # https://stackoverflow.com/a/33855217/14582
    throw 'This script needs to be dot-sourced'
}

'Building..'
dotnet build --nologo -v q $PSScriptRoot
dotnet build -c Release --nologo -v q $PSScriptRoot

set-alias okflogd $PSScriptRoot\build\Flog.Cli\Debug\okflog.exe
set-alias okud $PSScriptRoot\build\Unity.Cli\Debug\okunity.exe
set-alias loggod $PSScriptRoot\build\Loggo.Cli\Debug\loggo.exe
set-alias pmltoold $PSScriptRoot\build\PmlTool.Cli\Debug\pmltool.exe
set-alias ucaptured $PSScriptRoot\build\PmlTool.Cli\Debug\Scripts\UnityCapture.ps1

set-alias okflog $PSScriptRoot\build\Flog.Cli\okflog.exe
set-alias oku $PSScriptRoot\build\Unity.Cli\okunity.exe
set-alias loggo $PSScriptRoot\build\Loggo.Cli\loggo.exe
set-alias pmltool $PSScriptRoot\build\PmlTool.Cli\pmltool.exe
set-alias ucapture $PSScriptRoot\build\PmlTool.Cli\Scripts\UnityCapture.ps1
