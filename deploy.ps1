$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. "$ScriptDir\paths.ps1"

$SlnPath        = "$ScriptDir\lstwoMODS WobblyLife.sln"
$LibDir         = "$ScriptDir\lstwoMODS WobblyLife\lib"
$BuildOut       = "$ScriptDir\lstwoMODS WobblyLife\bin\Release\net472"
$ExtBuildOut    = "$ScriptDir\lstwoMODS.WobblyLife.OverlayExtension\bin\Release\net472"

Write-Host ""
Write-Host "Paths:" -ForegroundColor Yellow
Write-Host "  Solution          : $SlnPath"
Write-Host "  Lib dir           : $LibDir"
Write-Host "  Build output      : $BuildOut"
Write-Host "  Extension output  : $ExtBuildOut"
Write-Host "  Plugins dir       : $GamePluginsPath"
Write-Host "  Overlay dir       : $OverlayDir"
Write-Host "  Overlay plugins   : $OverlayPluginsDir"
Write-Host ""

Write-Host "Building lstwoMODS WobblyLife solution (Release, Any CPU)..." -ForegroundColor Cyan
dotnet build "$SlnPath" -c Release -p:Platform="Any CPU"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Solution build failed." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Publishing lstwoMODS_Overlay (Release, win-x64)..." -ForegroundColor Cyan
dotnet publish "$OverlayProjectPath" -c Release -r win-x64

if ($LASTEXITCODE -ne 0) {
    Write-Host "Overlay publish failed." -ForegroundColor Red
    exit 1
}

foreach ($dir in @($GamePluginsPath, $OverlayDir, $OverlayPluginsDir)) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Host "Created: $dir"
    }
}

Write-Host ""
Write-Host "Deploying plugin DLLs to $GamePluginsPath..." -ForegroundColor Cyan

$libFiles = @("lstwoMODS_Core.dll", "lstwoMODS.ImGui.Shared.dll", "DynamicExpresso.Core.dll", "CustomItems.dll", "Concentus.dll")
foreach ($file in $libFiles) {
    $src = "$LibDir\$file"
    if (-not (Test-Path $src)) { Write-Host "ERROR: $src not found." -ForegroundColor Red; exit 1 }
    Copy-Item $src "$GamePluginsPath\$file" -Force
    Write-Host "  $src -> $GamePluginsPath\$file"
}


$buildFiles = @("lstwoMODS_WobblyLife.dll", "lstwoMODS.WobblyLife.SharedObjects.dll")
foreach ($file in $buildFiles) {
    $src = "$BuildOut\$file"
    if (-not (Test-Path $src)) { Write-Host "ERROR: $src not found." -ForegroundColor Red; exit 1 }
    Copy-Item $src "$GamePluginsPath\$file" -Force
    Write-Host "  $src -> $GamePluginsPath\$file"
}

Write-Host ""
Write-Host "Deploying license notices to $GamePluginsPath\licenses..." -ForegroundColor Cyan

$LicensesDir = "$ScriptDir\licenses"
if (-not (Test-Path $LicensesDir)) { Write-Host "ERROR: $LicensesDir not found." -ForegroundColor Red; exit 1 }
$LicensesDst = "$GamePluginsPath\licenses"
if (-not (Test-Path $LicensesDst)) { New-Item -ItemType Directory -Path $LicensesDst -Force | Out-Null }
Copy-Item "$LicensesDir\*" -Destination $LicensesDst -Recurse -Force
foreach ($pkg in Get-ChildItem $LicensesDir -Directory) {
    Write-Host "  $($pkg.Name) -> $LicensesDst\$($pkg.Name)"
}

Write-Host ""
Write-Host "Deploying overlay to $OverlayDir..." -ForegroundColor Cyan

if (-not (Test-Path $OverlayPublishOutputPath)) {
    Write-Host "ERROR: Overlay publish output not found at $OverlayPublishOutputPath" -ForegroundColor Red
    exit 1
}
Copy-Item "$OverlayPublishOutputPath\*" -Destination $OverlayDir -Recurse -Force -Exclude "imgui.ini"
Write-Host "  $OverlayPublishOutputPath\* -> $OverlayDir"

Write-Host ""
Write-Host "Deploying overlay extension to $OverlayPluginsDir..." -ForegroundColor Cyan

$extensionDlls = @(
    "lstwoMODS.WobblyLife.OverlayExtension.dll",
    "lstwoMODS.WobblyLife.SharedObjects.dll"
)
foreach ($dll in $extensionDlls) {
    $src = "$ExtBuildOut\$dll"
    if (-not (Test-Path $src)) { Write-Host "ERROR: $src not found." -ForegroundColor Red; exit 1 }
    Copy-Item $src "$OverlayPluginsDir\$dll" -Force
    Write-Host "  $src -> $OverlayPluginsDir\$dll"
}

Write-Host ""
Write-Host "Done." -ForegroundColor Green
