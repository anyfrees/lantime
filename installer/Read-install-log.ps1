param([string]$LogPath,[string]$ReportPath,[int]$Result)
$ErrorActionPreference='Stop'
$output=New-Object 'System.Collections.Generic.List[string]'
$output.Add('LanTime installation result: '+$Result)
$output.Add('Full log: '+$LogPath)
$output.Add('OS: '+[Environment]::OSVersion.VersionString)
try {
    $bytes=[IO.File]::ReadAllBytes($LogPath)
    # Win7 MSI logs can be UTF-16LE without a BOM. findstr cannot read them.
    $unicode=($bytes.Length -ge 2 -and $bytes[0] -eq 255 -and $bytes[1] -eq 254)
    $zeroes=0
    for($index=1;$index -lt [Math]::Min(128,$bytes.Length);$index+=2){if($bytes[$index] -eq 0){$zeroes++}}
    if($unicode -or $zeroes -gt 15){$encoding=[Text.Encoding]::Unicode}
    elseif($bytes.Length -ge 3 -and $bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191){$encoding=[Text.Encoding]::UTF8}
    else{$encoding=[Text.Encoding]::Default}
    $lines=[regex]::Split($encoding.GetString($bytes).TrimStart([char]0xFEFF),"\r?\n")
    $output.Add('Detected encoding: '+$encoding.WebName)
    $output.Add('---- Installer failure details ----')
    # Stock Win7 PowerShell 2 uses CLR 2: avoid .NET 4-only SortedSet.
    $selected=@{}
    for($index=0;$index -lt $lines.Length;$index++){
        if($lines[$index] -match 'Return value 3|1722|1721|1925|CustomAction|MainEngineThread|StopOwnedService|RemoveOwnedService|LANTIMESERVICEIMAGEPATH'){
            for($context=[Math]::Max(0,$index-4);$context -le [Math]::Min($lines.Length-1,$index+2);$context++){$selected[$context]=$true}
        }
    }
    foreach($index in @($selected.Keys|Sort-Object)){$output.Add(($index+1).ToString()+': '+$lines[$index])}
    if($selected.Count -eq 0){$output.Add('No matching action: last 50 log lines follow.');for($index=[Math]::Max(0,$lines.Length-50);$index -lt $lines.Length;$index++){$output.Add($lines[$index])}}
}catch{$output.Add('Cannot parse full log: '+$_.Exception.Message)}
[IO.File]::WriteAllLines($ReportPath,$output.ToArray(),(New-Object Text.UTF8Encoding($true)))
