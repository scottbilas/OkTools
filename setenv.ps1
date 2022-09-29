# dot source me!
# TODO: detect and warn if not dot-sourced
# TODO (alternate): eliminate need to dot-source
'Building..'
dotnet build --nologo -v q
dotnet build -c Release --nologo -v q
set-alias okflogd $PSScriptRoot\build\Flog.Cli\Debug\okflog.exe
set-alias okunityd $PSScriptRoot\build\Unity.Cli\Debug\okunity.exe
set-alias loggod $PSScriptRoot\build\Loggo.Cli\Debug\loggo.exe
set-alias pmltoold $PSScriptRoot\build\PmlTool.Cli\Debug\pmltool.exe
set-alias okflog $PSScriptRoot\build\Flog.Cli\okflog.exe
set-alias okunity $PSScriptRoot\build\Unity.Cli\okunity.exe
set-alias loggo $PSScriptRoot\build\Loggo.Cli\loggo.exe
set-alias pmltool $PSScriptRoot\build\PmlTool.Cli\pmltool.exe
