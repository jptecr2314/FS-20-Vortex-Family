$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$folder=Join-Path $root '.deps/compiler'
$expected='37333f4f1e2ce55e621355d6da651dc23d4cb5f94a8f76b9478816e87f110ad9'
if(Test-Path (Join-Path $folder 'tasks/net472/csc.exe')){return}
New-Item -ItemType Directory -Path (Join-Path $root '.deps') -Force|Out-Null
$zip=Join-Path $root '.deps/compiler-4.8.0.zip'
Invoke-WebRequest 'https://api.nuget.org/v3-flatcontainer/microsoft.net.compilers.toolset/4.8.0/microsoft.net.compilers.toolset.4.8.0.nupkg' -OutFile $zip
if((Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected){throw 'Compiler package hash mismatch'}
Expand-Archive -LiteralPath $zip -DestinationPath $folder
