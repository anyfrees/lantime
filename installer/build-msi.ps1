param(
    [string]$PayloadDirectory,
    [string]$OutputFile,
    [string]$Version = '1.0.3'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $PayloadDirectory) { $PayloadDirectory = Join-Path $projectRoot 'dist\payload' }
if (-not $OutputFile) { $OutputFile = Join-Path $projectRoot ('dist\LanTime-' + $Version + '-Setup.msi') }
$PayloadDirectory = (Resolve-Path -LiteralPath $PayloadDirectory).Path
$OutputFile = [IO.Path]::GetFullPath($OutputFile)
if (Test-Path -LiteralPath $OutputFile) { Remove-Item -LiteralPath $OutputFile -Force }
New-Item -ItemType Directory -Force -Path (Split-Path $OutputFile -Parent) | Out-Null
$staging = Join-Path ([IO.Path]::GetTempPath()) ('LanTime-msi-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $staging | Out-Null
$engine = New-Object -ComObject WindowsInstaller.Installer
$database = $engine.OpenDatabase($OutputFile, 3)
[IO.File]::WriteAllText((Join-Path $staging '_ForceCodepage.idt'),"`r`n`r`n936`t_ForceCodepage`r`n",[Text.Encoding]::ASCII)
$database.Import($staging,'_ForceCodepage.idt')
function Invoke-Sql([string]$Sql) {
    $view = $database.OpenView($Sql)
    try { $view.Execute() } finally { $view.Close();[Runtime.InteropServices.Marshal]::FinalReleaseComObject($view) | Out-Null }
}
function Add-Row([string]$Table, [string[]]$Columns, [object[]]$Values) {
    $quotedColumns = ($Columns | ForEach-Object { '`' + $_ + '`' }) -join ','
    $sql = 'INSERT INTO `' + $Table + '` (' + $quotedColumns + ') VALUES (' + (($Values | ForEach-Object {'?'}) -join ',') + ')'
    $view = $database.OpenView($sql)
    $record = $engine.CreateRecord($Values.Count)
    for ($n = 0; $n -lt $Values.Count; $n++) {
        if ($null -eq $Values[$n]) { continue }
        if ($Values[$n] -is [int]) { $record.IntegerData($n + 1) = $Values[$n] }
        else { $record.StringData($n + 1) = [string]$Values[$n] }
    }
    try { $view.Execute($record) } finally { $view.Close();[Runtime.InteropServices.Marshal]::FinalReleaseComObject($record) | Out-Null;[Runtime.InteropServices.Marshal]::FinalReleaseComObject($view) | Out-Null }
}
function Add-Stream([string]$Table, [string]$Name, [string]$File) {
    $record = $engine.CreateRecord(2)
    $record.StringData(1) = $Name
    $record.SetStream(2, $File)
    $view = $database.OpenView('INSERT INTO `' + $Table + '` (`Name`,`Data`) VALUES (?,?)')
    try { $view.Execute($record) } finally { $view.Close();[Runtime.InteropServices.Marshal]::FinalReleaseComObject($record) | Out-Null;[Runtime.InteropServices.Marshal]::FinalReleaseComObject($view) | Out-Null }
}
function Stable-Guid([string]$Name) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { $bytes = $algorithm.ComputeHash([Text.Encoding]::UTF8.GetBytes('nnu.cn/LanTime/' + $Name)) }
    finally { $algorithm.Dispose() }
    $guidBytes = New-Object byte[] 16
    [Array]::Copy($bytes, $guidBytes, 16)
    return '{' + ([Guid]::new($guidBytes)).ToString().ToUpperInvariant() + '}'
}
$schemas = @(
    'CREATE TABLE `Property` (`Property` CHAR(72) NOT NULL, `Value` CHAR(0) LOCALIZABLE PRIMARY KEY `Property`)',
    'CREATE TABLE `Directory` (`Directory` CHAR(72) NOT NULL, `Directory_Parent` CHAR(72), `DefaultDir` CHAR(255) NOT NULL LOCALIZABLE PRIMARY KEY `Directory`)',
    'CREATE TABLE `Component` (`Component` CHAR(72) NOT NULL, `ComponentId` CHAR(38), `Directory_` CHAR(72) NOT NULL, `Attributes` SHORT NOT NULL, `Condition` CHAR(255), `KeyPath` CHAR(72) PRIMARY KEY `Component`)',
    'CREATE TABLE `Feature` (`Feature` CHAR(38) NOT NULL, `Feature_Parent` CHAR(38), `Title` CHAR(64) LOCALIZABLE, `Description` CHAR(255) LOCALIZABLE, `Display` SHORT, `Level` SHORT NOT NULL, `Directory_` CHAR(72), `Attributes` SHORT NOT NULL PRIMARY KEY `Feature`)',
    'CREATE TABLE `FeatureComponents` (`Feature_` CHAR(38) NOT NULL, `Component_` CHAR(72) NOT NULL PRIMARY KEY `Feature_`,`Component_`)',
    'CREATE TABLE `File` (`File` CHAR(72) NOT NULL, `Component_` CHAR(72) NOT NULL, `FileName` CHAR(255) NOT NULL LOCALIZABLE, `FileSize` LONG NOT NULL, `Version` CHAR(72), `Language` CHAR(20), `Attributes` SHORT, `Sequence` LONG NOT NULL PRIMARY KEY `File`)',
    'CREATE TABLE `Media` (`DiskId` SHORT NOT NULL, `LastSequence` LONG NOT NULL, `DiskPrompt` CHAR(64) LOCALIZABLE, `Cabinet` CHAR(255), `VolumeLabel` CHAR(32), `Source` CHAR(72) PRIMARY KEY `DiskId`)',
    'CREATE TABLE `Registry` (`Registry` CHAR(72) NOT NULL, `Root` SHORT NOT NULL, `Key` CHAR(255) NOT NULL LOCALIZABLE, `Name` CHAR(255) LOCALIZABLE, `Value` CHAR(0) LOCALIZABLE, `Component_` CHAR(72) NOT NULL PRIMARY KEY `Registry`)',
    'CREATE TABLE `Shortcut` (`Shortcut` CHAR(72) NOT NULL, `Directory_` CHAR(72) NOT NULL, `Name` CHAR(128) NOT NULL LOCALIZABLE, `Component_` CHAR(72) NOT NULL, `Target` CHAR(255) NOT NULL, `Arguments` CHAR(255), `Description` CHAR(255) LOCALIZABLE, `Hotkey` SHORT, `Icon_` CHAR(72), `IconIndex` SHORT, `ShowCmd` SHORT, `WkDir` CHAR(72) PRIMARY KEY `Shortcut`)',
    'CREATE TABLE `Icon` (`Name` CHAR(72) NOT NULL, `Data` OBJECT NOT NULL PRIMARY KEY `Name`)',
    'CREATE TABLE `Binary` (`Name` CHAR(72) NOT NULL, `Data` OBJECT NOT NULL PRIMARY KEY `Name`)',
    'CREATE TABLE `CustomAction` (`Action` CHAR(72) NOT NULL, `Type` SHORT NOT NULL, `Source` CHAR(72), `Target` CHAR(255) PRIMARY KEY `Action`)',
    'CREATE TABLE `InstallExecuteSequence` (`Action` CHAR(72) NOT NULL, `Condition` CHAR(255), `Sequence` SHORT PRIMARY KEY `Action`)',
    'CREATE TABLE `InstallUISequence` (`Action` CHAR(72) NOT NULL, `Condition` CHAR(255), `Sequence` SHORT PRIMARY KEY `Action`)',
    'CREATE TABLE `AdminExecuteSequence` (`Action` CHAR(72) NOT NULL, `Condition` CHAR(255), `Sequence` SHORT PRIMARY KEY `Action`)',
    'CREATE TABLE `AdminUISequence` (`Action` CHAR(72) NOT NULL, `Condition` CHAR(255), `Sequence` SHORT PRIMARY KEY `Action`)',
    'CREATE TABLE `LaunchCondition` (`Condition` CHAR(255) NOT NULL, `Description` CHAR(255) NOT NULL LOCALIZABLE PRIMARY KEY `Condition`)',
    'CREATE TABLE `AppSearch` (`Property` CHAR(72) NOT NULL, `Signature_` CHAR(72) NOT NULL PRIMARY KEY `Property`,`Signature_`)',
    'CREATE TABLE `RegLocator` (`Signature_` CHAR(72) NOT NULL, `Root` SHORT NOT NULL, `Key` CHAR(255) NOT NULL, `Name` CHAR(255), `Type` SHORT PRIMARY KEY `Signature_`)',
    'CREATE TABLE `Upgrade` (`UpgradeCode` CHAR(38) NOT NULL, `VersionMin` CHAR(20), `VersionMax` CHAR(20), `Language` CHAR(255), `Attributes` LONG NOT NULL, `Remove` CHAR(255), `ActionProperty` CHAR(72) NOT NULL PRIMARY KEY `UpgradeCode`,`VersionMin`,`VersionMax`,`Language`,`Attributes`)',
    'CREATE TABLE `CreateFolder` (`Directory_` CHAR(72) NOT NULL, `Component_` CHAR(72) NOT NULL PRIMARY KEY `Directory_`,`Component_`)',
    'CREATE TABLE `RemoveFile` (`FileKey` CHAR(72) NOT NULL, `Component_` CHAR(72) NOT NULL, `FileName` CHAR(255) LOCALIZABLE, `DirProperty` CHAR(72) NOT NULL, `InstallMode` SHORT NOT NULL PRIMARY KEY `FileKey`)'
)
foreach ($schema in $schemas) { Invoke-Sql $schema }
$upgradeCode = '{17B66FBF-0492-452F-B574-D071E2F43B3A}'
$productCode = Stable-Guid ('Product/' + $Version)
$properties = [ordered]@{
    ProductCode=$productCode; UpgradeCode=$upgradeCode; ProductName='LanTime 局域网时间站'; ProductVersion=$Version;
    ProductLanguage='2052'; Manufacturer='chengkai'; ALLUSERS='1'; INSTALLLEVEL='1';
    ARPCOMMENTS='多源 NTP 服务与本机自动校时'; ARPURLINFOABOUT='https://nnu.cn'; ARPHELPLINK='https://github.com/anyfrees/lantime';
    ARPPRODUCTICON='LanTimeIcon'; ARPNOMODIFY='1'; SecureCustomProperties='OLDPRODUCTS;NEWERPRODUCTS;NETFRAMEWORK4;NETFRAMEWORK464;INSTALLFOLDER';
    REBOOT='ReallySuppress'; DefaultUIFont='Tahoma8'; MSIRESTARTMANAGERCONTROL='Disable';
}
foreach ($pair in $properties.GetEnumerator()) { Add-Row Property @('Property','Value') @($pair.Key,$pair.Value) }
$directories = @(
    @('TARGETDIR',$null,'SourceDir'),@('ProgramFilesFolder','TARGETDIR','.'),@('INSTALLFOLDER','ProgramFilesFolder','LanTime'),
    @('ProgramMenuFolder','TARGETDIR','.'),@('MenuFolder','ProgramMenuFolder','LanTime'),@('DesktopFolder','TARGETDIR','.'),
    @('CommonAppDataFolder','TARGETDIR','.'),@('DATADIR','CommonAppDataFolder','LanTime')
)
foreach ($dir in $directories) { Add-Row Directory @('Directory','Directory_Parent','DefaultDir') $dir }
Add-Row Feature @('Feature','Title','Description','Display','Level','Directory_','Attributes') @('Main','LanTime','LanTime 应用与说明',1,1,'INSTALLFOLDER',0)
$files = @(Get-ChildItem -LiteralPath $PayloadDirectory -File -Recurse | Sort-Object FullName)
$payloadDirectories=@{''='INSTALLFOLDER'}
$sequence = 0
$ddf = [Collections.Generic.List[string]]::new()
$ddf.Add('.OPTION EXPLICIT'); $ddf.Add('.Set CabinetNameTemplate=LanTime.cab'); $ddf.Add('.Set DiskDirectoryTemplate=' + $staging)
$ddf.Add('.Set InfFileName=' + (Join-Path $staging 'package.inf'));$ddf.Add('.Set RptFileName=' + (Join-Path $staging 'package.rpt'))
$ddf.Add('.Set CompressionType=MSZIP');$ddf.Add('.Set Cabinet=ON');$ddf.Add('.Set Compress=ON');$ddf.Add('.Set MaxDiskSize=0')
$exeId = $null
foreach ($file in $files) {
    $sequence++; $id = 'F' + $sequence.ToString('D4'); $component = 'C' + $sequence.ToString('D4')
    $relativeDirectory=$file.DirectoryName.Substring($PayloadDirectory.Length).TrimStart('\')
    if(-not $payloadDirectories.ContainsKey($relativeDirectory)) {
        $prefix='';$parentDirectory='INSTALLFOLDER'
        foreach($segment in $relativeDirectory.Split('\')) {
            $prefix=if($prefix){$prefix+'\'+$segment}else{$segment}
            if(-not $payloadDirectories.ContainsKey($prefix)){
                $directoryId='D'+(Stable-Guid ('Directory/'+$prefix)).Trim('{}').Replace('-','')
                Add-Row Directory @('Directory','Directory_Parent','DefaultDir') @($directoryId,$parentDirectory,$segment)
                $payloadDirectories[$prefix]=$directoryId
            }
            $parentDirectory=$payloadDirectories[$prefix]
        }
    }
    $stagedFile = Join-Path $staging ($id + '.bin');Copy-Item -LiteralPath $file.FullName -Destination $stagedFile
    $ddf.Add('"' + $stagedFile + '" ' + $id)
    $fileVersion = if ($file.Name -eq 'LanTime.exe') { $Version + '.0' } else { $null }
    if ($file.Name -eq 'LanTime.exe') { $exeId=$id }
    Add-Row Component @('Component','ComponentId','Directory_','Attributes','KeyPath') @($component,(Stable-Guid ('File/' + $relativeDirectory + '/' + $file.Name)),$payloadDirectories[$relativeDirectory],0,$id)
    Add-Row FeatureComponents @('Feature_','Component_') @('Main',$component)
    Add-Row File @('File','Component_','FileName','FileSize','Version','Attributes','Sequence') @($id,$component,($id+'|'+$file.Name),[int]$file.Length,$fileVersion,512,$sequence)
}
if (-not $exeId) {throw 'Payload has no LanTime.exe'}
$ddfPath = Join-Path $staging 'package.ddf';[IO.File]::WriteAllLines($ddfPath,$ddf,[Text.Encoding]::ASCII)
& (Join-Path $env:WINDIR 'System32\makecab.exe') /F $ddfPath | Out-Null
if ($LASTEXITCODE -ne 0) {throw 'Cabinet build failed'}
Add-Stream _Streams 'LanTime.cab' (Join-Path $staging 'LanTime.cab')
Add-Row Media @('DiskId','LastSequence','Cabinet') @(1,$sequence,'#LanTime.cab')
Add-Stream Icon 'LanTimeIcon' (Join-Path $projectRoot 'app.ico')
Add-Stream Binary 'Maintenance' (Join-Path $PayloadDirectory 'LanTime.exe')
Add-Row Component @('Component','ComponentId','Directory_','Attributes','KeyPath') @('DataComponent',(Stable-Guid 'DataComponent'),'DATADIR',4,'DataRegistry')
Add-Row FeatureComponents @('Feature_','Component_') @('Main','DataComponent')
Add-Row Registry @('Registry','Root','Key','Name','Value','Component_') @('DataRegistry',2,'Software\chengkai\LanTime','InstallLocation','[INSTALLFOLDER]','DataComponent')
Add-Row CreateFolder @('Directory_','Component_') @('DATADIR','DataComponent')
Add-Row CreateFolder @('Directory_','Component_') @('MenuFolder','DataComponent')
Add-Row RemoveFile @('FileKey','Component_','DirProperty','InstallMode') @('RemoveMenuFolder','DataComponent','MenuFolder',2)
foreach ($shortcut in @(@('StartMenu','MenuFolder'),@('Desktop','DesktopFolder'))) {
    Add-Row Shortcut @('Shortcut','Directory_','Name','Component_','Target','Description','Icon_','IconIndex','ShowCmd','WkDir') @($shortcut[0],$shortcut[1],'LanTime','DataComponent',('[#'+$exeId+']'),'LanTime 局域网时间站','LanTimeIcon',0,1,'INSTALLFOLDER')
}
Add-Row AppSearch @('Property','Signature_') @('NETFRAMEWORK4','Net4')
Add-Row AppSearch @('Property','Signature_') @('NETFRAMEWORK464','Net464')
Add-Row RegLocator @('Signature_','Root','Key','Name','Type') @('Net4',2,'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full','Install',2)
Add-Row RegLocator @('Signature_','Root','Key','Name','Type') @('Net464',2,'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full','Install',18)
Add-Row LaunchCondition @('Condition','Description') @('Installed OR VersionNT >= 601','需要 Windows 7 或更新版本。')
Add-Row LaunchCondition @('Condition','Description') @('Installed OR NETFRAMEWORK4 OR NETFRAMEWORK464','请先安装 .NET Framework 4.0 完整版或兼容的 4.x，再运行安装包。')
Add-Row LaunchCondition @('Condition','Description') @('NOT NEWERPRODUCTS','已安装较新版本的 LanTime，请先卸载较新版本。')
Add-Row Upgrade @('UpgradeCode','VersionMin','VersionMax','Attributes','ActionProperty') @($upgradeCode,'0.0.0',$Version,257,'OLDPRODUCTS')
Add-Row Upgrade @('UpgradeCode','VersionMin','Attributes','ActionProperty') @($upgradeCode,$Version,2,'NEWERPRODUCTS')
Add-Row CustomAction @('Action','Type','Source','Target') @('StopOwnedService',3074,'Maintenance','--installer-stop "[INSTALLFOLDER]LanTime.exe"')
Add-Row CustomAction @('Action','Type','Source','Target') @('RemoveOwnedService',3074,'Maintenance','--installer-uninstall "[INSTALLFOLDER]LanTime.exe"')
$execute = @(
 @('FindRelatedProducts',$null,25),@('AppSearch',$null,50),@('LaunchConditions',$null,100),@('ValidateProductID',$null,700),
 @('CostInitialize',$null,800),@('FileCost',$null,900),@('CostFinalize',$null,1000),@('MigrateFeatureStates',$null,1200),
 @('InstallValidate',$null,1400),@('InstallInitialize',$null,1500),@('RemoveExistingProducts','OLDPRODUCTS',1510),
 @('ProcessComponents',$null,1600),@('UnpublishFeatures',$null,1800),@('StopOwnedService','NOT (REMOVE="ALL") OR UPGRADINGPRODUCTCODE',1900),
 @('RemoveOwnedService','REMOVE="ALL" AND NOT UPGRADINGPRODUCTCODE',1910),@('RemoveRegistryValues',$null,2600),@('RemoveShortcuts',$null,3200),@('RemoveFiles',$null,3500),@('RemoveFolders',$null,3600),
 @('CreateFolders',$null,3700),@('InstallFiles',$null,4000),@('CreateShortcuts',$null,4500),@('WriteRegistryValues',$null,5000),
 @('RegisterUser',$null,6000),@('RegisterProduct',$null,6100),@('PublishFeatures',$null,6300),@('PublishProduct',$null,6400),@('InstallFinalize',$null,6600)
)
foreach ($action in $execute) { Add-Row InstallExecuteSequence @('Action','Condition','Sequence') $action }
foreach ($action in @(@('FindRelatedProducts',$null,25),@('AppSearch',$null,50),@('LaunchConditions',$null,100),@('CostInitialize',$null,800),@('FileCost',$null,900),@('CostFinalize',$null,1000),@('ExecuteAction',$null,1300))) { Add-Row InstallUISequence @('Action','Condition','Sequence') $action }
foreach ($action in @(@('CostInitialize',$null,800),@('FileCost',$null,900),@('CostFinalize',$null,1000),@('InstallValidate',$null,1400),@('InstallInitialize',$null,1500),@('InstallAdminPackage',$null,3900),@('InstallFiles',$null,4000),@('InstallFinalize',$null,6600))) { Add-Row AdminExecuteSequence @('Action','Condition','Sequence') $action }
foreach ($action in @(@('CostInitialize',$null,800),@('FileCost',$null,900),@('CostFinalize',$null,1000),@('ExecuteAction',$null,1300))) { Add-Row AdminUISequence @('Action','Condition','Sequence') $action }
$summary = $database.SummaryInformation(20)
$summary.Property(1)=936;$summary.Property(2)='LanTime Setup';$summary.Property(3)='LanTime 局域网时间站';$summary.Property(4)='chengkai'
$summary.Property(7)='Intel;2052';$summary.Property(9)='{' + [Guid]::NewGuid().ToString().ToUpperInvariant() + '}'
$summary.Property(14)=200;$summary.Property(15)=2;$summary.Property(18)='LanTime native MSI builder'
$summary.Persist();$database.Commit()
[Runtime.InteropServices.Marshal]::FinalReleaseComObject($summary) | Out-Null
[Runtime.InteropServices.Marshal]::FinalReleaseComObject($database) | Out-Null
[Runtime.InteropServices.Marshal]::FinalReleaseComObject($engine) | Out-Null
[GC]::Collect();[GC]::WaitForPendingFinalizers()
Write-Host ('MSI: ' + $OutputFile)
