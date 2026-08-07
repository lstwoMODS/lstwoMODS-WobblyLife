$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. "$ScriptDir\paths.ps1"

if (-not (Test-Path $GameExePath)) {
    Write-Host "ERROR: Game executable not found at $GameExePath" -ForegroundColor Red
    exit 1
}

Write-Host "Launching Wobbly Life..." -ForegroundColor Cyan
Start-Process $GameExePath -WorkingDirectory (Split-Path -Parent $GameExePath)
