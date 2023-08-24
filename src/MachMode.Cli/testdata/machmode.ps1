#Requires -Version 7
#Requires -module powershell-yaml

param(
    ####[Parameter(Mandatory = $true)]
    [string]$Mode
)

$Mode = ($Mode -and $Mode.Length) ? $Mode : 'gaming' ####$$$$$



Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop' #'Break' #'Stop'

# pre-checks

$asusService = Get-Service 'ASUS Link Near'
if ($asusService -and ($asusService.Status -ne 'Stopped' -or $asusService.StartType -ne 'Disabled')) {
    Write-Warning 'Tell G-Helper to kill ASUS services'
}

$config = Get-Content machmode.yaml | ConvertFrom-Yaml

$modesConfig = [ordered]@{}

$config.modes | %{
    if ($_ -is [string]) {
        $modesConfig[$_] = $_
    }
    elseif ($_ -is [hashtable]) {
        $modesConfig[$_.keys[0]] = $_.values[0]
    }
}

$baseMode = $modesConfig[$Mode]
if (!$baseMode) {
    Write-Error "Invalid mode $Mode (valid modes: $(($modesConfig.Keys | %{ $_ }) -join ', '))"
}

exit 0

foreach ($appConfig in $config.apps.GetEnumerator()) {
    $exeName = $appConfig.Name
    $shouldBeActive = ($modesConfig -contains $Mode) -or ($modesConfig -contains $baseMode)
    $processes = Get-Process -Name $exeName -ErrorAction SilentlyContinue

    if ($shouldBeActive) {
        if ($processes) {
            Write-Host "$exeName is already running"
        } else {
            Write-Host "Starting $exeName"
            Start-Process $appConfig.Value.path
        }
    }
    else {
        if ($processes) {
            foreach ($process in $processes) {
                Write-Host "Killing $exeName"
                $process.Kill()
            }
        }
        else {
            Write-Host "$exeName is not running"
        }
    }
}
