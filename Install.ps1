param([Parameter(Mandatory=$true)][string]$GamePath,[string]$PackageRoot=$PSScriptRoot)
$ErrorActionPreference='Stop'
$game=[IO.Path]::GetFullPath($GamePath);$plugins=Join-Path $game 'BepInEx/plugins'
if(!(Test-Path -LiteralPath $plugins)){throw 'BepInEx/plugins was not found in GamePath.'}
$source=Join-Path $PackageRoot 'BepInEx/plugins/VortexConventional'
if(!(Test-Path -LiteralPath (Join-Path $source 'VortexConventional.dll'))){$source=Join-Path $PackageRoot 'build/BepInEx/plugins/VortexConventional'}
if(!(Test-Path -LiteralPath (Join-Path $source 'VortexConventional.dll'))){throw 'Build or extract the complete package first.'}
foreach($process in Get-Process -Name NuclearOption -ErrorAction SilentlyContinue){
 if($process.Path-and[IO.Path]::GetDirectoryName($process.Path)-eq$game){throw 'Close Nuclear Option before installing. The installer will not close it.'}
}
$catalog=Get-Content -LiteralPath (Join-Path $source 'BuiltinPaints/catalog.json') -Raw|ConvertFrom-Json
if($catalog.schemaVersion-ne2-or$catalog.paints.Count-ne12){throw 'Incomplete built-in paint catalog.'}
$bundles=@(@{file=$catalog.basisFile;hash=$catalog.basisSha256})+@($catalog.paints|ForEach-Object {@{file=$_.bundleFile;hash=$_.bundleSha256}})
foreach($bundle in $bundles){
 if([string]::IsNullOrWhiteSpace($bundle.file)-or[IO.Path]::GetFileName($bundle.file)-ne$bundle.file-or$bundle.file.Contains('..')){throw 'Invalid paint bundle path.'}
 $path=Join-Path $source ('BuiltinPaints/'+$bundle.file)
 if(!(Test-Path -LiteralPath $path)-or(Get-FileHash -LiteralPath $path).Hash-ne$bundle.hash){throw ('Missing or corrupt paint bundle: '+$bundle.file)}
}
$relative=@(Get-ChildItem -LiteralPath $source -File|ForEach-Object Name)+@('BuiltinPaints/catalog.json')+@($bundles|ForEach-Object {'BuiltinPaints/'+$_.file})
$relative=@($relative|Select-Object -Unique);$target=Join-Path $plugins 'VortexConventional'
if([IO.Path]::GetFullPath($source)-eq[IO.Path]::GetFullPath($target)){throw 'Package source must be outside the installed plugin directory.'}
$legacy=@(Get-ChildItem -LiteralPath $plugins -Filter Fs20SharedAirframes.dll -File -Recurse)
foreach($path in (@($relative|ForEach-Object {Join-Path $target $_})+@($legacy|ForEach-Object FullName))){
 if(Test-Path -LiteralPath $path){$handle=[IO.File]::Open($path,'Open','ReadWrite','None');$handle.Dispose()}
}
$backup=Join-Path $game ('BepInEx/fs20-backups/'+[Guid]::NewGuid().ToString('N'))
$stage=Join-Path $backup 'incoming';$previous=Join-Path $backup 'previous'
New-Item -ItemType Directory -Path $stage,$previous -Force|Out-Null
$files=@();$moved=@();$written=@()
foreach($name in $relative){
 $incoming=Join-Path $source $name;$staged=Join-Path $stage $name;$installed=Join-Path $target $name;$saved=Join-Path $previous $name
 New-Item -ItemType Directory -Path (Split-Path $staged) -Force|Out-Null;Copy-Item -LiteralPath $incoming -Destination $staged
 $existed=Test-Path -LiteralPath $installed
 if($existed){New-Item -ItemType Directory -Path (Split-Path $saved) -Force|Out-Null;Copy-Item -LiteralPath $installed -Destination $saved}
 $files+=@{relative=$name;installed=$installed;saved=$saved;staged=$staged;existed=$existed;sha256=(Get-FileHash -LiteralPath $staged).Hash}
}
try{
 foreach($file in $legacy){
  $full=[IO.Path]::GetFullPath($file.FullName)
  if(!$full.StartsWith([IO.Path]::GetFullPath($plugins)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Legacy DLL escaped the plugin directory.'}
  $saved=Join-Path $backup ('legacy-'+$moved.Count+'.dll');Move-Item -LiteralPath $full -Destination $saved;$moved+=@{Original=$full;Backup=$saved}
 }
 foreach($file in $files){
  New-Item -ItemType Directory -Path (Split-Path $file.installed) -Force|Out-Null
  $written+=,$file;Copy-Item -LiteralPath $file.staged -Destination $file.installed -Force
  if((Get-FileHash -LiteralPath $file.installed).Hash-ne$file.sha256){throw ('Installed hash differs: '+$file.relative)}
 }
}catch{
 foreach($file in $written){if($file.existed){Copy-Item -LiteralPath $file.saved -Destination $file.installed -Force}elseif(Test-Path -LiteralPath $file.installed){Remove-Item -LiteralPath $file.installed}}
 foreach($file in $moved){Move-Item -LiteralPath $file.Backup -Destination $file.Original -Force};throw
}
$receipt=[pscustomobject]@{Plugin='FS-20 Vortex Family';InstalledDll=(Join-Path $target 'VortexConventional.dll');RemovedLegacyDlls=$moved.Count;Backup=$backup;Files=@($files|ForEach-Object {@{path=$_.relative;sha256=$_.sha256}})}
$receipt|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $backup 'installation.json') -Encoding UTF8
$receipt
