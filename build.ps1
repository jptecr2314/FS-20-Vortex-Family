param(
 [string]$GamePath=$env:NUCLEAR_OPTION_PATH,
 [string]$VortexAssembly
)
$ErrorActionPreference='Stop'
if(!$GamePath){$GamePath='C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option'}
& (Join-Path $PSScriptRoot 'restore-compiler.ps1')
$spec=Get-Content (Join-Path $PSScriptRoot 'build-spec.json') -Raw|ConvertFrom-Json
$managed=Join-Path $GamePath 'NuclearOption_Data/Managed'
$core=Join-Path $GamePath 'BepInEx/core'
$out=Join-Path $PSScriptRoot ('build/BepInEx/plugins/'+$spec.folder)
New-Item -ItemType Directory -Path $out -Force|Out-Null
$references=$spec.managed|ForEach-Object {Join-Path $managed $_}
$references+=@('BepInEx.dll','0Harmony.dll')|ForEach-Object {Join-Path $core $_}
if($spec.parent){
 if(!$VortexAssembly){$VortexAssembly=Join-Path $GamePath 'BepInEx/plugins/VortexConventional/VortexConventional.dll'}
 $references+=$VortexAssembly
}
foreach($file in $references){if(!(Test-Path -LiteralPath $file)){throw "Required local dependency not found: $file"}}
$argsList=@('/nologo','/target:library','/langversion:latest','/nostdlib+','/deterministic+',('/pathmap:'+$PSScriptRoot+'=/_/src'),('/out:'+(Join-Path $out $spec.dll)))
$argsList+=$references|ForEach-Object {'/reference:'+$_}
$argsList+=$spec.sources|ForEach-Object {Join-Path $PSScriptRoot $_}
foreach($resource in $spec.resources){
 $file=Join-Path $PSScriptRoot $resource.path
 if(!(Test-Path -LiteralPath $file)){throw "Authored geometry resource missing: $file"}
 $argsList+='/resource:'+$file+','+$resource.name
}
& (Join-Path $PSScriptRoot '.deps/compiler/tasks/net472/csc.exe') @argsList
if($LASTEXITCODE -ne 0){throw 'Build failed'}
foreach($name in @('README.md','THIRD_PARTY_NOTICES.md','AI_ASSISTANCE.md','CHANGELOG.md','meta.json')){
 Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $out -Force
}
if(Test-Path (Join-Path $PSScriptRoot 'LICENSE')){Copy-Item (Join-Path $PSScriptRoot 'LICENSE') $out -Force}
Get-FileHash (Join-Path $out $spec.dll) -Algorithm SHA256
