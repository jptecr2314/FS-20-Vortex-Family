param([Parameter(Mandatory)][string]$Output,[Parameter(Mandatory)][string]$Candidate,[switch]$ReusePayload,[switch]$SkipCaptures,[switch]$FocusCache,[switch]$Network,[string]$PaintPack)
$ErrorActionPreference='Stop'
$game='D:/Build/nuclear-option-qol/fs20-ai-control/game'
if(Get-Process NuclearOption -ErrorAction SilentlyContinue){throw 'Preserve the running game.'}
if(Test-Path -LiteralPath $Output){throw 'Preserve earlier evidence.'}
New-Item -ItemType Directory -Path $Output|Out-Null
$managed="$game/NuclearOption_Data/Managed";$source="$Output/NativeFs20StreamingBench.cs"
Copy-Item -LiteralPath (Join-Path $PSScriptRoot $(if($Network){'NativeFs20LocalNetwork.cs'}else{'NativeFs20StreamingBench.cs'})) -Destination $source
$refs=@('mscorlib.dll','netstandard.dll','System.dll','System.Core.dll','Assembly-CSharp.dll','UnityEngine.dll','UnityEngine.CoreModule.dll','UnityEngine.PhysicsModule.dll','UnityEngine.AudioModule.dll','UnityEngine.JSONSerializeModule.dll','UnityEngine.InputLegacyModule.dll','Mirage.dll','Newtonsoft.Json.dll','UnityEngine.ScreenCaptureModule.dll','UnityEngine.UIModule.dll','UnityEngine.ImageConversionModule.dll','Unity.RenderPipelines.Core.Runtime.dll','Unity.RenderPipelines.Universal.Runtime.dll','UnityEngine.AssetBundleModule.dll')|ForEach-Object {Join-Path $managed $_}
$refs+=@('BepInEx.dll','0Harmony.dll')|ForEach-Object {Join-Path "$game/BepInEx/core" $_}
if($Network){$refs+=@('com.rlabrecque.steamworks.net.dll','UniTask.dll','Unity.Addressables.dll','Unity.ResourceManager.dll')|ForEach-Object {Join-Path $managed $_}}
$args=@('/nologo','/target:library','/langversion:latest','/nostdlib+',"/out:$Output/FS20StreamingBench.dll")+@($refs|ForEach-Object {'/reference:'+$_})+@($source)
& (Join-Path $PSScriptRoot '../.deps/compiler/tasks/net472/csc.exe') @args
if($LASTEXITCODE-ne0){throw 'Streaming probe compilation failed.'}
$installed='C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option/BepInEx/plugins/VortexConventional/VortexConventional.dll';$installedHash=(Get-FileHash $installed).Hash
$family="$game/BepInEx/plugins/VortexConventional";$probe="$game/BepInEx/plugins/FS20StreamingBench.dll"
if((Test-Path "$family/VortexConventional.dll")-or(Test-Path $probe)){throw 'Preserve existing private plugins.'}
$mission='C:/Users/João Paulo/AppData/LocalLow/Shockfront/FS20AIControl/Missions/FS20 Streaming QA'
if(Test-Path -LiteralPath $mission){throw 'Preserve the existing mission.'}
$moves=@();$process=$null;$clientProcess=$null;$copied=$false;$payloadCopied=$false;$missionCopied=$false;$customCopied=$false;$customDirectoryCreated=$false
try{
 foreach($folder in @("$game/BepInEx/plugins/com.nikkorap.blueprinter/addons","$game/BepInEx/plugins/JPStagedReconPrototype")){
  if(!(Test-Path $folder)){continue};$absolute=[IO.Path]::GetFullPath($folder);$dest=[IO.Path]::GetFullPath((Join-Path $Output ('preserved-'+$moves.Count)))
  if(!$absolute.StartsWith('D:\Build\nuclear-option-qol\fs20-ai-control\game\BepInEx\plugins\',[StringComparison]::OrdinalIgnoreCase)-or!$dest.StartsWith([IO.Path]::GetFullPath($Output)+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Private preservation target mismatch'}
  Move-Item -LiteralPath $absolute -Destination $dest;$moves+=@{original=$absolute;saved=$dest}
 }
 New-Item -ItemType Directory -Path $family -Force|Out-Null
 if($ReusePayload){New-Item -ItemType HardLink -Path "$family/VortexConventional.dll" -Target $Candidate|Out-Null}else{Copy-Item -LiteralPath $Candidate -Destination "$family/VortexConventional.dll"};$copied=$true
 $payload=Join-Path (Split-Path $Candidate) 'BuiltinPaints';if(Test-Path $payload){if(Test-Path "$family/BuiltinPaints"){throw 'Preserve existing private paint payload'};$payloadCopied=$true
  if($ReusePayload){New-Item -ItemType Directory -Path "$family/BuiltinPaints"|Out-Null;Get-ChildItem -LiteralPath $payload -File|ForEach-Object {New-Item -ItemType HardLink -Path (Join-Path "$family/BuiltinPaints" $_.Name) -Target $_.FullName|Out-Null}}else{Copy-Item -LiteralPath $payload -Destination "$family/BuiltinPaints" -Recurse}
 }
 Copy-Item -LiteralPath "$Output/FS20StreamingBench.dll" -Destination $probe
 if($PaintPack){$customDirectory="$family/Paints";$customTarget=Join-Path $customDirectory ([IO.Path]::GetFileName($PaintPack));if(Test-Path -LiteralPath $customTarget){throw 'Preserve the existing custom paint'};$customDirectoryCreated=!(Test-Path -LiteralPath $customDirectory);New-Item -ItemType Directory -Path $customDirectory -Force|Out-Null;Copy-Item -LiteralPath $PaintPack -Destination $customTarget;$customCopied=$true}
 $missionCopied=$true;Copy-Item -LiteralPath 'C:/Users/João Paulo/AppData/LocalLow/Shockfront/FS20AIControl/Missions/FS20NA Flight Test' -Destination $mission -Recurse
 Rename-Item -LiteralPath "$mission/FS20NA Flight Test.json" -NewName 'FS20 Streaming QA.json'
 $m=Get-Content -LiteralPath "$mission/FS20 Streaming QA.json" -Raw|ConvertFrom-Json;$m.environment.weatherIntensity=1.0;$m.environment.cloudAltitude=1800.0;$m.environment.timeOfDay=14.0;$m.environment.timeFactor=0.0;$m|ConvertTo-Json -Depth 100|Set-Content -LiteralPath "$mission/FS20 Streaming QA.json" -Encoding utf8
 $p=[Diagnostics.ProcessStartInfo]::new();$p.FileName="$game/NuclearOption.exe";$p.WorkingDirectory=$game;$p.UseShellExecute=$false;$p.WindowStyle='Hidden';$p.CreateNoWindow=$true;$p.Environment['FS20_STREAM_OUTPUT']=$Output
 if($SkipCaptures){$p.Environment['FS20_STREAM_SKIP_CAPTURES']='1'};if($FocusCache){$p.Environment['FS20_STREAM_FOCUS_CACHE']='1'}
 if($Network){
  $password=[Guid]::NewGuid().ToString('N')
  @{MissionDirectory=(Split-Path $mission);ModdedServer=$true;Hidden=$true;ServerName='FS20 private verification';Port=@{IsOverride=$true;Value=28996};QueryPort=@{IsOverride=$true;Value=28997};Password=$password;MaxPlayers=2;BanListPaths=@();DisableErrorKick=$false;ErrorKickImmuneListPaths=@();NoPlayerStopTime=300;PostMissionDelay=30;RotationType=0;MissionRotation=@(@{Key=@{Group='User';Name='FS20 Streaming QA'};MaxTime=600})}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath "$Output/server-config.json" -Encoding UTF8
  $p.Environment['FS20_NET_OUTPUT']=$Output;$p.Environment['FS20_NET_ROLE']='server';$p.Environment['FS20_NET_PASSWORD']=$password
  foreach($arg in @('-batchmode','-DedicatedServer',"$Output/server-config.json",'-SteamServerInit','true','-limitframerate','30','-logFile',"$Output/server.log")){$p.ArgumentList.Add($arg)}
 }else{foreach($arg in @('-batchmode','-screen-fullscreen','0','-screen-width','1400','-screen-height','1000','-mission','FS20 Streaming QA','-socket','Offline','-logFile',"$Output/Player.log")){$p.ArgumentList.Add($arg)}}
 $process=[Diagnostics.Process]::Start($p)
 @{pid=$process.Id;executable=$p.FileName;installedGame=$false;privateNativeCopy=$true;batchMode=$true;noGraphics=$false;hidden=$true;familySha256=(Get-FileHash $Candidate).Hash;probeSha256=(Get-FileHash $probe).Hash}|ConvertTo-Json|Set-Content "$Output/launch.json"
 if($Network){
  $deadline=[DateTime]::UtcNow.AddMinutes(3)
  while(!(Test-Path -LiteralPath "$Output/ready.json")-and!$process.HasExited-and[DateTime]::UtcNow-lt$deadline){Start-Sleep -Seconds 1}
  if(!(Test-Path -LiteralPath "$Output/ready.json")){throw 'Native anonymous server did not become ready; see server.log. Authentication was not modified.'}
  $p.ArgumentList.Clear();$p.Environment['FS20_NET_ROLE']='client'
  foreach($arg in @('-batchmode','-limitframerate','30','-logFile',"$Output/client.log")){$p.ArgumentList.Add($arg)}
  $clientProcess=[Diagnostics.Process]::Start($p)
  if(!$clientProcess.WaitForExit(360000)){throw 'Private native client timed out'}
  if(!$process.WaitForExit(30000)){throw 'Private native server did not finish'}
  $clientReport=Get-Content -LiteralPath "$Output/client.json" -Raw|ConvertFrom-Json
  if(!$clientReport.completed-or$clientProcess.ExitCode-ne0){throw 'Native client verification failed'}
 }elseif(!$process.WaitForExit(1200000)){$process.Kill();$process.WaitForExit();throw 'Private streaming test exceeded timeout'}
 $report=Get-Content -LiteralPath $(if($Network){"$Output/server.json"}else{"$Output/streaming.json"}) -Raw|ConvertFrom-Json
 if($process.ExitCode-ne0-or!$report.completed-or$report.error){throw ('Private streaming test failed: '+$report.error)}
 $report|Select-Object completed,assertions,graphics,batchMode|ConvertTo-Json
}finally{
 if($clientProcess-and!$clientProcess.HasExited){$clientProcess.Kill();$clientProcess.WaitForExit()}
 if($process-and!$process.HasExited){$process.Kill();$process.WaitForExit()}
 if(Test-Path $probe){Remove-Item -LiteralPath $probe}
 if($customCopied){if((Get-FileHash -LiteralPath $customTarget).Hash-ne(Get-FileHash -LiteralPath $PaintPack).Hash){throw 'Private custom paint changed concurrently'};Remove-Item -LiteralPath $customTarget;if($customDirectoryCreated-and!(Get-ChildItem -LiteralPath $customDirectory)){Remove-Item -LiteralPath $customDirectory}}
 if($copied){if((Get-FileHash "$family/VortexConventional.dll").Hash-ne(Get-FileHash $Candidate).Hash){throw 'Private family changed concurrently'};Remove-Item -LiteralPath "$family/VortexConventional.dll"}
 if($payloadCopied-and(Test-Path "$family/BuiltinPaints")){
  $resolved=[IO.Path]::GetFullPath("$family/BuiltinPaints");if($resolved-ne'D:\Build\nuclear-option-qol\fs20-ai-control\game\BepInEx\plugins\VortexConventional\BuiltinPaints'){throw 'Private payload cleanup mismatch'};Remove-Item -LiteralPath $resolved -Recurse -Force
 }
 if($missionCopied-and(Test-Path $mission)){if([IO.Path]::GetFullPath($mission)-ne'C:\Users\João Paulo\AppData\LocalLow\Shockfront\FS20AIControl\Missions\FS20 Streaming QA'){throw 'Temporary mission cleanup mismatch'};Remove-Item -LiteralPath $mission -Recurse -Force}
 foreach($move in $moves){if(Test-Path $move.original){throw 'Concurrent private addon appeared'};Move-Item -LiteralPath $move.saved -Destination $move.original}
 $preserved=(Get-FileHash $installed).Hash-eq$installedHash
 @{installedDllPreserved=$preserved;privatePluginsRestored=$true;temporaryMissionRemoved=!(Test-Path $mission);probeRemoved=!(Test-Path $probe);ownedProcessExited=(!$process-or$process.HasExited)}|ConvertTo-Json|Set-Content "$Output/restoration.json"
 if(!$preserved){throw 'Installed family changed outside the test'}
}
