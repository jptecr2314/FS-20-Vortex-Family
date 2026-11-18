param([string]$Output='D:/Artifacts/fs20-gun-duels/2026-11-17/run1',[string]$Variants='B,A,C,T,NA',[string]$Candidate,[int]$SeedBase=7041)
$ErrorActionPreference='Stop'
$game='D:/Build/nuclear-option-qol/fs20-ai-control/game'
if(Get-Process NuclearOption -ErrorAction SilentlyContinue){throw 'Preserve the existing game process.'}
if(Test-Path $Output){throw 'Preserve earlier results.'}
New-Item -ItemType Directory -Path $Output -Force|Out-Null
$installedFamily='C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option/BepInEx/plugins/VortexConventional/VortexConventional.dll'
$installedHash=(Get-FileHash -LiteralPath $installedFamily).Hash
$testFamily=if($Candidate){[IO.Path]::GetFullPath($Candidate)}else{$installedFamily}
$addons=[IO.Path]::GetFullPath("$game/BepInEx/plugins/com.nikkorap.blueprinter/addons")
$parked=[IO.Path]::GetFullPath("$Output/preserved-addons")
if(!$addons.StartsWith('D:\Build\nuclear-option-qol\fs20-ai-control\game\BepInEx\plugins\',[StringComparison]::OrdinalIgnoreCase) -or !$parked.StartsWith([IO.Path]::GetFullPath($Output)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Private addon preservation path mismatch.'}
$managed="$game/NuclearOption_Data/Managed"
$refs=@('mscorlib.dll','netstandard.dll','System.dll','System.Core.dll','Assembly-CSharp.dll','UnityEngine.dll','UnityEngine.CoreModule.dll','UnityEngine.PhysicsModule.dll','UnityEngine.AudioModule.dll','UnityEngine.JSONSerializeModule.dll','UnityEngine.InputLegacyModule.dll','Mirage.dll','Newtonsoft.Json.dll')|ForEach-Object {Join-Path $managed $_}
$refs+=@('BepInEx.dll','0Harmony.dll')|ForEach-Object {Join-Path "$game/BepInEx/core" $_}
$refs+=Join-Path $managed 'UnityEngine.ScreenCaptureModule.dll'
$refs+=Join-Path $managed 'UnityEngine.UIModule.dll'
$source=Join-Path $Output 'NativeFs20GunDuels.cs';Copy-Item (Join-Path $PSScriptRoot 'NativeFs20GunDuels.cs') $source
$args=@('/nologo','/target:library','/langversion:latest','/nostdlib+',"/out:$Output/Fs20GunDuels.dll")
$args+=$refs|ForEach-Object {'/reference:'+$_};$args+=$source
& (Join-Path $PSScriptRoot '../.deps/compiler/tasks/net472/csc.exe') @args
if($LASTEXITCODE-ne0){throw 'Profiler compilation failed.'}
$probe="$game/BepInEx/plugins/Fs20GunDuels.dll"
if(Test-Path $probe){throw 'Existing profiler must be preserved.'}
$mission='C:/Users/João Paulo/AppData/LocalLow/Shockfront/FS20AIControl/Missions/FS20 Spawn Profile'
if(Test-Path $mission){throw 'Existing user mission must be preserved.'}
$family="$game/BepInEx/plugins/VortexConventional/VortexConventional.dll"
if(Test-Path -LiteralPath $family){throw 'Preserve the existing private family DLL.'}
$parkedAddons=$false;$familyCopied=$false
$process=$null
try{
 if(Test-Path -LiteralPath $addons){Move-Item -LiteralPath $addons -Destination $parked;$parkedAddons=$true}
 New-Item -ItemType Directory -Path (Split-Path $family) -Force|Out-Null
 Copy-Item -LiteralPath $testFamily -Destination $family;$familyCopied=$true;$hash=(Get-FileHash $family).Hash
 Get-ChildItem "$game/BepInEx/plugins" -Recurse -Filter '*.dll'|ForEach-Object {[pscustomobject]@{path=$_.FullName;sha256=(Get-FileHash $_.FullName).Hash}}|ConvertTo-Json -Depth 3|Set-Content "$Output/installed-plugins.json"
 Copy-Item -LiteralPath 'C:/Users/João Paulo/AppData/LocalLow/Shockfront/FS20AIControl/Missions/FS20NA Flight Test' -Destination $mission -Recurse
 Rename-Item -LiteralPath "$mission/FS20NA Flight Test.json" -NewName 'FS20 Spawn Profile.json'
 Copy-Item "$Output/Fs20GunDuels.dll" $probe
 $p=[Diagnostics.ProcessStartInfo]::new();$p.FileName="$game/NuclearOption.exe";$p.WorkingDirectory=$game;$p.UseShellExecute=$false;$p.WindowStyle='Normal'
 $p.Environment['FS20_DUEL_VARIANTS']=$Variants;$p.Environment['FS20_DUEL']='1';$p.Environment['FS20_DUEL_OUTPUT']=$Output
 $p.Environment['FS20_DUEL_SEED']=$SeedBase.ToString([Globalization.CultureInfo]::InvariantCulture)
 foreach($a in @('-screen-fullscreen','0','-screen-width','1920','-screen-height','1080','-mission','FS20 Spawn Profile','-socket','Offline','-logFile',"$Output/Player.log")){$p.ArgumentList.Add($a)}
 $process=[Diagnostics.Process]::Start($p)
 @{pid=$process.Id;executable=$p.FileName;installedGame=$false;privateNativeCopy=$true;batchMode=$false;noGraphics=$false;familySha256=$hash;probeSha256=(Get-FileHash $probe).Hash;sourceSha256=(Get-FileHash $source).Hash}|ConvertTo-Json|Set-Content "$Output/launch.json"
 if(!$process.WaitForExit(1800000)){$process.Kill();$process.WaitForExit();throw 'Owned test exceeded its timeout.'}
 if($process.ExitCode-ne0){throw 'Native duel test failed; inspect report/log.'}
 $r=Get-Content "$Output/duels.json" -Raw|ConvertFrom-Json;if(!$r.completed-or$r.error){throw 'Native duel report incomplete.'}
 $r.rounds|Select-Object label,elapsed,outcome|ConvertTo-Json -Depth 4
}finally{
 if($process-and!$process.HasExited){$process.Kill();$process.WaitForExit()}
 if(Test-Path $probe){Remove-Item -LiteralPath $probe}
 if([IO.Path]::GetFullPath($mission)-ne'C:\Users\João Paulo\AppData\LocalLow\Shockfront\FS20AIControl\Missions\FS20 Spawn Profile'){throw 'Temporary mission cleanup path mismatch.'}
 if(Test-Path $mission){Remove-Item -LiteralPath $mission -Recurse -Force}
 $privateUnchanged=(!$familyCopied) -or ((Get-FileHash $family).Hash-eq$hash)
 if($familyCopied -and $privateUnchanged){Remove-Item -LiteralPath $family}
 if($parkedAddons){if(Test-Path -LiteralPath $addons){throw 'Concurrent private addons appeared; preserve both directories.'};Move-Item -LiteralPath $parked -Destination $addons}
 $installedUnchanged=(Get-FileHash -LiteralPath $installedFamily).Hash-eq$installedHash
 @{installedFamilyUnchanged=$installedUnchanged;privateFamilyUnchanged=$privateUnchanged;temporaryFamilyRemoved=!(Test-Path $family);addonsRestored=(!$parkedAddons -or (Test-Path $addons));probeRemoved=!(Test-Path $probe);temporaryMissionRemoved=!(Test-Path $mission);ownedProcessExited=(!$process -or $process.HasExited)}|ConvertTo-Json|Set-Content "$Output/restoration.json"
 if(!$installedUnchanged -or !$privateUnchanged){throw 'Family changed during test; do not overwrite concurrent changes.'}
 if($process){$process.Dispose()}
}
