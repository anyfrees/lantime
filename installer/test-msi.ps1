param([Parameter(Mandatory=$true)][string]$PackageFile,[string]$ReportFile)
$ErrorActionPreference='Stop'
$PackageFile=(Resolve-Path -LiteralPath $PackageFile).Path
$results=[Collections.Generic.List[string]]::new()
$failures=0
function Check([bool]$Condition,[string]$Name){if($Condition){$results.Add('PASS '+$Name)}else{$script:failures++;$results.Add('FAIL '+$Name)}}
$installer=New-Object -ComObject WindowsInstaller.Installer
$installer.UILevel=2
$database=$null;$session=$null
try{
    $database=$installer.OpenDatabase($PackageFile,0)
    $tableView=$database.OpenView('SELECT `Name` FROM `_Tables`')
    $tables=[Collections.Generic.List[string]]::new()
    try{$tableView.Execute();while($record=$tableView.Fetch()){$tables.Add($record.StringData(1));[Runtime.InteropServices.Marshal]::FinalReleaseComObject($record)|Out-Null}}
    finally{$tableView.Close();[Runtime.InteropServices.Marshal]::FinalReleaseComObject($tableView)|Out-Null}
    foreach($required in @('Signature','AppSearch','RegLocator','CompLocator','IniLocator','DrLocator','InstallExecuteSequence','InstallUISequence','AdminExecuteSequence','File','Media','Component','FeatureComponents','Shortcut','Upgrade')){Check ($tables.Contains($required)) ('required table '+$required)}
    if($failures -eq 0){
        # Option 1 creates a safe session: these checks cannot install files,
        # register the product, change settings, or alter the current machine.
        $session=$installer.OpenPackage($PackageFile,1)
        foreach($action in @('AppSearch','LaunchConditions','CostInitialize','FileCost','CostFinalize')){
            $status=$session.DoAction($action)
            Check ($status -eq 1) ('native Windows Installer '+$action+' returned '+$status)
            if($status -ne 1){break}
        }
        Check ([bool]$session.Property('NETFRAMEWORK4') -or [bool]$session.Property('NETFRAMEWORK464')) 'native AppSearch found .NET Framework runtime'
    }
}catch{$failures++;$results.Add('FAIL '+$_.Exception.Message)}
finally{
    if($session){[Runtime.InteropServices.Marshal]::FinalReleaseComObject($session)|Out-Null}
    if($database){[Runtime.InteropServices.Marshal]::FinalReleaseComObject($database)|Out-Null}
    [Runtime.InteropServices.Marshal]::FinalReleaseComObject($installer)|Out-Null
    [GC]::Collect();[GC]::WaitForPendingFinalizers()
}
$results.Add('RESULT: '+@($results|Where-Object {$_.StartsWith('PASS ')}).Count+' passed, '+$failures+' failed')
$results.Add('Safe native-engine preflight only; installation lifecycle must be tested separately.')
if($ReportFile){[IO.File]::WriteAllLines([IO.Path]::GetFullPath($ReportFile),$results,[Text.Encoding]::UTF8)}
$results|Write-Output
if($failures){throw ('MSI preflight failed with '+$failures+' errors')}
