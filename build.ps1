# Builds ArabicKeyboardFixer.exe and (if Inno Setup is installed) the installer.
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }

# Multi-size icon (16/32/48/128) built from the PNGs in icons\
python make_icon.py
if ($LASTEXITCODE -ne 0) { throw "Icon generation failed" }

# /codepage:65001 is required: the source contains Arabic text.
& $csc /nologo /codepage:65001 /target:winexe /optimize+ /warn:4 `
    /win32icon:app_icon.ico /win32manifest:app.manifest `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll `
    /out:ArabicKeyboardFixer.exe ArabicKeyboardFixer.cs
if ($LASTEXITCODE -ne 0) { throw "Compilation failed" }
Write-Host "Built ArabicKeyboardFixer.exe"

$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if ($iscc) {
    & $iscc /Q installer.iss
    if ($LASTEXITCODE -ne 0) { throw "Installer build failed" }
    Write-Host "Built installer"
} else {
    Write-Host "Inno Setup not found; skipped installer"
}
