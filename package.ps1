param([switch]$SkipTests,[switch]$SkipMsi)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$versionMatch=[regex]::Match((Get-Content -LiteralPath (Join-Path $root 'src\AppInfo.cs') -Raw),'Version="([0-9.]+)"')
if(-not $versionMatch.Success){throw 'Cannot read application version'}
$version=$versionMatch.Groups[1].Value
& (Join-Path $root 'build.ps1')
$dist=Join-Path $root 'dist'
$payload=Join-Path $dist 'payload'
$portableRoot=Join-Path $dist 'portable'
$portable=Join-Path $portableRoot 'LanTime'
$evidence=Join-Path $dist 'verification'
New-Item -ItemType Directory -Force -Path $payload,$portable,$evidence | Out-Null
foreach($name in @('LanTime.exe','LanTime.exe.config')){Copy-Item -LiteralPath (Join-Path $root ('release\'+$name)) -Destination $payload -Force}
foreach($name in @('README.md','LICENSE.txt','CHANGELOG.md')){Copy-Item -LiteralPath (Join-Path $root $name) -Destination $payload -Force}
Copy-Item -LiteralPath (Join-Path $root 'app.ico') -Destination $payload -Force
Copy-Item -LiteralPath (Join-Path $root 'installer\installed.mode') -Destination $payload -Force
Copy-Item -LiteralPath (Join-Path $root 'docs') -Destination $payload -Recurse -Force
foreach($file in Get-ChildItem -LiteralPath $payload -File){if($file.Name -ne 'installed.mode'){Copy-Item -LiteralPath $file.FullName -Destination $portable -Force}}
Copy-Item -LiteralPath (Join-Path $payload 'docs') -Destination $portable -Recurse -Force
if(Test-Path -LiteralPath (Join-Path $portable 'installed.mode')){throw 'Portable payload must not use installed layout'}
if(-not $SkipTests){
    $testProcess=Start-Process -FilePath (Join-Path $portable 'LanTime.exe') -ArgumentList @('--self-test',('"'+(Join-Path $evidence 'self-tests.txt')+'"')) -WindowStyle Hidden -Wait -PassThru
    if($testProcess.ExitCode -ne 0){throw 'Functional checks failed; see dist/verification/self-tests.txt'}
    $uiProcess=Start-Process -FilePath (Join-Path $portable 'LanTime.exe') -ArgumentList @('--ui-check',('"'+(Join-Path $evidence 'ui')+'"')) -WindowStyle Hidden -Wait -PassThru
    if($uiProcess.ExitCode -ne 0){throw 'UI checks failed; see dist/verification/ui/ui-checks.txt'}
}
$portableZip=Join-Path $dist ('LanTime-'+$version+'-Portable.zip')
Compress-Archive -LiteralPath $portable -DestinationPath $portableZip -Force
if(-not $SkipMsi){& (Join-Path $root 'installer\build-msi.ps1') -PayloadDirectory $payload -OutputFile (Join-Path $dist ('LanTime-'+$version+'-Setup.msi')) -Version $version}
$hashLines=Get-ChildItem -LiteralPath $dist -File | Where-Object {$_.Name -eq ('LanTime-'+$version+'-Portable.zip') -or $_.Name -eq ('LanTime-'+$version+'-Setup.msi')} | Sort-Object Name | ForEach-Object {((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant())+'  '+$_.Name}
[IO.File]::WriteAllLines((Join-Path $dist 'SHA256SUMS.txt'),$hashLines,[Text.Encoding]::ASCII)
Write-Host ('Packages: '+$dist)
