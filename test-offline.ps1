$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'restore-compiler.ps1')
$out=Join-Path $PSScriptRoot 'build/tests'
New-Item -ItemType Directory -Path $out -Force|Out-Null
$csc=Join-Path $PSScriptRoot '.deps/compiler/tasks/net472/csc.exe'
$exe=Join-Path $out 'c-envelope.exe'
& $csc /nologo /target:exe "/out:$exe" (Join-Path $PSScriptRoot 'src/Fs20FlightEnvelope.cs') (Join-Path $PSScriptRoot 'tests/test_fs20_flight_envelope.cs')
if($LASTEXITCODE -ne 0){throw 'Test compilation failed'}
& $exe
if($LASTEXITCODE -ne 0){throw 'Offline test failed'}
$exe=Join-Path $out 'a-envelope.exe'
& $csc /nologo /target:exe "/out:$exe" (Join-Path $PSScriptRoot 'src/Fs20AEnergyEnvelope.cs') (Join-Path $PSScriptRoot 'src/Fs20AUpgradeSpec.cs') (Join-Path $PSScriptRoot 'tests/test_fs20a_energy_envelope.cs')
if($LASTEXITCODE -ne 0){throw 'Test compilation failed'}
& $exe
if($LASTEXITCODE -ne 0){throw 'Offline test failed'}
$exe=Join-Path $out 'vapor.exe'
& $csc /nologo /target:exe "/out:$exe" (Join-Path $PSScriptRoot 'src/Fs20VaporEnvelope.cs') (Join-Path $PSScriptRoot 'tests/test_fs20_vapor_envelope.cs')
if($LASTEXITCODE -ne 0){throw 'Test compilation failed'}
& $exe
if($LASTEXITCODE -ne 0){throw 'Offline test failed'}
$exe=Join-Path $out 'overlay.exe'
& $csc /nologo /target:exe "/out:$exe" (Join-Path $PSScriptRoot 'src/Fs20VariantOverlayLayout.cs') (Join-Path $PSScriptRoot 'tests/test_fs20_overlay_layout.cs')
if($LASTEXITCODE -ne 0){throw 'Test compilation failed'}
& $exe (Join-Path $out 'overlay.json')
if($LASTEXITCODE -ne 0){throw 'Offline test failed'}
$exe=Join-Path $out 'landing.exe'
& $csc /nologo /target:exe "/out:$exe" (Join-Path $PSScriptRoot 'src/Fs20LandingEnvelope.cs') (Join-Path $PSScriptRoot 'src/Fs20HookUv.cs') (Join-Path $PSScriptRoot 'tests/test_fs20_landing.cs')
if($LASTEXITCODE -ne 0){throw 'Test compilation failed'}
& $exe
if($LASTEXITCODE -ne 0){throw 'Offline test failed'}
