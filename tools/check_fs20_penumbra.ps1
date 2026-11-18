param([string]$GamePath='C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option', [ValidateSet('C','NA')][string]$AircraftVariant='C', [string]$Plugin, [string]$ReportPath, [switch]$RequireInstalledRule)
$key=if($AircraftVariant -eq 'NA'){'JP_FS20_NA'}else{'JP_FS20_CTOL'}
$familyField=if($AircraftVariant -eq 'NA'){'variantNA'}else{'variant'}
if(!$ReportPath){$ReportPath='D:/Artifacts/fs20-penumbra/'+$(if($AircraftVariant -eq 'NA'){'NA-'}else{''})+'CONTRACT_CHECKS.json'}
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$plugins=Join-Path $GamePath 'BepInEx/plugins'
Add-Type -LiteralPath (Join-Path $GamePath 'BepInEx/core/Mono.Cecil.dll')
$module=@(Get-ChildItem -LiteralPath $plugins -Filter 'Aryx_FS-41.Eclipse_*.dll' -File -Recurse)
if($module.Count -ne 1){throw 'Expected one Aryx catapult module'}
$assembly=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($module[0].FullName)
$embedded=@($assembly.MainModule.Resources|Where-Object {$_.Name.EndsWith('.catapults')})
if($embedded.Count -ne 1){throw 'Native rule resource changed'}
$text=[Text.Encoding]::UTF8.GetString($embedded[0].GetResourceData())
$native=@($text -split "`n"|Where-Object {$_ -match '^SmallFighter1\s*\|'})
if($native.Count -ne 1){throw 'Expected the verified native FS-20B rule'}
$path=Join-Path $root ('packaging/fs20-penumbra/FS20'+$AircraftVariant+'.catapults')
$new=@(Get-Content -LiteralPath $path|Where-Object {$_ -match ('^'+[Regex]::Escape($key)+'\s*\|')})
if($new.Count -ne 1){throw 'Expected exactly one selected FS-20 rule'}
$originalFields=@($native[0] -split '\|'|ForEach-Object Trim)
$fields=@($new[0] -split '\|'|ForEach-Object Trim)
if($fields.Count -ne 8 -or (($fields[1..7] -join '|') -ne ($originalFields[1..7] -join '|'))){throw 'Selected rule does not match the native retained nose-gear attachment and launch limits'}
if($text -match ('(?m)^'+[Regex]::Escape($key)+'\s*\|')){throw 'The native module already defines this variant; inspect before overriding it'}
$runtime=$assembly.MainModule.Types|Where-Object Name -eq AryxCatapultPatchRuntime
foreach($method in @('ReadFileConfigs','ResolveConfigDirectory','ScanAndPatch')){
    if(!($runtime.Methods|Where-Object Name -eq $method)){throw ('Missing external rule loader: '+$method)}
}
$db=$assembly.MainModule.Types|Where-Object Name -eq AryxCatapultPatchDatabase
$lookup=$db.Methods|Where-Object Name -eq TryGetRule
if(!($lookup.Body.Instructions|Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.FullName -match 'UnityEngine.Object::get_name'})){throw 'Rule lookup no longer matches definition names'}
$fs20=if($Plugin){@(Get-Item -LiteralPath $Plugin)}else{@(Get-ChildItem -LiteralPath $plugins -Filter 'VortexConventional.dll' -File -Recurse)}
if($fs20.Count -ne 1){throw 'Expected one FS-20 plugin'}
$fs20Assembly=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($fs20[0].FullName)
$familyPluginType=$fs20Assembly.MainModule.Types|Where-Object Name -eq VortexConventionalPlugin
$method=$familyPluginType.Methods|Where-Object Name -eq AddToHangar
$fieldsUsed=@($method.Body.Instructions|Where-Object {$_.Operand -is [Mono.Cecil.FieldReference]}|ForEach-Object {$_.Operand.Name})
if('original' -notin $fieldsUsed -or $familyField -notin $fieldsUsed -or 'HangarTypes' -notin $fieldsUsed){throw 'The installed family plugin no longer adds this variant alongside B'}
$game=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GamePath 'NuclearOption_Data/Managed/Assembly-CSharp.dll'))
$hangar=$game.MainModule.Types|Where-Object Name -eq Hangar
$canSpawn=$hangar.Methods|Where-Object Name -eq CanSpawnAircraft
$calls=@($canSpawn.Body.Instructions|Where-Object {$_.Operand -is [Mono.Cecil.MethodReference]}|ForEach-Object {$_.Operand.FullName})
if(!($calls|Where-Object {$_ -match 'Hangar::get_Available'}) -or !($calls|Where-Object {$_ -match 'Contains'})){throw 'Native spawn availability contract changed'}
$version=($familyPluginType.CustomAttributes|Where-Object {$_.AttributeType.Name -eq 'BepInPlugin'}).ConstructorArguments[2].Value
$installedPath=Join-Path $module[0].DirectoryName ('FS20'+$AircraftVariant+'.catapults')
$installedRulePresent=Test-Path -LiteralPath $installedPath
$installedHash=if($installedRulePresent){(Get-FileHash -LiteralPath $installedPath).Hash.ToLowerInvariant()}else{$null}
$expectedHash=(Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant()
$report=[ordered]@{pass=$true;scope='Static inspection of the installed native rule, loader and family plugin; no game code executed';fs20Version=$version;catapultModule=$module[0].FullName;catapultHash=(Get-FileHash $module[0].FullName).Hash.ToLowerInvariant();ruleSha256=(Get-FileHash $path).Hash.ToLowerInvariant();definitionKey=$fields[0];clutchParent=$fields[1];clutchOffset=$fields[2];detectionRadius=[double]::Parse($fields[3],[Globalization.CultureInfo]::InvariantCulture);releaseSpeedMps=90;accelerationCapG=5;throttleDelaySeconds=0.5;nativeFs20BRuleUnchanged=$true;gameLaunched=$false;liveSelectionTested=$false;launchTested=$false}
$report.scope='Static source-rule/loader/family contracts plus on-disk installation status; no game code executed'
$report.installedRulePath=$installedPath
$report.installedRulePresent=$installedRulePresent
$report.installedRuleSha256=$installedHash
$report.installationReady=($installedRulePresent -and $installedHash -eq $expectedHash)
$report.requireInstalledRule=[bool]$RequireInstalledRule
if($RequireInstalledRule -and !$report.installationReady){$report.pass=$false}
New-Item -ItemType Directory -Path (Split-Path -Parent $ReportPath) -Force|Out-Null
$report|ConvertTo-Json|Set-Content -LiteralPath $ReportPath -Encoding utf8
if(!$report.pass){throw ('External '+$AircraftVariant+' catapult rule is missing or differs from the verified source: '+$installedPath)}
Write-Output ('FS20'+$AircraftVariant+'_PENUMBRA_CONTRACT_PASS family='+$version+' installedRuleReady='+$report.installationReady)
