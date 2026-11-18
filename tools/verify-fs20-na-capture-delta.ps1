param(
 [string]$Baseline='D:/Artifacts/fs20-ai-control/2026-11-17/candidate-source/build/BepInEx/plugins/VortexConventional/VortexConventional.dll',
 [Parameter(Mandatory)][string]$Candidate,
 [Parameter(Mandatory)][string]$Output
)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Output){throw 'Preserve previous verification.'}
Add-Type -LiteralPath 'C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option/BepInEx/core/Mono.Cecil.dll'
function Body($t){@($t.Fields|ForEach-Object {$_.FullName;if($_.HasConstant){$_.Constant}};$t.Methods|ForEach-Object {$_.FullName;if($_.HasBody){$_.Body.Instructions|ForEach-Object ToString}};$t.NestedTypes|ForEach-Object {Body $_})-join"`n"}
function Code($m){($m.Body.Instructions|ForEach-Object ToString)-join"`n"}
function ResourceHash($r){$s=$r.GetResourceStream();$h=[Security.Cryptography.SHA256]::Create();try{[Convert]::ToHexString($h.ComputeHash($s))}finally{$s.Dispose();$h.Dispose()}}
$old=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($Baseline);$new=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($Candidate)
try{
 $changed=@();foreach($t in $old.MainModule.Types){$n=$new.MainModule.Types|Where-Object FullName -eq $t.FullName;if(!$n){throw 'Removed baseline type.'};if((Body $t)-cne(Body $n)){$changed+=$t.Name}}
 if((($changed|Sort-Object)-join',')-ne'Fs20AiGuidance,VortexConventionalPlugin'){throw ('Unexpected production delta: '+($changed-join','))}
 $added=@($new.MainModule.Types|Where-Object {$_.FullName-notin$old.MainModule.Types.FullName})
 if($added.Count-ne1-or$added[0].Name-ne'Fs20NAAimPolicy'){throw 'Unexpected added type.'}
 $g0=$old.MainModule.Types|Where-Object Name -eq Fs20AiGuidance;$g1=$new.MainModule.Types|Where-Object Name -eq Fs20AiGuidance
 foreach($name in @('Gains','YawGain')){if((Code ($g0.Methods|Where-Object Name -eq $name))-cne(Code ($g1.Methods|Where-Object Name -eq $name))){throw ('Baseline axis calibration changed: '+$name)}}
 $e0=$old.MainModule.Types|Where-Object Name -eq VortexConventionalPlugin;$e1=$new.MainModule.Types|Where-Object Name -eq VortexConventionalPlugin
 $entryChanges=@();foreach($m in $e0.Methods|Where-Object HasBody){$n=$e1.Methods|Where-Object FullName -eq $m.FullName;if((Code $m)-cne(Code $n)){$entryChanges+=$m.Name}}
 if(($entryChanges-join',')-ne'Awake'){throw 'Unexpected entrypoint change.'}
 $registration=@(($e1.Methods|Where-Object Name -eq Awake).Body.Instructions|Where-Object {$_.OpCode.Code.ToString()-eq'Ldtoken'-and$_.Operand.FullName-like'*Fs20AiGuidance/GunPoint'})
 if($registration.Count-ne1){throw 'Native gun steering-point observer registration missing.'}
 if($new.MainModule.Resources.Count-ne$old.MainModule.Resources.Count){throw 'Resource count changed.'}
 foreach($r in $old.MainModule.Resources){$n=$new.MainModule.Resources|Where-Object Name -eq $r.Name;if(!$n-or(ResourceHash $r)-ne(ResourceHash $n)){throw ('Changed asset '+$r.Name)}}
 foreach($name in @('Step','Refine')){$method=$added[0].Methods|Where-Object Name -eq $name;if(@($method.Body.Instructions|Where-Object {$_.OpCode.Code.ToString()-in@('Newarr','Newobj')}).Count){throw ('Per-tick policy allocation in '+$name)}}
 @{passed=$true;baselineSha256=(Get-FileHash $Baseline).Hash;candidateSha256=(Get-FileHash $Candidate).Hash;changedTypes=$changed;addedType=$added.Name;preservedResources=$new.MainModule.Resources.Count;baselineAxisGainsIdentical=$true;profilesPhysicsDamageAndManualCodeIdentical=$true;allocationFreeScalarPolicy=$true;scope='NA gun capture, relative-rate feedback and energy-safe closure ceiling only. A/C/T/B profiles and all existing physical/manual controllers unchanged; one native gun-point observer registration.'}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath $Output
 'FS20_NA_CAPTURE_DELTA_PASS'
}finally{$old.Dispose();$new.Dispose()}
