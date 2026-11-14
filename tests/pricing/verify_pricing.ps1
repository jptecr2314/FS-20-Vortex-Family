param([string]$Stage='D:/Artifacts/fs20-multiplayer/0.6.9/pricing')
$ErrorActionPreference='Stop'
$build=Get-Content ($Stage+'/BUILD.json') -Raw|ConvertFrom-Json
$g='C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option'
Add-Type -LiteralPath ($g+'/BepInEx/core/Mono.Cecil.dll')
$old=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($build.baselineDll)
$new=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($build.dll)
function Body($t){@($t.Fields|ForEach-Object {$_.FullName;if($_.HasConstant){$_.Constant};[Convert]::ToBase64String($_.InitialValue)};$t.Methods|ForEach-Object {$_.FullName;if($_.HasBody){$_.Body.Instructions|ForEach-Object ToString}};$t.NestedTypes|ForEach-Object {Body $_})-join "`n"}
function RH($r){$s=$r.GetResourceStream();try{[Convert]::ToBase64String([Security.Cryptography.SHA256]::Create().ComputeHash($s))}finally{$s.Dispose()}}
try{
 $changed=@();foreach($t in $old.MainModule.Types){$n=$new.MainModule.Types|Where-Object FullName -eq $t.FullName;if(!$n){throw ('Removed type '+$t.Name)};if((Body $t)-cne(Body $n)){$changed+=$t.Name}}
 $allowed=@('Fs20AirframeA','Fs20TAirframe','Fs20NAAirframe','VortexConventionalPlugin','SharedAirframeStock','AirframePool')
 if((($changed|Sort-Object)-join ',') -ne (($allowed|Sort-Object)-join ',')){throw ('Unexpected change: '+($changed -join ','))}
 $added=@($new.MainModule.Types|Where-Object {$_.FullName -notin $old.MainModule.Types.FullName})
 if($added.Count -ne 1 -or $added[0].Name -ne 'Fs20VariantPricing'){throw 'Unexpected new types'}
 foreach($r in $old.MainModule.Resources){$n=$new.MainModule.Resources|Where-Object Name -eq $r.Name;if(!$n -or (RH $r)-cne(RH $n)){throw ('Changed resource '+$r.Name)}}
 if($old.MainModule.Resources.Count -ne $new.MainModule.Resources.Count){throw 'Resource count changed'}
 $wiring=@();foreach($tn in @('Fs20AirframeA','Fs20TAirframe','Fs20NAAirframe','VortexConventionalPlugin')){
  $t=$new.MainModule.Types|Where-Object Name -eq $tn
  $calls=@($t.Methods|Where-Object HasBody|ForEach-Object {$_.Body.Instructions}|Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.FullName -match 'Fs20VariantPricing::Apply'})
  $expected=if($tn -eq 'VortexConventionalPlugin'){2}else{1};if($calls.Count -ne $expected){throw ('Missing factory price wiring: '+$tn)};$wiring+=[pscustomobject]@{type=$tn;calls=$calls.Count}
 }
 $stock=$new.MainModule.Types|Where-Object Name -eq SharedAirframeStock
 $enable=($stock.Methods|Where-Object Name -eq Enable).Body.Instructions
 if(@($enable|Where-Object {$_.Operand -is [Mono.Cecil.TypeReference] -and $_.Operand.Name -eq 'FamilyMatches'}).Count){throw 'Cross-variant ownership transpiler still installed'}
 # Supply readers/debit and all multiplayer hooks must retain their original bodies.
 foreach($name in @('SupplyReads','SupplyDebit')){
  $o=($old.MainModule.Types|Where-Object Name -eq SharedAirframeStock).NestedTypes|Where-Object Name -eq $name
  $n=$stock.NestedTypes|Where-Object Name -eq $name
  if((Body $o)-cne(Body $n)){throw ('Faction-supply behavior changed: '+$name)}
 }
 $native=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($g+'/NuclearOption_Data/Managed/Assembly-CSharp.dll')
 try{$player=$native.MainModule.Types|Where-Object FullName -eq 'NuclearOption.Networking.Player'
  foreach($method in @('UserCode_CmdPurchaseAirframe_787674834','UserCode_CmdSellAirframe_1853434019')){
   $m=$player.Methods|Where-Object Name -eq $method
   if(!@($m.Body.Instructions|Where-Object {$_.Operand -is [Mono.Cecil.FieldReference] -and $_.Operand.FullName -eq 'System.Single UnitDefinition::value'}).Count){throw 'Native price contract changed'}
  }
 }finally{$native.Dispose()}
 [ordered]@{passed=$true;candidateSha256=(Get-FileHash $build.dll).Hash.ToLowerInvariant();baselineSha256=$build.baselineSha256;changedTypes=$changed;addedType='Fs20VariantPricing';factoryWiring=$wiring;resourcesPreserved=$old.MainModule.Resources.Count;multiplayerUnchanged=$true;flightGeometryAndDamageUnchanged=$true;factionSupplyUnchanged=$true;crossVariantOwnershipDisabled=$true;nativePriceContractChecked=$true;installed=$false}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath ($Stage+'/scope.json') -Encoding utf8
 'FS20_PRICING_SCOPE_PASS: native economy wiring, exact owned variants, faction supply and physical/network paths preserved.'
}finally{$old.Dispose();$new.Dispose()}
