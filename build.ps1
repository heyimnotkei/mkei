$ErrorActionPreference = 'Stop'

$projName = 'MKei'
$projectPath = Join-Path $PSScriptRoot "$projName.csproj"
$publishDir = Join-Path $PSScriptRoot 'publish'
$releaseDir = Join-Path $PSScriptRoot 'releases'

if (-not (Test-Path $projectPath)) {
    throw "Project file not found: $projectPath"
}

& dotnet restore $projectPath
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE."
}

& dotnet publish $projectPath -c Release -r win-x64 `
    -p:PublishSingleFile=true `
    -p:IncludeAllContentForSelfExtract=true `
    -p:PublishTrimmed=false `
    --self-contained true `
    -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$publishedExe = Join-Path $publishDir "$projName.exe"
if (-not (Test-Path $publishedExe)) {
    throw "Published executable not found: $publishedExe"
}

New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
$buildId = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$releaseExe = Join-Path $releaseDir "$projName 3.0 build $buildId.exe"
Copy-Item $publishedExe $releaseExe

Write-Host "Build complete. Executable: $publishedExe"
Write-Host "Versioned release: $releaseExe"
