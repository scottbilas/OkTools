[CmdletBinding()]
param (
    [switch]$Core,
    [switch]$Terminal,
    [switch]$All)

if ($All)
{
    $Core = $true
    $Terminal = $true
}

$rc = 0

filter fix-bug {
    # https://github.com/dotnet/msbuild/issues/10998
    $esc = [char]27
    $_.replace("$esc]9;4;3;$esc\$esc]9;4;0;$esc\", '')
}

function dowit($wat) {
    "*** BUILDING $($wat.ToUpper()) ***"
    dotnet build src/$wat/$wat.csproj -c Release --nologo
    if (!$LASTEXITCODE) {
        dotnet test src/$wat/$wat-Tests.csproj --nologo
    }
    if (!$LASTEXITCODE) {
        dotnet pack src/$wat/$wat.csproj -c Release
    }
    if (!$LASTEXITCODE) {
        dotnet tool restore
    }
    if (!$LASTEXITCODE) {
        dotnet tool run generate-public-api --target-frameworks net9.0 --assembly (resolve-path artifacts\build\bin\$wat\net9.0\OkTools.$wat.dll) | fix-bug > api-$($wat.ToLower()).txt
    }
    if ($LASTEXITCODE) { $rc = 1 }
    ""
}

if ($Core) { dowit Core }
if ($Terminal) { dowit Terminal }

exit $rc
