param(
    [string]$GamePath='C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option',
    [ValidateSet('C','NA')][string]$AircraftVariant='C',
    [string]$ReceiptPath
)
$ErrorActionPreference='Stop'
$ruleName='FS20'+$AircraftVariant+'.catapults'
$key=if($AircraftVariant -eq 'NA'){'JP_FS20_NA'}else{'JP_FS20_CTOL'}
if(!$ReceiptPath){$ReceiptPath='D:/Artifacts/fs20-penumbra/'+$(if($AircraftVariant -eq 'NA'){'NA-'}else{''})+'INSTALLATION.json'}
$root=Split-Path -Parent $PSScriptRoot
$game=[IO.Path]::GetFullPath($GamePath).TrimEnd([IO.Path]::DirectorySeparatorChar)
$plugins=Join-Path $game 'BepInEx/plugins'
if(!(Test-Path (Join-Path $game 'NuclearOption.exe'))){throw 'NuclearOption.exe was not found in GamePath.'}
foreach($process in Get-Process -Name NuclearOption -ErrorAction SilentlyContinue){
    if($process.Path -and [IO.Path]::GetDirectoryName($process.Path) -eq $game){throw 'Close this Nuclear Option instance before installing. The installer will not close it.'}
}
$modules=@(Get-ChildItem -LiteralPath $plugins -Filter 'Aryx_FS-41.Eclipse_*.dll' -File -Recurse)
if($modules.Count -ne 1){throw 'Expected one enabled Aryx FS-41/Penumbra module. No files were changed.'}
$module=$modules[0]
if(!(Test-Path (Join-Path $module.DirectoryName 'example.catapults'))){throw 'The installed Aryx module does not expose the verified external catapult configuration. No files were changed.'}
$source=Join-Path $root ('packaging/fs20-penumbra/'+$ruleName)
$destination=Join-Path $module.DirectoryName $ruleName
$sourceHash=(Get-FileHash -LiteralPath $source).Hash.ToLowerInvariant()
$before=(Get-FileHash -LiteralPath $module.FullName).Hash
$fs20=@(Get-ChildItem -LiteralPath $plugins -Filter 'VortexConventional.dll' -File -Recurse)
if($fs20.Count -ne 1){throw 'Expected one enabled FS-20 family plugin. No files were changed.'}
$fs20Before=(Get-FileHash -LiteralPath $fs20[0].FullName).Hash
& (Join-Path $PSScriptRoot 'check_fs20_penumbra.ps1') -GamePath $game -AircraftVariant $AircraftVariant
if(!$?){throw 'The installed catapult compatibility contracts failed. No files were changed.'}
$backup=$null
if(Test-Path -LiteralPath $destination){
    if((Get-FileHash -LiteralPath $destination).Hash.ToLowerInvariant() -ne $sourceHash){
        $backup=Join-Path $game ('BepInEx/fs20-backups/'+[Guid]::NewGuid().ToString('N')+'/'+$ruleName)
        New-Item -ItemType Directory -Path (Split-Path -Parent $backup) -Force|Out-Null
        Copy-Item -LiteralPath $destination -Destination $backup
    }
}
Copy-Item -LiteralPath $source -Destination $destination -Force
if((Get-FileHash -LiteralPath $destination).Hash.ToLowerInvariant() -ne $sourceHash){throw 'Installed rule differs from the source.'}
& (Join-Path $PSScriptRoot 'check_fs20_penumbra.ps1') -GamePath $game -AircraftVariant $AircraftVariant -RequireInstalledRule -ReportPath ($ReceiptPath+'.checks.json')
if(!$?){throw 'Post-install rule discovery verification failed.'}
if((Get-FileHash -LiteralPath $module.FullName).Hash -ne $before -or (Get-FileHash -LiteralPath $fs20[0].FullName).Hash -ne $fs20Before){throw 'A dependency DLL changed during installation; review concurrent work.'}
$receipt=[ordered]@{game=$game;rule=$destination;ruleSha256=$sourceHash;aircraftKey=$key;catapultModule=$module.FullName;catapultDllUnchanged=$true;fs20DllUnchanged=$true;backup=$backup;gameLaunched=$false;liveFlightTested=$false}
New-Item -ItemType Directory -Path (Split-Path -Parent $ReceiptPath) -Force|Out-Null
$receipt|ConvertTo-Json|Set-Content -LiteralPath $ReceiptPath -Encoding utf8
$receipt|ConvertTo-Json -Compress
