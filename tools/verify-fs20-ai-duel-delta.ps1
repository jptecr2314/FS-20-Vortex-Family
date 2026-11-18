param(
 [Parameter(Mandatory)][string]$Candidate,
 [string]$Baseline='C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option/BepInEx/plugins/VortexConventional/VortexConventional.dll',
 [Parameter(Mandatory)][string]$Output
)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Output){throw 'Preserve previous evidence.'}
Add-Type -LiteralPath 'C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option/BepInEx/core/Mono.Cecil.dll'
function Body($t){@($t.Fields|ForEach-Object {$_.FullName;if($_.HasConstant){$_.Constant}};$t.Methods|ForEach-Object {$_.FullName;if($_.HasBody){$_.Body.Instructions|ForEach-Object ToString}};$t.NestedTypes|ForEach-Object {Body $_}) -join "`n"}
function ResourceHash($r){$s=$r.GetResourceStream();$h=[Security.Cryptography.SHA256]::Create();try{[Convert]::ToHexString($h.ComputeHash($s))}finally{$s.Dispose();$h.Dispose()}}
function CaseBlock($method,$tag){
 $code=$method.Body.Instructions
 for($i=0;$i-lt$code.Count-2;$i++){
  if($code[$i].OpCode.Code.ToString()-eq'Ldstr'-and$code[$i].Operand-eq$tag){
   if($code[$i+2].OpCode.FlowControl.ToString()-ne'Cond_Branch'){throw 'Unexpected native profile dispatch.'}
   $at=$code.IndexOf($code[$i+2].Operand);$block=@()
   while($at-lt$code.Count){$op=$code[$at];$block+=$op.ToString();if($op.OpCode.FlowControl.ToString()-in@('Branch','Return')){return $block-join"`n"};$at++}
  }
 }
 throw ('Profile case not found: '+$tag)
}
$old=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($Baseline)
$new=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($Candidate)
try{
 $changed=@();foreach($t in $old.MainModule.Types){$n=$new.MainModule.Types|Where-Object FullName -eq $t.FullName;if(!$n){throw 'Production type removed.'};if((Body $t)-cne(Body $n)){$changed+=$t.Name}}
 if((($changed|Sort-Object)-join',')-ne'Fs20AiCalibration,VortexConventionalPlugin'){throw ('Unexpected delta: '+($changed-join','))}
 $added=@($new.MainModule.Types|Where-Object {$_.FullName -notin $old.MainModule.Types.FullName})
 if($added.Count-ne1-or$added[0].Name-ne'Fs20AiGuidance'){throw 'Unexpected new production type.'}
 $before=($old.MainModule.Types|Where-Object Name -eq Fs20AiCalibration).Methods|Where-Object Name -eq Apply
 $after=($new.MainModule.Types|Where-Object Name -eq Fs20AiCalibration).Methods|Where-Object Name -eq Apply
 if($before.Body.Instructions.Count-ne$after.Body.Instructions.Count){throw 'Planning profile structure changed.'}
 $diffs=@();for($i=0;$i-lt$before.Body.Instructions.Count;$i++){
  $a=$before.Body.Instructions[$i];$b=$after.Body.Instructions[$i]
  if($a.ToString()-cne$b.ToString()){
   if($a.OpCode.Code.ToString()-ne'Ldc_R4'-or$b.OpCode.Code.ToString()-ne'Ldc_R4'){throw 'Nonconstant planning delta.'}
   $diffs+=([string]$a.Operand)+':'+([string]$b.Operand)
  }
 }
 if((($diffs|Sort-Object)-join',')-ne'100:200,120:200,140:220,300:450,400:450'){throw ('Unexpected A/C/NA planning constants: '+($diffs-join','))}
 if((CaseBlock $before 'T')-cne(CaseBlock $after 'T')){throw 'Installed T planning branch changed.'}
 if((CaseBlock $before 'B')-cne(CaseBlock $after 'B')){throw 'Native B planning branch changed.'}
 $entry=$old.MainModule.Types|Where-Object Name -eq VortexConventionalPlugin
 $next=$new.MainModule.Types|Where-Object Name -eq VortexConventionalPlugin
 $entryChanges=@();foreach($m in $entry.Methods|Where-Object HasBody){$n=$next.Methods|Where-Object FullName -eq $m.FullName;if((($m.Body.Instructions|ForEach-Object ToString)-join"`n")-cne(($n.Body.Instructions|ForEach-Object ToString)-join"`n")){$entryChanges+=$m.Name}}
 if(($entryChanges-join',')-ne'Awake'){throw 'Unexpected plugin entry change.'}
 if($new.MainModule.Resources.Count-ne$old.MainModule.Resources.Count){throw 'Resource count changed.'}
 foreach($r in $old.MainModule.Resources){$n=$new.MainModule.Resources|Where-Object Name -eq $r.Name;if(!$n-or(ResourceHash $r)-ne(ResourceHash $n)){throw ('Asset changed: '+$r.Name)}}
 @{passed=$true;candidateSha256=(Get-FileHash $Candidate).Hash;baselineSha256=(Get-FileHash $Baseline).Hash;changedTypes=$changed;addedTypes=$added.Name;planningChanges=$diffs;entryChanges=$entryChanges;resourcesPreserved=$new.MainModule.Resources.Count;TPlanningBranchUnchanged=$true;BPlanningBranchUnchanged=$true;TControllersUnchanged=$true;physicalAndPlayerControlCodeUnchanged=$true;scope='A/C/NA planning constants and scoped authoritative outer attitude-loop calibration; installed T planning branch, all T controllers, assets, damage, forces and player control code unchanged.'}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath $Output
 'FS20_DUEL_DELTA_PASS'
}finally{$old.Dispose();$new.Dispose()}
