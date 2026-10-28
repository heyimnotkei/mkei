$ErrorActionPreference = 'Stop'

$projName = 'MKei'

if (-not (Test-Path "$projName.csproj")) {
@"
<Project Sdk="Microsoft.NET.Sdk.WindowsDesktop">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="NAudio" Version="2.2.1" />
  </ItemGroup>
</Project>
"@ | Set-Content "$projName.csproj"
}

dotnet restore

dotnet publish -c Release -r win-x64 `
    -p:PublishSingleFile=true `
    -p:IncludeAllContentForSelfExtract=true `
    -p:PublishTrimmed=false `
    --self-contained true `
    -o "publish"

$publishedExe = Join-Path "publish" "$projName.exe"
if (-not (Test-Path $publishedExe)) {
    throw "Published executable not found: $publishedExe"
}

$releaseDir = "releases"
New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
$buildId = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$releaseExe = Join-Path $releaseDir "$projName 3.0 build $buildId.exe"
Copy-Item $publishedExe $releaseExe

Write-Host "Build complete. Executable: $publishedExe"
Write-Host "Versioned release: $releaseExe"
