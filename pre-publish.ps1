[CmdletBinding()]
param (
    [switch]$Core,
    [switch]$Terminal)

$rc = 0

if ($Core) {
    "*** BUILDING CORE ***"
    dotnet build src/Core/Core.csproj -c Release --nologo
    if (!$LASTEXITCODE) {
        dotnet test src/Core/Core-Tests.csproj --nologo
    }
    if ($LASTEXITCODE) { $rc = 1 }
    ""
}

if ($Terminal) {
    dotnet build src/Terminal/Terminal.csproj -c Release --nologo
    if (!$LASTEXITCODE) {
        dotnet test src/Terminal/Terminal-Tests.csproj --nologo
    }
    if ($LASTEXITCODE) { $rc = 1 }
    ""
}

exit $rc
