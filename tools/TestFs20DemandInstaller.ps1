param([Parameter(Mandatory)][string]$Output,[Parameter(Mandatory)][string]$Source)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Output){throw 'Preserve earlier installer evidence.'}
$package=Join-Path $Output 'package';$input=Join-Path $package 'BepInEx/plugins/VortexConventional'
New-Item -ItemType Directory -Path "$input/BuiltinPaints" -Force|Out-Null
[IO.File]::WriteAllText("$input/VortexConventional.dll",'new-family-transaction-fixture')
$catalog=Get-Content -LiteralPath "$Source/assets/fs20-paints/BuiltinPaints/catalog.json" -Raw|ConvertFrom-Json
foreach($paint in $catalog.paints){[IO.File]::WriteAllText("$input/BuiltinPaints/$($paint.bundleFile)",$paint.id);$paint.bundleSha256=(Get-FileHash -LiteralPath "$input/BuiltinPaints/$($paint.bundleFile)").Hash}
[IO.File]::WriteAllText("$input/BuiltinPaints/$($catalog.basisFile)",'basis-fixture');$catalog.basisSha256=(Get-FileHash -LiteralPath "$input/BuiltinPaints/$($catalog.basisFile)").Hash
$catalog|ConvertTo-Json -Depth 7|Set-Content -LiteralPath "$input/BuiltinPaints/catalog.json" -Encoding UTF8
$game=Join-Path $Output 'mock-game';$plugins=Join-Path $game 'BepInEx/plugins';$family=Join-Path $plugins 'VortexConventional'
New-Item -ItemType Directory -Path "$family/BuiltinPaints","$family/Paints","$plugins/Other","$game/BepInEx/config","$plugins/legacy" -Force|Out-Null
[IO.File]::WriteAllText("$family/VortexConventional.dll",'old-family')
[IO.File]::WriteAllText("$family/BuiltinPaints/catalog.json",'old-catalog')
[IO.File]::WriteAllText("$family/Paints/custom.fs20paint",'user-custom')
[IO.File]::WriteAllText("$plugins/Other/keep.dll",'other-mod')
[IO.File]::WriteAllText("$plugins/legacy/Fs20SharedAirframes.dll",'legacy')
[IO.File]::WriteAllText("$game/BepInEx/config/fs20.cfg",'keep-settings')
function Inventory { $items=@{};Get-ChildItem -LiteralPath $game -File -Recurse|Where-Object {$_.FullName-notmatch'fs20-backups'}|ForEach-Object {$items[$_.FullName]=(Get-FileHash -LiteralPath $_.FullName).Hash};return $items }
function Equal($left,$right){if($left.Count-ne$right.Count){return $false};foreach($key in $left.Keys){if($left[$key]-ne$right[$key]){return $false}};return $true}
$before=Inventory;$checks=@()
$locked=[IO.File]::Open("$family/VortexConventional.dll",'Open','Read','None');$rejected=$false
try{& "$Source/Install.ps1" -GamePath $game -PackageRoot $package|Out-Null}catch{$rejected=$true}finally{$locked.Dispose()}
if(!$rejected-or!(Equal $before (Inventory))){throw 'Locked-file refusal changed installed files.'};$checks+='locked-file refusal preserved files'
$global:Fs20InstallerFaultOnce=$true
function Copy-Item {
 [CmdletBinding()]param([string]$LiteralPath,[string]$Destination,[switch]$Force)
 if($global:Fs20InstallerFaultOnce-and$Destination -match '[\\/]plugins[\\/]VortexConventional[\\/]BuiltinPaints[\\/]'-and$Destination.EndsWith('.bundle')){$global:Fs20InstallerFaultOnce=$false;throw 'Injected mid-install copy failure'}
 Microsoft.PowerShell.Management\Copy-Item @PSBoundParameters
}
$rejected=$false
try{& "$Source/Install.ps1" -GamePath $game -PackageRoot $package|Out-Null}catch{$rejected=$true;$_|Out-String|Set-Content -LiteralPath "$Output/injected-failure.txt"}
Remove-Item Function:\Copy-Item;Remove-Variable Fs20InstallerFaultOnce -Scope Global
if(!$rejected-or!(Equal $before (Inventory))){throw 'Rollback did not restore the complete original file inventory.'};$checks+='mid-install failure restored DLL, catalog, new files and legacy plugin'
$receipt=& "$Source/Install.ps1" -GamePath $game -PackageRoot $package
if($receipt.RemovedLegacyDlls-ne1){throw 'Legacy migration failed.'}
foreach($f in $receipt.Files){if((Get-FileHash -LiteralPath (Join-Path $family $f.path)).Hash-ne$f.sha256){throw 'Installed payload mismatch.'}}
foreach($key in $before.Keys|Where-Object {$_-match'custom.fs20paint|keep.dll|fs20.cfg'}){if((Get-FileHash -LiteralPath $key).Hash-ne$before[$key]){throw 'Unrelated file changed.'}}
$checks+='complete DLL and 13 bundles installed with backup; custom paint, other mod and configuration preserved'
$after=Inventory;$again=& "$Source/Install.ps1" -GamePath $game -PackageRoot $package
if(!(Equal $after (Inventory))){throw 'Reinstallation changed the resulting files.'};$checks+='reinstallation is stable'
@{passed=$true;mockGameOnly=$true;checks=$checks;fileCount=$receipt.Files.Count;receipt=$receipt}|ConvertTo-Json -Depth 7|Set-Content -LiteralPath (Join-Path $Output 'installer.json') -Encoding UTF8
$checks
