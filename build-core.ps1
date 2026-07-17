$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. "$ScriptDir\paths.ps1"

$LibDir = "$ScriptDir\lstwoMODS WobblyLife\lib"

Write-Host ""
Write-Host "Paths:" -ForegroundColor Yellow
Write-Host "  Core project    : $CoreProjectPath"
Write-Host "  Core output     : $CoreBuildOutputPath"
Write-Host "  Overlay project : $OverlayProjectPath"
Write-Host "  Overlay output  : $OverlayDebugOutputPath"
Write-Host "  Lib dir         : $LibDir"
Write-Host ""

Write-Host "Publishing lstwoMODS_Core (Release, Any CPU)..." -ForegroundColor Cyan
dotnet publish "$CoreProjectPath" -c Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "Core publish failed." -ForegroundColor Red
    exit 1
}

Write-Host "Copying Core DLLs to lib/..." -ForegroundColor Cyan

$coreFiles = @("lstwoMODS_Core.dll", "lstwoMODS.ImGui.Shared.dll", "lstwoMODS_Core.xml")
foreach ($file in $coreFiles) {
    $src = "$CoreBuildOutputPath\$file"
    $dst = "$LibDir\$file"
    if (-not (Test-Path $src)) {
        Write-Host "ERROR: $src not found." -ForegroundColor Red
        exit 1
    }
    Copy-Item $src $dst -Force
    Write-Host "  $src"
    Write-Host "    -> $dst"
}

Write-Host ""
Write-Host "Building lstwoMODS_Overlay (Debug)..." -ForegroundColor Cyan
dotnet build "$OverlayProjectPath" -c Debug

if ($LASTEXITCODE -ne 0) {
    Write-Host "Overlay build failed." -ForegroundColor Red
    exit 1
}

Write-Host "  -> $OverlayDebugOutputPath\lstwoMODS_Overlay.exe"

Write-Host ""
Write-Host "Done." -ForegroundColor Green
