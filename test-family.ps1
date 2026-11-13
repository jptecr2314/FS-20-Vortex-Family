param([string]$GamePath=$env:NUCLEAR_OPTION_PATH,[string]$Compiler)
$ErrorActionPreference='Stop'
if(!$GamePath){$GamePath='C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option'}
if(!$Compiler){& (Join-Path $PSScriptRoot 'restore-compiler.ps1');$Compiler=Join-Path $PSScriptRoot '.deps/compiler/tasks/net472/csc.exe'}
$plugin=Join-Path $PSScriptRoot 'build/BepInEx/plugins/VortexConventional/VortexConventional.dll'
$out=Join-Path $PSScriptRoot 'build/tests/family'
New-Item -ItemType Directory -Path $out -Force|Out-Null
function Invoke-Check([string]$Program,[string[]]$Arguments){
    $start=[Diagnostics.ProcessStartInfo]::new()
    $start.FileName=$Program
    $start.UseShellExecute=$false
    $start.CreateNoWindow=$true
    $start.RedirectStandardOutput=$true
    $start.RedirectStandardError=$true
    foreach($argument in $Arguments){$start.ArgumentList.Add($argument)}
    $process=[Diagnostics.Process]::Start($start)
    try{
        $stdout=$process.StandardOutput.ReadToEndAsync()
        $stderr=$process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        if($process.ExitCode -ne 0){throw ($stdout.Result+$stderr.Result)}
        $stdout.Result.TrimEnd()
    }finally{$process.Dispose()}
}
& $Compiler /nologo /langversion:latest /target:exe ('/out:'+(Join-Path $out 'FamilyChecks.exe')) (Join-Path $PSScriptRoot 'tests/Fs20FamilyIntegration.cs')
if($LASTEXITCODE -ne 0){throw 'Family test compilation failed'}
$family=Invoke-Check (Join-Path $out 'FamilyChecks.exe') @($plugin,$GamePath)
$family|Write-Output
$stock=Invoke-Check (Get-Process -Id $PID).Path @('-NoProfile','-File',(Join-Path $PSScriptRoot 'test-stock-option.ps1'),'-GamePath',$GamePath,'-PluginAssembly',$plugin,'-Compiler',$Compiler,'-TestRoot',(Join-Path $out 'stock-option'))
$stock|Write-Output
$familyChecks=[regex]::Match(($family -join "`n"),'FS20_FAMILY_INTEGRATION_PASS checks=(\d+)')
$gateChecks=[regex]::Match(($stock -join "`n"),'SINGLEPLAYER_GATE_PASS checks=(\d+)')
if(!$familyChecks.Success -or !$gateChecks.Success -or ($stock -join "`n") -notmatch 'PASS actual ConfigEntry ON/OFF/ON'){throw 'Verification result missing'}
& $Compiler /nologo /langversion:latest /target:exe ('/out:'+(Join-Path $out 'PitchGovernorChecks.exe')) (Join-Path $PSScriptRoot 'tests/Fs20TPitchGovernor.cs')
if($LASTEXITCODE -ne 0){throw 'Pitch governor test compilation failed'}
$pitch=Invoke-Check (Join-Path $out 'PitchGovernorChecks.exe') @($plugin,$GamePath)
$pitch|Write-Output
$pitchChecks=[regex]::Match(($pitch -join "`n"),'FS20T_PITCH_GOVERNOR_PASS checks=(\d+)')
if(!$pitchChecks.Success){throw 'Pitch governor checks failed'}
[ordered]@{passed=$true;pluginSha256=(Get-FileHash -LiteralPath $plugin).Hash;familyChecks=[int]$familyChecks.Groups[1].Value;stockConfigOnOffPersistence=$true;singlePlayerChecks=[int]$gateChecks.Groups[1].Value;pitchGovernorChecks=[int]$pitchChecks.Groups[1].Value;scope='Actual compiled plugin helpers and native prefix contracts, offline. No Unity UI or game launch.'}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $out 'family-validation.json') -Encoding utf8
