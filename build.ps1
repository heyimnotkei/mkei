param([int]$BuildId = 0)

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
$overwriteExisting = $BuildId -ne 0
if (-not $overwriteExisting) {
    do {
        $BuildId = Get-Random -Minimum 1000000000 -Maximum 2147483647
        $releaseExe = Join-Path $releaseDir "$projName $BuildId.exe"
    } while (Test-Path $releaseExe)
} else {
    if ($BuildId -lt 1000000000 -or $BuildId -ge 2147483647) {
        throw 'BuildId must be between 1000000000 and 2147483646.'
    }
    $releaseExe = Join-Path $releaseDir "$projName $BuildId.exe"
}

[System.IO.File]::Copy($publishedExe, $releaseExe, $overwriteExisting)

Write-Host "Build complete. Executable: $publishedExe"
Write-Host "Build archive: $releaseExe"
