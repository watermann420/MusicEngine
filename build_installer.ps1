# MusicEngine Installer packager
# Creates a desktop folder with installer exe + payload.

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$desktop = [Environment]::GetFolderPath("DesktopDirectory")
if ([string]::IsNullOrWhiteSpace($desktop)) {
    throw "Desktop folder not found."
}

$outDir = Join-Path $desktop "MusicEngine_Installer"
$payloadDir = Join-Path $outDir "payload"

if (Test-Path $outDir) {
    Remove-Item $outDir -Recurse -Force
}
New-Item -ItemType Directory -Path $payloadDir | Out-Null

Write-Host "Building MusicEngine (Release)..."
dotnet build "$repoRoot\MusicEngine.csproj" -c Release

Write-Host "Publishing MusicEngine (Release)..."
dotnet publish "$repoRoot\MusicEngine.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=false

$publishDir = Join-Path $repoRoot "bin\Release\net10.0-windows\win-x64\publish"
if (!(Test-Path $publishDir)) {
    throw "Publish output not found: $publishDir"
}

Write-Host "Copying payload..."
Copy-Item "$publishDir\*" $payloadDir -Recurse -Force

Write-Host "Building Installer (Release)..."
dotnet build "$repoRoot\MusicEngine.Installer\MusicEngine.Installer.csproj" -c Release

$installerExe = Join-Path $repoRoot "MusicEngine.Installer\bin\Release\net10.0-windows\MusicEngine.Installer.exe"
if (!(Test-Path $installerExe)) {
    throw "Installer EXE not found: $installerExe"
}

Copy-Item $installerExe $outDir -Force

Write-Host "Done. Output folder:"
Write-Host $outDir
