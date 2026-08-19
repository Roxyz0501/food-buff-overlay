param(
    [string]$Configuration = "Release",
    [string]$ActPath = "C:\Program Files (x86)\Advanced Combat Tracker\Advanced Combat Tracker.exe"
)
$ErrorActionPreference = "Stop"
$solutionPath = Join-Path $PSScriptRoot "FoodBuffOverlay.sln"
if (-not (Test-Path -LiteralPath $ActPath)) { throw "Advanced Combat Tracker.exe was not found. Pass its path with -ActPath." }
dotnet build $solutionPath -c $Configuration -p:ActPath="$ActPath" --configfile (Join-Path $PSScriptRoot "NuGet.Config")
if ($LASTEXITCODE -ne 0) { throw "Build failed." }
$pluginDll = Join-Path $PSScriptRoot "src\FoodBuffOverlay\bin\$Configuration\net48\FoodBuffOverlay.dll"
$updaterExe = Join-Path $PSScriptRoot "src\FoodBuffOverlay.Updater\bin\$Configuration\net48\FoodBuffOverlay.Updater.exe"
$pluginOutput = Split-Path -Parent $pluginDll
Copy-Item -LiteralPath $updaterExe -Destination (Join-Path $pluginOutput "FoodBuffOverlay.Updater.exe") -Force

$version = [Reflection.AssemblyName]::GetAssemblyName($pluginDll).Version.ToString(3)
$dist = Join-Path $PSScriptRoot "dist"
$stage = Join-Path $dist "package"
New-Item -ItemType Directory -Force -Path $stage | Out-Null
Copy-Item -LiteralPath $pluginDll -Destination (Join-Path $stage "FoodBuffOverlay.dll") -Force
Copy-Item -LiteralPath $updaterExe -Destination (Join-Path $stage "FoodBuffOverlay.Updater.exe") -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "README.md") -Destination (Join-Path $stage "README.md") -Force
$zipName = "FoodBuffOverlay-v$version.zip"
$zipPath = Join-Path $dist $zipName
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zipPath -Force
$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath).Hash.ToLowerInvariant()
$manifestPath = Join-Path $dist "FoodBuffOverlay-v$version.sha256"
Set-Content -LiteralPath $manifestPath -Value "$hash  $zipName" -Encoding ascii
Write-Host "Built: $pluginDll"
Write-Host "Release asset: $zipPath"
Write-Host "SHA-256: $manifestPath"
