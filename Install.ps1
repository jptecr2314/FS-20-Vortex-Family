param(
    [Parameter(Mandatory=$true)][string]$GamePath,
    [string]$PackageRoot=$PSScriptRoot
)
$ErrorActionPreference='Stop'
$game=[IO.Path]::GetFullPath($GamePath)
$plugins=Join-Path $game 'BepInEx/plugins'
if(!(Test-Path -LiteralPath $plugins)){throw 'BepInEx/plugins was not found in GamePath.'}
$source=Join-Path $PackageRoot 'BepInEx/plugins/VortexConventional'
if(!(Test-Path -LiteralPath (Join-Path $source 'VortexConventional.dll'))){
    $source=Join-Path $PackageRoot 'build/BepInEx/plugins/VortexConventional'
}
if(!(Test-Path -LiteralPath (Join-Path $source 'VortexConventional.dll'))){throw 'Build or extract the package first.'}
foreach($process in Get-Process -Name NuclearOption -ErrorAction SilentlyContinue){
    if($process.Path -and [IO.Path]::GetDirectoryName($process.Path) -eq $game){throw 'Close Nuclear Option before installing. The installer will not close it.'}
}
$target=Join-Path $plugins 'VortexConventional'
$current=Join-Path $target 'VortexConventional.dll'
$legacy=@(Get-ChildItem -LiteralPath $plugins -Filter Fs20SharedAirframes.dll -File -Recurse)
$locked=@($current)+@($legacy|ForEach-Object FullName)
foreach($path in $locked){
    if(Test-Path -LiteralPath $path){
        $handle=[IO.File]::Open($path,'Open','ReadWrite','None')
        $handle.Dispose()
    }
}
$backup=Join-Path $game ('BepInEx/fs20-backups/'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $backup -Force|Out-Null
$hadCurrent=Test-Path -LiteralPath $current
if($hadCurrent){Copy-Item -LiteralPath $current -Destination (Join-Path $backup 'VortexConventional.dll')}
$moved=@()
try{
    foreach($file in $legacy){
        $full=[IO.Path]::GetFullPath($file.FullName)
        if(!$full.StartsWith([IO.Path]::GetFullPath($plugins)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Legacy DLL escaped the plugin directory.'}
        $saved=Join-Path $backup ('legacy-'+$moved.Count+'.dll')
        Move-Item -LiteralPath $full -Destination $saved
        $moved+=@{Original=$full;Backup=$saved}
    }
    New-Item -ItemType Directory -Path $target -Force|Out-Null
    Get-ChildItem -LiteralPath $source -File|ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $target -Force}
    if((Get-FileHash -LiteralPath $current).Hash -ne (Get-FileHash -LiteralPath (Join-Path $source 'VortexConventional.dll')).Hash){throw 'Installed DLL hash differs from the package.'}
}catch{
    if($hadCurrent){Copy-Item -LiteralPath (Join-Path $backup 'VortexConventional.dll') -Destination $current -Force}
    elseif(Test-Path -LiteralPath $current){Remove-Item -LiteralPath $current}
    foreach($file in $moved){Move-Item -LiteralPath $file.Backup -Destination $file.Original -Force}
    throw
}
[pscustomobject]@{Plugin='FS-20 Vortex Family';InstalledDll=$current;RemovedLegacyDlls=$moved.Count;Backup=$backup}
