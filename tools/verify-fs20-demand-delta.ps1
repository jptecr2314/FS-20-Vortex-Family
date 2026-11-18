param([Parameter(Mandatory)][string]$Baseline,[Parameter(Mandatory)][string]$Candidate,[Parameter(Mandatory)][string]$Output)
$ErrorActionPreference='Stop'
Add-Type -LiteralPath 'C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option/BepInEx/core/Mono.Cecil.dll'
function Body($t){@($t.Fields|ForEach-Object {$_.FullName;if($_.HasConstant){$_.Constant}};$t.Methods|ForEach-Object {$_.FullName;if($_.HasBody){$_.Body.Instructions|ForEach-Object ToString}};$t.NestedTypes|ForEach-Object {Body $_})-join"`n"}
$a=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($Baseline);$b=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($Candidate)
try{
 $changed=@();foreach($t in $a.MainModule.Types){$n=$b.MainModule.Types|Where-Object FullName -eq $t.FullName;if(!$n){throw 'Removed type'};if((Body $t)-cne(Body $n)){$changed+=$t.Name}}
 if((($changed|Sort-Object)-join',')-ne'Fs20AirframeA,Fs20NAAirframe,Fs20NetworkSignature,Fs20PreparedLiveries,Fs20TAirframe,VortexConventionalPlugin'){throw ('Unexpected code changes: '+($changed-join','))}
 $added=@($b.MainModule.Types|Where-Object {$_.FullName-notin$a.MainModule.Types.FullName}|ForEach-Object Name)
 if((($added|Where-Object {$_-notlike'<>f__AnonymousType*'}|Sort-Object)-join',')-ne'Fs20LodPaintBinding,Fs20PaintCache,Fs20PaintLease,Fs20PaintLods'){throw 'Unexpected added types'}
 $equal=@();foreach($r in $a.MainModule.Resources|Where-Object Name -ne Fs20PreparedPaints.bundle){$n=$b.MainModule.Resources|Where-Object Name -eq $r.Name;if(!$n){throw 'Removed aircraft resource'};if([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($r.GetResourceData()))-ne[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($n.GetResourceData()))){throw ('Changed aircraft resource: '+$r.Name)};$equal+=$r.Name}
 if($a.MainModule.Resources.Count-ne$b.MainModule.Resources.Count-or!($b.MainModule.Resources|Where-Object Name -eq Fs20PaintCatalog.json)){throw 'Unexpected resource contract'}
 @{passed=$true;baselineSha256=(Get-FileHash $Baseline).Hash;candidateSha256=(Get-FileHash $Candidate).Hash;changedTypes=$changed;addedTypes=$added;identicalResources=$equal;replacedResource=@('Fs20PreparedPaints.bundle','Fs20PaintCatalog.json');flightAiPhysicsDamageAndGeometryIdentical=$true}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath $Output -Encoding UTF8
 'FS20_DEMAND_DELTA_PASS'
}finally{$a.Dispose();$b.Dispose()}
