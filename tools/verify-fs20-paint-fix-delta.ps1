param([Parameter(Mandatory)][string]$Baseline,[Parameter(Mandatory)][string]$Candidate,[Parameter(Mandatory)][string]$Output)
$ErrorActionPreference='Stop'
Add-Type -LiteralPath 'C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option/BepInEx/core/Mono.Cecil.dll'
function Body($t){@($t.Fields|ForEach-Object {$_.FullName;if($_.HasConstant){$_.Constant}};$t.Methods|ForEach-Object {$_.FullName;if($_.HasBody){$_.Body.Instructions|ForEach-Object ToString}};$t.NestedTypes|ForEach-Object {Body $_})-join"`n"}
$a=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($Baseline);$b=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($Candidate)
try{
 if($a.MainModule.Types.Count-ne$b.MainModule.Types.Count){throw 'Unexpected type count'}
 $changed=@();foreach($t in $a.MainModule.Types){$n=$b.MainModule.Types|Where-Object FullName -eq $t.FullName;if(!$n){throw 'Removed type'};if((Body $t)-cne(Body $n)){$changed+=$t.Name}}
 if((($changed|Sort-Object)-join',')-ne'Fs20ExteriorPaint,Fs20PreparedLiveries'){throw ('Unexpected code changes: '+($changed-join','))}
 if($a.MainModule.Resources.Count-ne$b.MainModule.Resources.Count){throw 'Unexpected resource count'}
 $assets=@();foreach($r in $a.MainModule.Resources){$n=$b.MainModule.Resources|Where-Object Name -eq $r.Name;if(!$n){throw 'Removed resource'};if([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($r.GetResourceData()))-ne[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($n.GetResourceData()))){$assets+=$r.Name}}
 if((($assets|Sort-Object)-join',')-ne'Fs20PreparedPaints.bundle,Fs20T.bin'){throw 'Unexpected resource delta'}
 @{passed=$true;baselineSha256=(Get-FileHash $Baseline).Hash;candidateSha256=(Get-FileHash $Candidate).Hash;changedTypes=$changed;changedResources=$assets;otherResourcesIdentical=13;flightAiDamageNetworkCodeIdentical=$true}|ConvertTo-Json|Set-Content -LiteralPath $Output
 'FS20_PAINT_FIX_DELTA_PASS'
}finally{$a.Dispose();$b.Dispose()}
