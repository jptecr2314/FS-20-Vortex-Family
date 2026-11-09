param(
    [string]$GamePath=$env:NUCLEAR_OPTION_PATH,
    [string]$PluginAssembly,
    [string]$TestRoot=(Join-Path $PSScriptRoot 'build/tests/stock-option'),
    [string]$Compiler=(Join-Path $PSScriptRoot '.deps/compiler/tasks/net472/csc.exe')
)
$ErrorActionPreference='Stop'
if(!$GamePath){$GamePath='C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option'}
if(!$PluginAssembly){$PluginAssembly=Join-Path $PSScriptRoot 'build/BepInEx/plugins/VortexConventional/VortexConventional.dll'}
if(!(Test-Path -LiteralPath $Compiler)){& (Join-Path $PSScriptRoot 'restore-compiler.ps1')}
$source=Join-Path $PSScriptRoot 'tests/test_fs20_stock_config.cs'
if(!(Test-Path -LiteralPath $source)){$source=Join-Path $PSScriptRoot 'test_fs20_stock_config.cs'}
New-Item -ItemType Directory -Path $TestRoot -Force|Out-Null
$exe=Join-Path $TestRoot 'StockConfigChecks.exe'
& $Compiler /nologo /target:exe "/out:$exe" $source
if($LASTEXITCODE -ne 0){throw 'Stock config test compilation failed'}
& $exe $PluginAssembly $GamePath (Join-Path $TestRoot 'option.cfg')
if($LASTEXITCODE -ne 0){throw 'Stock config checks failed'}
$singlePlayer=Join-Path $PSScriptRoot 'tests/test_fs20_singleplayer.cs'
if(!(Test-Path -LiteralPath $singlePlayer)){$singlePlayer=Join-Path $PSScriptRoot 'test_fs20_singleplayer.cs'}
$gateExe=Join-Path $TestRoot 'SinglePlayerChecks.exe'
& $Compiler /nologo /target:exe "/out:$gateExe" $singlePlayer
if($LASTEXITCODE -ne 0){throw 'Single-player test compilation failed'}
& $gateExe $PluginAssembly $GamePath
if($LASTEXITCODE -ne 0){throw 'Single-player gate checks failed'}
