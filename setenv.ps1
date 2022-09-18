# dot source me!
# TODO: detect and warn if not dot-sourced
# TODO (alternate): eliminate need to dot-source
'Building..'
dotnet build --nologo -v q
set-alias okflog $PSScriptRoot\build\Flog.Cli\Debug\okflog.exe
set-alias okunity $PSScriptRoot\build\Unity.Cli\Debug\okunity.exe
set-alias loggo $PSScriptRoot\build\Loggo.Cli\Debug\loggo.exe
set-alias pmltool $PSScriptRoot\build\PmlTool.Cli\Debug\pmltool.exe
