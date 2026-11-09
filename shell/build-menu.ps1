# Builds the Windows 11 Explorer menu for an app into -OutDir:
#   <Name>Menu.dll   the IExplorerCommand handler (ExplorerMenu.cpp + MenuConfig.h)
#   <Name>Menu.msix  the sparse package that registers it (AppxManifest.xml + Assets), signed
#   TNT24.cer        the public half of the signing certificate, which the installer trusts on the PC
# Returns the certificate's thumbprint.
#
# Windows only accepts the package when it's signed by a certificate trusted machine-wide, so it's signed with a
# self-signed "CN=TNT24" certificate that's created on the first build and kept in this user's certificate store
# (Cert:\CurrentUser\My). Building on another PC makes a different certificate; installs from either still work.
#
# Needs Visual Studio (or Build Tools) with the C++ workload, which includes the Windows SDK:
#   winget install Microsoft.VisualStudio.2022.BuildTools --override "--quiet --wait --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended"
param(
    [Parameter(Mandatory)][string]$Name,
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$OutDir
)
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$OutDir = (New-Item -ItemType Directory -Force $OutDir).FullName
$obj = (New-Item -ItemType Directory -Force (Join-Path $here 'obj')).FullName
$publisher = 'CN=TNT24' # must match Identity Publisher in AppxManifest.xml

# --- Handler DLL -----------------------------------------------------------------------------
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vs = if (Test-Path $vswhere) {
    & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
}
if (-not $vs) { throw "The C++ build tools weren't found. See the top of shell\build-menu.ps1 for the winget command." }
$vcvars = Join-Path $vs 'VC\Auxiliary\Build\vcvars64.bat'

$dll = Join-Path $OutDir "${Name}Menu.dll"
# Static CRT (/MT) so the DLL needs no Visual C++ redistributable.
$cl = "cl /nologo /std:c++17 /O1 /MT /EHsc /W4 /WX /DUNICODE /D_UNICODE /LD " +
      "`"$here\ExplorerMenu.cpp`" /Fo`"$obj\\`" /Fe`"$dll`" " +
      "/link /DEF:`"$here\ExplorerMenu.def`" /IMPLIB:`"$obj\${Name}Menu.lib`" shlwapi.lib shell32.lib ole32.lib runtimeobject.lib"
# vcvars looks for vswhere on PATH. Everything goes to stdout because Windows PowerShell treats stderr as a failure.
$env:PATH = "$(Split-Path $vswhere);$env:PATH"
cmd /c "call `"$vcvars`" >nul 2>&1 && $cl 2>&1" | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Compiling the Explorer menu DLL failed" }
Remove-Item "$OutDir\${Name}Menu.exp", "$OutDir\${Name}Menu.lib" -ErrorAction SilentlyContinue

# --- Sparse package --------------------------------------------------------------------------
function Find-SdkTool($exe) {
    $tool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Filter $exe -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.Directory.Name -eq 'x64' -and $_.Directory.Parent.Name -match '^\d+(\.\d+){3}$' } |
        Sort-Object { [version]$_.Directory.Parent.Name } | Select-Object -Last 1
    if (-not $tool) { throw "$exe (Windows SDK) wasn't found" }
    $tool.FullName
}

$layout = Join-Path $obj 'package'
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
New-Item -ItemType Directory $layout | Out-Null
Copy-Item (Join-Path $here 'Assets') $layout -Recurse
# Package versions have four parts.
$packageVersion = (($Version.Split('.') + @('0', '0', '0'))[0..3]) -join '.'
(Get-Content (Join-Path $here 'AppxManifest.xml') -Raw).Replace('{VERSION}', $packageVersion) |
    Set-Content (Join-Path $layout 'AppxManifest.xml') -Encoding UTF8

$msix = Join-Path $OutDir "${Name}Menu.msix"
# /nv: the manifest names files (the exe, the DLL) that live in the install folder, not in the package.
& (Find-SdkTool makeappx.exe) pack /o /nv /d $layout /p $msix | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makeappx failed" }

# --- Signing ---------------------------------------------------------------------------------
$cert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq $publisher -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date).AddDays(30) } |
    Sort-Object NotAfter | Select-Object -Last 1
if (-not $cert) {
    # Code signing EKU, not a CA. Ten years, so installs don't need re-trusting any time soon.
    $cert = New-SelfSignedCertificate -Type Custom -Subject $publisher -KeyUsage DigitalSignature `
        -FriendlyName 'TNT24 package signing (Renamerama, VideoSqueeze)' -CertStoreLocation Cert:\CurrentUser\My `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}') -NotAfter (Get-Date).AddYears(10)
    Write-Host "Created signing certificate $publisher ($($cert.Thumbprint)) in Cert:\CurrentUser\My"
}
& (Find-SdkTool signtool.exe) sign /q /fd SHA256 /sha1 $cert.Thumbprint /s My $msix | Out-Host
if ($LASTEXITCODE -ne 0) { throw "signtool failed" }
Export-Certificate -Cert $cert -FilePath (Join-Path $OutDir 'TNT24.cer') | Out-Null

Write-Host "Built $dll and $msix ($packageVersion, signed by $($cert.Thumbprint))"
$cert.Thumbprint
