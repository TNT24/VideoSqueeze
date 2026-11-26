# Fetches the FFmpeg build that ships with VideoSqueeze into .\ffmpeg (ffmpeg.exe, ffprobe.exe, their DLLs, LICENSE).
# The build copies that folder next to VideoSqueeze.exe, where Tools.Locate looks first. Run by build-installer.ps1;
# run it once yourself before `dotnet build` / `dotnet test` on a fresh clone.
#
# gyan.dev "full" shared build: has libsvtav1, libx265, NVENC and libvmaf (the "essentials" build lacks SVT-AV1).
# Versioned releases on GitHub stay up, unlike the nightly builds. To upgrade, change both values and run the tests.
$Version = '9.1'
$Sha256 = 'd010a19b78d5eae62dca8d4c7f195acd56dae8ed6fb85f72821edd9437da2898'

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$name = "ffmpeg-$Version-full_build-shared"
$marker = 'ffmpeg\VERSION.txt'
if ((Test-Path $marker) -and (Get-Content $marker -Raw).Trim() -eq $name) { return }

$cache = 'ffmpeg-cache'
$zip = "$cache\$name.zip"
New-Item -ItemType Directory -Force $cache | Out-Null
if (-not (Test-Path $zip) -or (Get-FileHash $zip -Algorithm SHA256).Hash -ne $Sha256) {
    Write-Host "Downloading FFmpeg $Version..."
    $ProgressPreference = 'SilentlyContinue' # the progress bar makes Invoke-WebRequest many times slower
    Invoke-WebRequest "https://github.com/GyanD/codexffmpeg/releases/download/$Version/$name.zip" -OutFile $zip
    if ((Get-FileHash $zip -Algorithm SHA256).Hash -ne $Sha256) { Remove-Item $zip; throw "FFmpeg download doesn't match its SHA-256." }
}

$tmp = "$cache\extract"
if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
Expand-Archive $zip $tmp
if (Test-Path ffmpeg) { Remove-Item ffmpeg -Recurse -Force }
New-Item -ItemType Directory ffmpeg | Out-Null
Get-ChildItem "$tmp\$name\bin" | Where-Object Name -ne 'ffplay.exe' | Copy-Item -Destination ffmpeg
Copy-Item "$tmp\$name\LICENSE" ffmpeg\LICENSE.txt
@"
FFmpeg $Version (gyan.dev full shared build), included unmodified. FFmpeg is free software under the GNU GPL v3;
see LICENSE.txt. Source code: https://ffmpeg.org/download.html and https://github.com/GyanD/codexffmpeg/releases/tag/$Version
"@ | Set-Content ffmpeg\README.txt -Encoding utf8
Remove-Item $tmp -Recurse -Force
Set-Content $marker $name -Encoding ascii
Write-Host "FFmpeg $Version ready in .\ffmpeg"
