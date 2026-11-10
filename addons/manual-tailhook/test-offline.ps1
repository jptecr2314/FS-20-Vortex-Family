param([string]$GamePath = $env:NUCLEAR_OPTION_PATH, [string]$CompilerPath)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$repository = Split-Path -Parent (Split-Path -Parent $root)
if (-not $GamePath) { throw 'Pass -GamePath or set NUCLEAR_OPTION_PATH.' }
if (-not $CompilerPath) { & (Join-Path $repository 'restore-compiler.ps1'); $CompilerPath = Join-Path $repository '.deps/compiler/tasks/net472/csc.exe' }
$out = Join-Path $repository 'build/manual-tailhook-tests'
New-Item -ItemType Directory -Path $out -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $GamePath 'BepInEx\core') -Filter '*.dll' | Copy-Item -Destination $out -Force
$compilerArgs = @('/nologo','/target:exe','/langversion:latest','/optimize-',
    ('/out:' + (Join-Path $out 'ManualTailHookHarness.exe')),
    ('/reference:' + (Join-Path $out '0Harmony.dll')),
    (Join-Path $root 'src\ManualTailHookPlugin.cs'), (Join-Path $root 'tests/ManualTailHookHarness.cs'))
& $CompilerPath @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Harness compilation failed' }
& (Join-Path $out 'ManualTailHookHarness.exe')
if ($LASTEXITCODE -ne 0) { throw 'Harness failed' }

# Check actual installed API contracts independently of the simulated objects.
Add-Type -LiteralPath (Join-Path $GamePath 'BepInEx\core\Mono.Cecil.dll')
$gameAssemblyPath = Join-Path $GamePath 'NuclearOption_Data\Managed\Assembly-CSharp.dll'
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($gameAssemblyPath)
try {
    $hook = $assembly.MainModule.Types | Where-Object Name -eq 'TailHook'
    $wire = $assembly.MainModule.Types | Where-Object Name -eq 'ArrestorGear'
    foreach ($fieldName in @('deployed','hooked','aircraft','unitPart')) {
        if (-not ($hook.Fields | Where-Object Name -eq $fieldName)) { throw "Missing TailHook.$fieldName" }
    }
    if (-not ($wire.Fields | Where-Object Name -eq 'attachedHook')) { throw 'Missing arrestor attachment field' }
    foreach ($methodName in @('CheckDeployConditions','FixedUpdate','Unhook')) {
        if (-not ($hook.Methods | Where-Object { $_.Name -eq $methodName -and $_.Parameters.Count -eq 0 })) { throw "Missing TailHook.$methodName" }
    }
    $catch = $wire.Methods | Where-Object Name -eq 'Hook'
    if ($catch.ReturnType.FullName -ne 'System.Boolean' -or $catch.Parameters[0].ParameterType.Name -ne 'TailHook') { throw 'Arrestor Hook signature changed' }
    $release = $wire.Methods | Where-Object Name -eq 'Unhook'
    if (-not $release.IsPublic) { throw 'Arrestor Unhook no longer public' }
    $releaseIL = @($release.Body.Instructions | ForEach-Object ToString) -join "`n"
    if ($releaseIL -notmatch 'callvirt System.Void TailHook::Unhook' -or $releaseIL -notmatch 'stfld TailHook ArrestorGear::attachedHook') { throw 'Native release no longer clears both attachment sides' }
    $forceIL = @($wire.Methods | Where-Object Name -eq 'FixedUpdate' | ForEach-Object { $_.Body.Instructions | ForEach-Object ToString }) -join "`n"
    if ($forceIL -notmatch 'TailHook::ApplyForce') { throw 'Native arrestor force contract changed' }
    Write-Output 'PASS: installed native hook, animation, catch and two-sided release API contracts.'
} finally { $assembly.Dispose() }
