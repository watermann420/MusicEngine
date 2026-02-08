param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Debug'
)

$project = Join-Path $PSScriptRoot 'MusicEngine.CppLayer\native\MusicEngine.CppLayer.Native.vcxproj'

if (!(Test-Path $project)) {
    Write-Error "Native project not found: $project"
    exit 1
}

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (!(Test-Path $vswhere)) {
    Write-Error "vswhere not found. Install Visual Studio Build Tools (Desktop C++)."
    exit 1
}

$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
if (-not $msbuild) {
    Write-Error "MSBuild not found. Install Visual Studio Build Tools (Desktop C++)."
    exit 1
}

Write-Host "Using MSBuild: $msbuild"
& $msbuild $project /p:Configuration=$Configuration /p:Platform=x64
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
