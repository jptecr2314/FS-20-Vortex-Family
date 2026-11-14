param([string]$Stage='D:/Artifacts/fs20-multiplayer/0.6.8')
$ErrorActionPreference='Stop'
$gamePath='C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option'
$receipt=Get-Content -LiteralPath (Join-Path $stage 'BUILD.json') -Raw | ConvertFrom-Json
Add-Type -LiteralPath (Join-Path $gamePath 'BepInEx/core/Mono.Cecil.dll')
$candidate=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($receipt.dll)
$game=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $gamePath 'NuclearOption_Data/Managed/Assembly-CSharp.dll'))
$mirage=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $gamePath 'NuclearOption_Data/Managed/Mirage.dll'))
function AllTypes($types){foreach($type in $types){$type;AllTypes $type.NestedTypes}}
$all=@(AllTypes $candidate.MainModule.Types)
$runtime=@(AllTypes $game.MainModule.Types)+@(AllTypes $mirage.MainModule.Types)+$all
$network=$all | Where-Object Name -eq Fs20Multiplayer
$install=$network.Methods | Where-Object Name -eq Install
$surface=$all | Where-Object Name -eq Fs20NetworkSurfaces
$install=@($install)+@($surface.Methods | Where-Object Name -eq Install)
$installed=@($install.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.TypeReference]} | ForEach-Object {$_.Operand.FullName})
$contracts=@()
foreach($patch in @($network.NestedTypes)+@($surface.NestedTypes)){
    $attribute=$patch.CustomAttributes | Where-Object {$_.AttributeType.FullName -eq 'HarmonyLib.HarmonyPatch'}
    if(!$attribute){continue}
    if($patch.FullName -notin $installed){throw ('Patch not installed: '+$patch.Name)}
    if($attribute.ConstructorArguments.Count -eq 0){
        if($patch.Name -ne 'FlightAuthority'){throw 'Unexpected dynamic patch target'}
        $target=$patch.Methods | Where-Object Name -eq TargetMethods
        if(!$target){throw 'Missing owner-only controller targets'}
        foreach($typeName in 'Fs20FlightController','Fs20AFlightController','Fs20TFlightController'){
            $type=$all | Where-Object Name -eq $typeName
            if(!($type.Methods | Where-Object Name -eq PrepareControls) -or !($type.Fields | Where-Object {$_.Name -eq 'aircraft' -and $_.FieldType.Name -eq 'Aircraft'})){throw 'Flight authority field contract changed'}
        }
        $label='A/C/T PrepareControls; owner-only'
        if($receipt.variants -contains 'NA'){
            $yaw=$all|Where-Object Name -eq Fs20NAYawController
            if(!($yaw.Methods|Where-Object Name -eq Apply) -or !($yaw.Fields|Where-Object {$_.Name -eq 'aircraft' -and $_.FieldType.Name -eq 'Aircraft'})){throw 'NA yaw authority contract changed'}
            $label+='; NA yaw Apply'
        }
        $contracts+=[pscustomobject]@{patch=$patch.Name;target=$label;verified=$true};continue
    }
    if($attribute.ConstructorArguments.Count -ne 2){throw 'Unexpected patch attribute shape'}
    $typeName=$attribute.ConstructorArguments[0].Value.FullName
    $methodName=$attribute.ConstructorArguments[1].Value
    $type=$runtime | Where-Object FullName -eq $typeName | Select-Object -First 1
    $methods=@($type.Methods | Where-Object Name -eq $methodName)
    if($methods.Count -ne 1){throw ('Ambiguous/missing patch target: '+$typeName+'::'+$methodName)}
    $method=$methods[0]
    foreach($handler in $patch.Methods | Where-Object {$_.Name -in @('Prefix','Postfix','Finalizer')}){
        foreach($parameter in $handler.Parameters){
            $name=$parameter.Name;$actual=$parameter.ParameterType.FullName.TrimEnd('&')
            if($name -match '^__(\d+)$'){
                $index=[int]$Matches[1]
                if($index -ge $method.Parameters.Count -or $actual -ne $method.Parameters[$index].ParameterType.FullName){throw ('Argument mismatch: '+$patch.Name+' '+$name)}
            }elseif($name -eq '__result'){
                if($actual -ne $method.ReturnType.FullName){throw ('Return mismatch: '+$patch.Name)}
            }elseif($name -like '___*'){
                $field=$type.Fields | Where-Object Name -eq $name.Substring(3)
                if(!$field -or $field.FieldType.FullName -ne $actual){throw ('Field mismatch: '+$patch.Name+' '+$name)}
            }
        }
    }
    $contracts+=[pscustomobject]@{patch=$patch.Name;target=$method.FullName;verified=$true}
}
if($contracts.Count -ne 24){throw ('Unexpected patch count '+$contracts.Count)}
$entry=$all | Where-Object Name -eq VortexConventionalPlugin
$awake=$entry.Methods | Where-Object Name -eq Awake
$instanceStore=$awake.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.FieldReference] -and $_.Operand.Name -eq 'instance' -and $_.OpCode.Code.ToString() -eq 'Stsfld'} | Select-Object -First 1
$gateInstall=$awake.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.Name -eq 'Fs20SinglePlayer' -and $_.Operand.Name -eq 'Install'} | Select-Object -First 1
if(!$instanceStore -or !$gateInstall -or $instanceStore.Offset -ge $gateInstall.Offset){throw 'Plugin configuration is not available when installing network hooks'}
foreach($pair in @(@('SharedAirframeStock','sharedStock'),@('VortexConventionalPlugin','flapMode'))){
    $type=$all | Where-Object Name -eq $pair[0]
    if(!($type.Fields | Where-Object Name -eq $pair[1])){throw 'Local host-setting field changed'}
}
$admission=$network.NestedTypes | Where-Object Name -eq ServerAdmission
if(-not($admission.Methods | Where-Object Name -eq Prefix | ForEach-Object {$_.Body.Instructions} | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'get_Accepted'})){throw 'Server admission does not require the connection handshake'}
$nativeManager=$game.MainModule.Types | Where-Object Name -eq NetworkManagerNuclearOption
$prefabs=$nativeManager.Methods | Where-Object Name -eq RegisterPrefabs
if(-not($prefabs.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.FullName -match 'RegisterAll<AircraftDefinition>'})){throw 'Native manager no longer registers the extended aircraft list'}
foreach($name in 'FlightAuthority','HookForceAuthority','BayMassAuthority'){
    $patch=$network.NestedTypes | Where-Object Name -eq $name
    $calls=@($patch.Methods | Where-Object Name -eq Prefix | ForEach-Object {$_.Body.Instructions} | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference]} | ForEach-Object {$_.Operand.FullName})
    if(-not($calls | Where-Object {$_ -match 'Unit::get_LocalSim'})){throw ('Missing native simulation-owner guard: '+$name)}
}
$wait=$network.Methods | Where-Object Name -eq WaitForCompatibility
$stateType=($wait.CustomAttributes | Where-Object {$_.AttributeType.Name -eq 'AsyncStateMachineAttribute'}).ConstructorArguments[0].Value.FullName
$state=$all | Where-Object FullName -eq $stateType
$move=$state.Methods | Where-Object Name -eq MoveNext
$await=$move.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.FullName -match 'UniTask`1<Mirage.Authentication.AuthenticationResult>::GetAwaiter'} | Select-Object -First 1
$success=$move.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'get_Success'} | Select-Object -First 1
if(!$await -or !$success -or $await.Offset -ge $success.Offset){throw 'Native authentication is not awaited before FS-20 admission'}
$calls=@($all | Where-Object {$_.FullName -match 'Fs20Multiplayer'} | ForEach-Object Methods | Where-Object HasBody | ForEach-Object {$_.Body.Instructions} | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference]} | ForEach-Object {$_.Operand.FullName})
if($calls | Where-Object {$_ -match 'AuthenticationResult::CreateSuccess|ConfigEntry.*::set_Value'}){throw 'Native auth success synthesized / client config persisted'}
$signature=$all | Where-Object Name -eq Fs20NetworkSignature
$signatureCalls=@($signature.Methods | Where-Object HasBody | ForEach-Object {$_.Body.Instructions} | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference]} | ForEach-Object {$_.Operand.FullName})
if($signatureCalls | Where-Object {$_ -match 'set_LookupIndex|set_PrefabHash'}){throw 'Compatibility signature changes native IDs'}
$policy=$all | Where-Object Name -eq Fs20NetworkPolicy
if(($policy.Fields | Where-Object Name -eq TimeoutMilliseconds).Constant -ne 10000){throw 'Compatibility wait is not bounded as designed'}
$typeNames=@($contracts | ForEach-Object target)
if($typeNames | Where-Object {$_ -match 'SteamAuthenticate|UDPAuthenticate|BuildHashDifferent|GetBuildHash|CheckSteamAuthToken'}){throw 'Native identity/build verification patched'}
$result=[ordered]@{version=$receipt.version;candidateSha256=(Get-FileHash -LiteralPath $receipt.dll).Hash.ToLowerInvariant();passed=$true;scope='Static Mono.Cecil inspection only; no candidate IL executed';patchContracts=$contracts;nativeAuthenticationAwaited=$true;nativeAuthenticationSuccessNeverSynthesized=$true;steamPasswordBuildChecksUnmodified=$true;perConnectionHostAdmission=$true;simulationOwnerGuards=$true;clientConfigurationNotRewritten=$true;networkDefinitionIdsNotRewritten=$true;gameLaunched=$false;runtimeHandshakeTested=$false}
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $stage 'NATIVE_CONTRACTS.json') -Encoding utf8
'FS20_MULTIPLAYER_NATIVE_CONTRACTS_PASS targets='+$contracts.Count+' scope=static-only'
$candidate.Dispose();$game.Dispose();$mirage.Dispose()
