param(
    [string]$GamePath = $env:NUCLEAR_OPTION_PATH,
    [string]$CompilerPath
)
$ErrorActionPreference = 'Stop'
if (-not $GamePath) { throw 'Pass -GamePath or set NUCLEAR_OPTION_PATH to your licensed game installation.' }
$repository = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not $CompilerPath) {
    & (Join-Path $repository 'restore-compiler.ps1')
    $CompilerPath = Join-Path $repository '.deps/compiler/tasks/net472/csc.exe'
}
$managed = Join-Path $GamePath 'NuclearOption_Data/Managed'
$core = Join-Path $GamePath 'BepInEx/core'
$output = Join-Path $repository 'build/manual-tailhook'
$plugin = Join-Path $output 'BepInEx/plugins/ManualTailHook'
New-Item -ItemType Directory -Path $plugin -Force | Out-Null
$refs = @('mscorlib.dll','netstandard.dll','System.dll','System.Core.dll','Assembly-CSharp.dll',
    'UnityEngine.dll','UnityEngine.CoreModule.dll','UnityEngine.InputLegacyModule.dll',
    'UnityEngine.UI.dll','Unity.TextMeshPro.dll','Mirage.dll') | ForEach-Object { Join-Path $managed $_ }
$refs += @('BepInEx.dll','0Harmony.dll') | ForEach-Object { Join-Path $core $_ }
$compilerArgs = @('/nologo','/target:library','/langversion:latest','/nostdlib+','/deterministic+',
    ('/out:' + (Join-Path $plugin 'ManualTailHook.dll')))
$compilerArgs += $refs | ForEach-Object { '/reference:' + $_ }
$compilerArgs += Join-Path $PSScriptRoot 'src/ManualTailHookPlugin.cs'
& $CompilerPath @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Manual tailhook compilation failed' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'meta.json') -Destination $plugin -Force
foreach ($name in @('README.txt','LICENSE','AI_ASSISTANCE.md','THIRD_PARTY_NOTICES.md')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $output -Force
}
Compress-Archive -Path (Join-Path $output '*') -DestinationPath (Join-Path $repository 'build/NO-Manual-Tailhook-0.1.1.zip') -Force
Get-FileHash -LiteralPath (Join-Path $plugin 'ManualTailHook.dll') -Algorithm SHA256
