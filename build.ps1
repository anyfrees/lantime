$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x C# compiler not found.' }
$outDir = Join-Path $PSScriptRoot 'release'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$files = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
$compilerArgs = @('/nologo','/target:winexe','/optimize+','/platform:anycpu','/codepage:65001',"/win32manifest:$PSScriptRoot\app.manifest","/win32icon:$PSScriptRoot\app.ico","/resource:$PSScriptRoot\LICENSE.txt,LanTime.LICENSE.txt",'/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll','/reference:System.ServiceProcess.dll','/reference:System.Runtime.Serialization.dll',"/out:$outDir\LanTime.exe")
& $compiler @compilerArgs @files
if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LanTime.exe.config') -Destination $outDir -Force
Write-Host "Built: $outDir\LanTime.exe"
