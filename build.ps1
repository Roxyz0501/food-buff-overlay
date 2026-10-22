param(
    [string]$Configuration = "Release",
    [string]$ActPath = "C:\Program Files (x86)\Advanced Combat Tracker\Advanced Combat Tracker.exe"
)
$ErrorActionPreference = "Stop"
if (-not (Test-Path -LiteralPath $ActPath)) { throw "Advanced Combat Tracker.exe was not found. Pass its path with -ActPath." }
dotnet build (Join-Path $PSScriptRoot "tests\FoodBuffOverlay.Tests\FoodBuffOverlay.Tests.csproj") -c $Configuration -p:ActPath="$ActPath" --configfile (Join-Path $PSScriptRoot "NuGet.Config")
if ($LASTEXITCODE -ne 0) { throw "Build failed." }
$pluginDll = Join-Path $PSScriptRoot "src\FoodBuffOverlay\bin\$Configuration\net48\FoodBuffOverlay.dll"
$version = [Reflection.AssemblyName]::GetAssemblyName($pluginDll).Version.ToString(3)
$dist = Join-Path $PSScriptRoot "dist"
$stage = Join-Path $dist "package"
if (Test-Path -LiteralPath $stage) {
    $resolvedDist = [IO.Path]::GetFullPath($dist).TrimEnd('\') + '\'
    $resolvedStage = [IO.Path]::GetFullPath($stage)
    if (-not $resolvedStage.StartsWith($resolvedDist, [StringComparison]::OrdinalIgnoreCase) -or (Split-Path -Leaf $resolvedStage) -ne "package") { throw "Refusing to clear an unexpected package staging directory." }
    Remove-Item -LiteralPath $resolvedStage -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $stage | Out-Null
Copy-Item -LiteralPath $pluginDll -Destination (Join-Path $stage "FoodBuffOverlay.dll") -Force
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
