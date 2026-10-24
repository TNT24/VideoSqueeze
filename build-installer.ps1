# Publishes VideoSqueeze and wraps it in a Windows installer (installer\VideoSqueeze-Setup-<version>.exe).
# -Publish also uploads it as a GitHub release, which installed copies then offer as an update.
param([switch]$Publish)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

[xml]$proj = Get-Content VideoSqueeze.csproj
$version = ($proj.Project.PropertyGroup | Where-Object Version | Select-Object -First 1).Version

if (Test-Path build) { Remove-Item build -Recurse -Force }
dotnet publish -c Release -o build
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup not found. Install it with: winget install JRSoftware.InnoSetup" }

& $iscc "/DAppVersion=$version" installer.iss
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed" }

if ($Publish) {
    if (git status --porcelain) { throw "Commit your changes before publishing." }
    git push
    if ($LASTEXITCODE -ne 0) { throw "git push failed" }
    gh release create "v$version" "installer\VideoSqueeze-Setup-$version.exe" --title "VideoSqueeze $version" --generate-notes --target (git rev-parse HEAD)
    if ($LASTEXITCODE -ne 0) { throw "gh release create failed" }
}
