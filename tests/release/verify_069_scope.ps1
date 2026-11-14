param([string]$Stage='D:/Artifacts/fs20-multiplayer/0.6.9')
$ErrorActionPreference='Stop'
Add-Type -LiteralPath 'C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option/BepInEx/core/Mono.Cecil.dll'
$build=Get-Content -LiteralPath ($Stage+'/BUILD.json') -Raw|ConvertFrom-Json
$baseline='D:/Artifacts/fs20-dynamic-takeoff/2026-11-14/candidate/VortexConventional.dll'
$na='D:/Artifacts/fs20na-model/revision-24/candidate-release/VortexConventional.dll'
$a=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($baseline)
$b=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($build.dll)
$c=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($na)
function Body($t){@($t.Fields|ForEach-Object {$_.FullName;if($_.HasConstant){$_.Constant};[Convert]::ToBase64String($_.InitialValue)};$t.Methods|ForEach-Object {$_.FullName;if($_.HasBody){$_.Body.Instructions|ForEach-Object ToString}};$t.NestedTypes|ForEach-Object {Body $_})-join "`n"}
function HashResource($r){$s=$r.GetResourceStream();try{[Convert]::ToBase64String([Security.Cryptography.SHA256]::Create().ComputeHash($s))}finally{$s.Dispose()}}
try{
 $changed=@();foreach($t in $a.MainModule.Types){$next=$b.MainModule.Types|Where-Object FullName -eq $t.FullName;if(!$next){throw ('Removed type '+$t.FullName)};if((Body $t)-cne(Body $next)){$changed+=$t.Name}}
 if((($changed|Sort-Object)-join ',') -ne 'Fs20SinglePlayer,VortexConventionalPlugin'){throw ('Unexpected family delta: '+($changed -join ','))}
 $added=@($b.MainModule.Types|Where-Object {$_.FullName -notin $a.MainModule.Types.FullName})
 if(@($added|Where-Object {$_.Name -notmatch '^Fs20(Compatibility|Multiplayer|Network|Surface)'}).Count){throw 'Unexpected added module'}
 foreach($r in $a.MainModule.Resources){$next=$b.MainModule.Resources|Where-Object Name -eq $r.Name;if(!$next -or (HashResource $r)-cne(HashResource $next)){throw ('Changed family resource '+$r.Name)}}
 if($a.MainModule.Resources.Count -ne $b.MainModule.Resources.Count){throw 'Unexpected resources'}
 $naCompared=@();foreach($t in $c.MainModule.Types|Where-Object {$_.Name -like 'Fs20NA*' -and $_.Name -ne 'Fs20NAAirframe'}){
  $next=$b.MainModule.Types|Where-Object FullName -eq $t.FullName
  if(!$next -or (Body $t)-cne(Body $next)){throw ('NA corrected implementation differs: '+$t.Name)}
  $naCompared+=$t.Name
 }
 $signature=($b.MainModule.Types|Where-Object Name -eq Fs20NetworkSignature)
 $signatureIL=Body $signature
 if($signatureIL -notmatch 'JP_FS20_NA' -or $signatureIL -notmatch '0.6.9'){throw 'NA/version absent from network signature'}
 $surfaceIL=Body ($b.MainModule.Types|Where-Object Name -eq Fs20NetworkSurfaces)
 if($surfaceIL -notmatch 'JP_FS20_NA'){throw 'NA absent from mechanism presentation'}
 $networkIL=Body ($b.MainModule.Types|Where-Object Name -eq Fs20Multiplayer)
 if($networkIL -notmatch 'Fs20NAYawController' -or $networkIL -notmatch '"Apply"'){throw 'NA yaw owner guard missing'}
 $version=($b.MainModule.Types|Where-Object Name -eq VortexConventionalPlugin).CustomAttributes|Where-Object {$_.AttributeType.Name -eq 'BepInPlugin'}
 if($version.ConstructorArguments[2].Value -ne '0.6.9'){throw 'Wrong plugin version'}
 $meta=Get-Content -LiteralPath ($build.source+'/meta.json') -Raw|ConvertFrom-Json
 if($meta.artifact.version -ne '0.6.9'){throw 'Wrong package metadata'}
 [ordered]@{passed=$true;version='0.6.9';candidateSha256=(Get-FileHash -LiteralPath $build.dll).Hash.ToLowerInvariant();changedBaseTypes=$changed;addedNetworkTypes=$added.Name;preservedResources=$a.MainModule.Resources.Count;correctedNaTypesIdentical=$naCompared;dynamicCalculatorUnchanged=$true;naCorrectionBaselineSha256=(Get-FileHash $na).Hash;familyBaselineSha256=(Get-FileHash $baseline).Hash;installed=$false;published=$false}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath ($Stage+'/scope.json') -Encoding utf8
 'FS20_069_SCOPE_PASS: family/dynamic physics unchanged, corrected NA implementation retained, isolated network integration.'
}finally{$a.Dispose();$b.Dispose();$c.Dispose()}
