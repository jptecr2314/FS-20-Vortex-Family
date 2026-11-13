using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using BepInEx.Configuration;
using BepInEx.Logging;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using Mirage;
using Mirage.Authentication;
using Mirage.Serialization;
using Mirage.SocketLayer;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using NetworkPlayer = Mirage.NetworkPlayer;

namespace FS20MultiplayerBench
{
    // Native MessagePacker + MessageHandler + NetworkPlayer, with an in-memory
    // wire. Authentication inputs are explicit fixture results; no Steam call
    // is simulated as a successful real login. This is not a two-PC flight.
    public static class NativeBench
    {
        private const BindingFlags All = BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private const string Prefix="Japensou.NuclearOption.VortexConventional.";
        private static Assembly plugin;
        private static int checks, frames;
        private static string output;
        private static Harmony harmony;
        [Serializable] private sealed class Report
        {
            public bool passed, gameLaunched=false, steamLoginTested=false, fullFlightTested=false;
            public string error, pluginSha256, unityVersion;
            public int checks, nativeWirePackets, patches;
            public List<string> scenarios=new List<string>();
        }
        private static Type P(string name)=>plugin.GetType(Prefix+name,true);
        private static FieldInfo F(Type t,string name)
        {
            for(;t!=null;t=t.BaseType){var f=t.GetField(name,All|BindingFlags.DeclaredOnly);if(f!=null)return f;}
            throw new MissingFieldException(name);
        }
        private static void Set(object value,string field,object data)=>F(value.GetType(),field).SetValue(value,data);
        private static object Get(object value,string field)=>F(value.GetType(),field).GetValue(value);
        private static void Static(string type,string field,object value)=>F(P(type),field).SetValue(null,value);
        private static object Call(string type,string method,params object[] args)=>P(type).GetMethod(method,All).Invoke(null,args);
        private static void Check(bool ok,string why){checks++;if(!ok)throw new InvalidOperationException(why);}
        private static byte[] Digest=Enumerable.Range(0,32).Select(i=>(byte)i).ToArray();
        private static byte[] Pack(Type type,object message)=> (byte[])typeof(MessagePacker).GetMethods().Single(m=>m.Name=="Pack"&&m.IsGenericMethod&&m.GetParameters().Length==1).MakeGenericMethod(type).Invoke(null,new[]{message});
        private sealed class Wire : IConnection
        {
            internal Action<byte[]> OnSend;
            internal bool closed;
            public IConnectionHandle Handle=>null;
            public ConnectionState State=>closed?ConnectionState.Disconnected:ConnectionState.Connected;
            public void Disconnect(){closed=true;}
            public void Disconnect(DisconnectReason reason){closed=true;}
            private void Send(byte[] bytes,int offset,int count){frames++;OnSend?.Invoke(bytes.Skip(offset).Take(count).ToArray());}
            public void SendReliable(byte[] bytes,int offset,int count)=>Send(bytes,offset,count);
            public void SendUnreliable(byte[] bytes,int offset,int count)=>Send(bytes,offset,count);
            public INotifyToken SendNotify(byte[] bytes,int offset,int count){Send(bytes,offset,count);return null;}
            public void SendNotify(byte[] bytes,int offset,int count,INotifyCallBack callback)=>Send(bytes,offset,count);
            public void FlushBatch(){}
        }
        private static void Serializers()
        {
            var message=P("Fs20CompatibilityMessage");
            foreach(var item in new[]{new[]{typeof(Writer<>).AssemblyQualifiedName,"Write"},new[]{typeof(Reader<>).AssemblyQualifiedName,"Read"}})
            {
                var open=Type.GetType(item[0]);var closed=open.MakeGenericType(message);
                var property=closed.GetProperty(item[1],All);var field=closed.GetField(item[1],All);
                var type=property!=null?property.PropertyType:field.FieldType;
                var value=Delegate.CreateDelegate(type,P("Fs20CompatibilitySerialization").GetMethod(item[1],All));
                if(property!=null)property.SetValue(null,value);else field.SetValue(null,value);
            }
            Call("Fs20Multiplayer","EnsureMessageType");
        }
        private static void Handshake(Report report,bool matching,bool nativeSuccess,bool tamperAck=false)
        {
            Call("Fs20Multiplayer","ClearServer");
            Static("Fs20Multiplayer","serverOnline",true);Static("Fs20Multiplayer","serverDigest",Digest);
            Static("Fs20Multiplayer","serverStock",true);Static("Fs20Multiplayer","serverFlaps",(byte)2);
            Static("Fs20Multiplayer","clientHandler",null);
            var clientDigest=(byte[])Digest.Clone();if(!matching)clientDigest[3]^=17;
            Static("Fs20Multiplayer","clientDigest",clientDigest);
            var policy=F(P("Fs20Multiplayer"),"clientPolicy").GetValue(null);
            policy.GetType().GetMethod("Begin",All).Invoke(policy,new object[]{clientDigest});
            var serverHandler=new MessageHandler(null,false,true);var clientHandler=new MessageHandler(null,false,true);
            Call("Fs20Multiplayer","Register",serverHandler,true);Call("Fs20Multiplayer","Register",clientHandler,false);
            var serverWire=new Wire();var clientWire=new Wire();
            var serverPeer=new NetworkPlayer(serverWire,false,null,null);var clientPeer=new NetworkPlayer(clientWire,false,null,null);
            serverWire.OnSend=data=>clientHandler.HandleMessage(clientPeer,new ArraySegment<byte>(data));
            clientWire.OnSend=data=>{
                if(tamperAck&&data.Length>=54&&data[3]==2){data=(byte[])data.Clone();data[data.Length-1]^=1;}
                serverHandler.HandleMessage(serverPeer,new ArraySegment<byte>(data));
            };
            var hello=Activator.CreateInstance(P("Fs20CompatibilityMessage"));
            Set(hello,"Protocol",P("Fs20NetworkPolicy").GetField("Protocol",All).GetRawConstantValue());Set(hello,"Phase",(byte)0);
            Set(hello,"Digest",clientDigest);Set(hello,"Nonce",new byte[16]);
            clientWire.SendReliable(Pack(hello.GetType(),hello),0,Pack(hello.GetType(),hello).Length);
            var completion=new UniTaskCompletionSource<AuthenticationResult>();
            var pending=(UniTask<AuthenticationResult>)Call("Fs20Multiplayer","WaitForCompatibility",completion.Task,serverPeer,CancellationToken.None);
            Check(pending.Status==UniTaskStatus.Pending,"Compatibility must await native authentication");
            var peer=Call("Fs20Multiplayer","Peer",serverPeer);
            Check(!(bool)peer.GetType().GetProperty("OfferSent",All).GetValue(peer),"No host policy before native result");
            var native=nativeSuccess?AuthenticationResult.CreateSuccess("Fixture only"):AuthenticationResult.CreateFail("Fixture native failure");
            completion.TrySetResult(native);
            Check(pending.Status.IsCompleted(),"Matching/mismatched handshake should complete through native handlers");
            var result=pending.GetAwaiter().GetResult();
            Check(result.Success==(matching&&nativeSuccess&&!tamperAck),"Handshake admission result: "+result.Reason);
            var admission=P("Fs20Multiplayer+ServerAdmission").GetMethod("Prefix",All);
            Check((bool)admission.Invoke(null,new object[]{serverPeer})==(matching&&nativeSuccess&&!tamperAck),"Server admission requires both checks");
            if(matching&&nativeSuccess&&!tamperAck)
            {
                Check((bool)P("Fs20Multiplayer+ClientAdmission").GetMethod("Prefix",All).Invoke(null,new object[]{clientPeer}),"Client requires host offer");
                Check((bool)policy.GetType().GetProperty("Active",All).GetValue(policy),"Client policy activated");
                Check((bool)policy.GetType().GetProperty("SharedStock",All).GetValue(policy),"Host stock policy replicated");
                Check((byte)policy.GetType().GetProperty("FlapMode",All).GetValue(policy)==2,"Host flap policy replicated");
            }
            P("Fs20Multiplayer+ServerDisconnected").GetMethod("Postfix",All).Invoke(null,new object[]{serverPeer});
            P("Fs20Multiplayer+ClientDisconnected").GetMethod("Postfix",All).Invoke(null,null);
            Check(!((IDictionary)F(P("Fs20Multiplayer"),"peers").GetValue(null)).Contains(serverPeer),"Disconnected peer removed");
            Check(!(bool)policy.GetType().GetProperty("Active",All).GetValue(policy),"Client session policy cleared");
            report.scenarios.Add("native-wire matching="+matching+" nativeSuccess="+nativeSuccess+" tamperedAck="+tamperAck);
        }
        private static void Controls(Report report,GameObject holder)
        {
            var aircraft=NativeAircraft(holder);
            Set(aircraft,"<remoteSim>k__BackingField",false);
            var prefix=P("Fs20Multiplayer+FlightAuthority").GetMethod("Prefix",All);
            Check((bool)prefix.Invoke(null,new object[]{aircraft}),"Simulation owner may run assistance");
            Set(aircraft,"<remoteSim>k__BackingField",true);
            Check(!(bool)prefix.Invoke(null,new object[]{aircraft}),"Remote assistance must be suppressed");
            foreach(var name in new[]{"Fs20FlightController","Fs20AFlightController","Fs20TFlightController"})
                Check(harmony.GetPatchedMethods().Any(m=>m.DeclaringType==P(name)&&m.Name=="PrepareControls"),"Missing controller hook: "+name);
            Check(plugin.GetType(Prefix+"Fs20NAAirframe")==null,"NA must be absent");
            report.scenarios.Add("owner-only A/C/T controllers; NA absent");
        }
        private static Aircraft NativeAircraft(GameObject holder)
        {
            var source=AssetDatabase.LoadAssetAtPath<Object>("Assets/Blueprinter/_donotship/MonoBehaviour/SmallFighter1_PLACEHOLDER.asset");
            Check(source!=null,"Native B definition is available");
            var prefab=(GameObject)Get(source,"unitPrefab");
            var copy=Object.Instantiate(prefab,holder.transform,false);
            var aircraft=copy.GetComponent<Aircraft>();
            Check(aircraft!=null,"Native B clone has a valid Aircraft component");return aircraft;
        }
        private static void HostPolicy(Report report,ConfigFile config)
        {
            var stock=config.Bind("Stock","shared",false);
            var modeType=P("Fs20FlapMode");
            var bind=typeof(ConfigFile).GetMethods().Single(m=>m.Name=="Bind"&&m.IsGenericMethod&&m.GetParameters().Length==4&&m.GetParameters()[3].ParameterType==typeof(string));
            var flap=bind.MakeGenericMethod(modeType).Invoke(config,new[]{(object)"C","flaps",Enum.ToObject(modeType,0),"Local preference"});
            var shared=plugin.GetType("Japensou.NuclearOption.SharedAirframes.SharedAirframeStock",true);
            shared.GetField("sharedStock",All).SetValue(null,stock);
            Static("Fs20Multiplayer","localStock",stock);Static("Fs20Multiplayer","localFlaps",flap);
            config.Save();var before=File.ReadAllBytes(config.ConfigFilePath);
            Static("Fs20Multiplayer","serverOnline",true);Static("Fs20Multiplayer","serverStock",true);Static("Fs20Multiplayer","serverFlaps",(byte)2);
            Check((bool)shared.GetProperty("Enabled",All).GetValue(null),"Host stock overrides local setting");
            Check(Convert.ToByte(P("VortexConventionalPlugin").GetProperty("FlapMode",All).GetValue(null))==2,"Host flap snapshot applies");
            Call("Fs20Multiplayer","ClearServer");
            var policy=F(P("Fs20Multiplayer"),"clientPolicy").GetValue(null);
            policy.GetType().GetMethod("Begin",All).Invoke(policy,new object[]{Digest});
            Check((bool)policy.GetType().GetMethod("Offer",All).Invoke(policy,new object[]{P("Fs20NetworkPolicy").GetField("Protocol",All).GetRawConstantValue(),Digest,new byte[16],true,(byte)2}),"Client accepts host policy");
            policy.GetType().GetMethod("Authenticate",All).Invoke(policy,new object[]{true});
            Check((bool)shared.GetProperty("Enabled",All).GetValue(null),"Client uses host shared stock");
            Check(Convert.ToByte(P("VortexConventionalPlugin").GetProperty("FlapMode",All).GetValue(null))==2,"Client uses host flap policy");
            Check(stock.Value==false&&Convert.ToByte(flap.GetType().GetProperty("Value").GetValue(flap))==0,"Local preference values preserved");
            Check(before.SequenceEqual(File.ReadAllBytes(config.ConfigFilePath)),"Client config bytes preserved");
            policy.GetType().GetMethod("Reset",All).Invoke(policy,null);
            Check(!(bool)shared.GetProperty("Enabled",All).GetValue(null),"Local stock restored after disconnect");
            report.scenarios.Add("host stock/flap policy; original client configuration preserved/restored");
        }
        private static void NativeDamage(Report report,GameObject holder)
        {
            // Native binary RPC serializers must preserve the byte IDs used by
            // the extended T parts, including indices above the original wing.
            var generated=typeof(Aircraft).Assembly.GetType("Mirage.GeneratedNetworkCode",true);
            foreach(string name in new[]{"RpcDamage","RpcBreakPart"})
                Check(typeof(Unit).GetMethods(All).Any(m=>m.Name==name&&m.GetParameters()[0].ParameterType==typeof(byte)),"Native damage part IDs changed");
            report.scenarios.Add("native byte-index damage/detachment contract");
        }
        private static void SurfaceMotion(Report report,GameObject holder)
        {
            var aircraft=NativeAircraft(holder);var obj=aircraft.gameObject;
            var def=Object.Instantiate(aircraft.definition);def.jsonKey="JP_FS20_T";((Unit)aircraft).definition=def;
            Set(aircraft,"<remoteSim>k__BackingField",true);
            foreach(var nativeHigh in obj.GetComponentsInChildren<HighLiftDevice>(true))Object.DestroyImmediate(nativeHigh);
            var wing=obj.transform.Find("wing1a_L").gameObject;var part=wing.GetComponent<AeroPart>();Set(part,"parentUnit",aircraft);Set(part,"wingArea",5f);
            aircraft.partLookup=new List<UnitPart>{part};
            var moving=new GameObject("slat");moving.transform.SetParent(wing.transform);var slat=moving.AddComponent(P("Fs20TSlat"));
            Set(slat,"ClosedRotation",Quaternion.identity);Set(slat,"Axis",Vector3.right);Set(slat,"Stroke",Vector3.forward);Set(slat,"FullAngle",20f);
            var high=wing.AddComponent(P("Fs20THighLift"));Set(high,"part",part);var slats=Array.CreateInstance(P("Fs20TSlat"),1);slats.SetValue(slat,0);Set(high,"slats",slats);
            var state=obj.AddComponent(P("Fs20NetworkSurfaceState"));state.GetType().GetMethod("Bind",All).Invoke(state,new object[]{aircraft});
            var message=Activator.CreateInstance(P("Fs20SurfaceMessage"));
            Set(message,"NetId",(uint)8);Set(message,"Sequence",(uint)1);Set(message,"PartIds",new byte[]{0});Set(message,"Lift",new[]{75000f});Set(message,"Area",new[]{5f});
            Set(message,"Slats",new[]{1f});Set(message,"Hooks",new float[0]);Set(message,"HookFlags",new byte[0]);
            bool Receive()=> (bool)state.GetType().GetMethod("Receive",All).Invoke(state,new object[]{message,null});
            Check(Receive(),"T remote snapshot accepted");
            state.GetType().GetMethod("Animate",All).Invoke(state,new object[]{.1f});
            Check(moving.transform.localPosition.z>.19f,"T remote slat follows deployment");
            Check(Mathf.Abs(part.GetWingArea()-5f)<.0001f,"Remote slat animation must not change physical wing area");
            Check(!Receive(),"Duplicate snapshot rejected");
            Set(part,"detachedFromUnit",true);var before=moving.transform.localPosition;
            state.GetType().GetMethod("Animate",All).Invoke(state,new object[]{.1f});
            Check(moving.transform.localPosition==before,"Detached slat must stop receiving motion");
            Set(part,"detachedFromUnit",false);Set(part,"fragmented",true);
            state.GetType().GetMethod("Animate",All).Invoke(state,new object[]{.1f});
            Check(moving.transform.localPosition==before,"Fragmented slat must stop receiving motion");
            Set(aircraft,"<remoteSim>k__BackingField",false);Set(message,"Sequence",(uint)2);
            Check(!Receive(),"Presentation packet cannot override simulation owner");
            Object.DestroyImmediate(def);
            report.scenarios.Add("T slat motion; no remote wing-area edit; duplicate/owner rejection; detached/fragmented motion stop");
        }
        private static void SurfaceMotionC(Report report,GameObject holder)
        {
            var aircraft=NativeAircraft(holder);var obj=aircraft.gameObject;
            var def=Object.Instantiate(aircraft.definition);def.jsonKey="JP_FS20_CTOL";((Unit)aircraft).definition=def;
            Set(aircraft,"<remoteSim>k__BackingField",true);
            var part=obj.transform.Find("wing1a_L").GetComponent<AeroPart>();Set(part,"parentUnit",aircraft);aircraft.partLookup=new List<UnitPart>{part};
            var source=AssetDatabase.LoadAssetAtPath<WeaponMount>("Assets/Blueprinter/_donotship/MonoBehaviour/TailHook_Multirole1_PLACEHOLDER.asset");
            Check(source!=null,"Native hook definition available");
            var hookObj=Object.Instantiate(source.prefab,part.transform,false);var hook=hookObj.GetComponent<TailHook>();hook.unitPart=part;
            var fairing=hookObj.AddComponent(P("Fs20HookFairing"));
            var slats=obj.GetComponentsInChildren<HighLiftDevice>(true).Where(x=>x.enabled).ToArray();
            Check(slats.Length>0,"C native slat schedules available");
            var state=obj.AddComponent(P("Fs20NetworkSurfaceState"));state.GetType().GetMethod("Bind",All).Invoke(state,new object[]{aircraft});
            var message=Activator.CreateInstance(P("Fs20SurfaceMessage"));
            Set(message,"NetId",(uint)9);Set(message,"Sequence",(uint)1);Set(message,"PartIds",new byte[]{0});Set(message,"Lift",new[]{75000f});Set(message,"Area",new[]{5f});
            Set(message,"Slats",Enumerable.Repeat(1f,slats.Length).ToArray());Set(message,"Hooks",new[]{1f});Set(message,"HookFlags",new byte[]{5});
            Check((bool)state.GetType().GetMethod("Receive",All).Invoke(state,new object[]{message,null}),"C remote mechanisms accepted");
            state.GetType().GetMethod("Animate",All).Invoke(state,new object[]{.1f});
            Check(slats.All(x=>(float)Get(x,"position")>.19f),"C remote native slat positions applied");
            Check((float)Get(hook,"deployedAmount")>.19f&&(bool)Get(hook,"deployed"),"Remote hook deployment applied");
            Check((bool)Get(fairing,"Overloaded"),"Hook overload presentation replicated");
            Check(!(bool)P("Fs20Multiplayer+HookForceAuthority").GetMethod("Prefix",All).Invoke(null,new object[]{hook}),"Remote hook must not apply arresting forces");
            Object.DestroyImmediate(def);report.scenarios.Add("C native slat animation and hook deployment/overload; owner-only arresting force");
        }
        private static void TLayout(Report report,GameObject holder)
        {
            var original=AssetDatabase.LoadAssetAtPath<AircraftDefinition>("Assets/Blueprinter/_donotship/MonoBehaviour/SmallFighter1_PLACEHOLDER.asset");
            var template=NativeAircraft(holder).gameObject;
            foreach(var part in template.GetComponentsInChildren<AeroPart>(true))
            {
                Set(part,"<xform>k__BackingField",part.transform);
                if((Transform)Get(part,"liftNormal")==null)Set(part,"liftNormal",part.transform);
                if(part.parentUnit==null)part.parentUnit=template.GetComponent<Aircraft>();
                if((float)Get(part,"baseMass")==0f)Set(part,"baseMass",part.mass);
            }
            // Apply the exact authored geometry while inactive. Runtime
            // Register intentionally uses DontDestroyOnLoad, unavailable in
            // an editor fixture; do not patch that Unity lifecycle contract.
            Call("Fs20TGeometry","Apply",template.transform);
            var left=Object.Instantiate(template,holder.transform,false);var right=Object.Instantiate(template,holder.transform,false);
            var a=left.GetComponentsInChildren<UnitPart>(true);var b=right.GetComponentsInChildren<UnitPart>(true);
            Check(a.Length<256&&a.Length==b.Length,"Actual T clone native part budget and layout");
            Check(a.Select(p=>p.name).SequenceEqual(b.Select(p=>p.name)),"Actual T clone part order is repeatable");
            var sections=left.GetComponentsInChildren(P("Fs20TDamageState"),true);
            Check(sections.Length>0,"Actual T has independent damage sections");
            foreach(var component in sections)
            {
                var part=(AeroPart)Get(component,"part");
                Check(part.parentUnit==left.GetComponent<Aircraft>(),"Cloned T damage owner remains in its own aircraft");
                Check(part.Joints.All(j=>j.connectedPart.transform.IsChildOf(left.transform)),"Cloned T structural joints remain local");
            }
            Check(left.GetComponentsInChildren<NetworkBehaviour>(true).All(n=>n.GetType().Assembly!=plugin),"No unweaved custom network behaviours added");
            report.scenarios.Add("actual T prefab: repeatable "+a.Length+" parts; "+sections.Length+" damage owners and structural clone references");
            Object.DestroyImmediate(template);
        }
        private static void SurfaceWire(Report report)
        {
            var message=Activator.CreateInstance(P("Fs20SurfaceMessage"));
            Set(message,"NetId",(uint)77);Set(message,"Sequence",(uint)12);Set(message,"PartIds",new byte[]{0});Set(message,"Lift",new[]{75000f});Set(message,"Area",new[]{5f});
            Set(message,"Slats",new[]{.5f});Set(message,"Hooks",new float[0]);Set(message,"HookFlags",new byte[0]);
            var bytes=Pack(message.GetType(),message);
            var unpack=typeof(MessagePacker).GetMethods().Single(m=>m.Name=="Unpack"&&m.IsGenericMethod).MakeGenericMethod(message.GetType());
            var decoded=unpack.Invoke(null,new object[]{bytes,null});
            Check((uint)Get(decoded,"NetId")==77&&((float[])Get(decoded,"Lift"))[0]==75000f,"Native surface serializer roundtrip");
            var handler=new MessageHandler(null,false,true);Call("Fs20NetworkSurfaces","Register",handler,true);
            var connection=new Wire();var peer=new NetworkPlayer(connection,false,null,null);
            handler.HandleMessage(peer,new ArraySegment<byte>(bytes));
            Check(!peer.IsConnected,"Native Mirage must reject unauthenticated surface data");
            report.scenarios.Add("native surface serializer; unauthenticated presentation packet rejected");
        }
        public static void Run()
        {
            output=Environment.GetEnvironmentVariable("FS20_MP_OUTPUT");Directory.CreateDirectory(output);
            var report=new Report{unityVersion=Application.unityVersion};GameObject holder=null;
            try
            {
                var file=Environment.GetEnvironmentVariable("FS20_MP_DLL");plugin=Assembly.LoadFrom(file);
                using(var sha=System.Security.Cryptography.SHA256.Create())report.pluginSha256=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(plugin.Location))).Replace("-","").ToLowerInvariant();
                harmony=new Harmony("fs20.local.multiplayer.068");
                foreach(var nested in P("Fs20Multiplayer").GetNestedTypes(All).Where(t=>t.GetCustomAttributes(typeof(HarmonyPatch),false).Length>0))harmony.PatchAll(nested);
                Call("Fs20NetworkSurfaces","Install",harmony);Serializers();
                typeof(BepInEx.Paths).GetProperty("BepInExConfigPath",All).SetValue(null,Path.Combine(output,"fixture-bepinex.cfg"));
                typeof(BepInEx.Paths).GetProperty("ConfigPath",All).SetValue(null,output);
                Static("Fs20Multiplayer","logger",new ManualLogSource("FS20 local bench"));
                var config=new ConfigFile(Path.Combine(output,"fixture.cfg"),false);
                Static("Fs20Multiplayer","enabled",config.Bind("MP","enabled",false));
                Check(!(bool)Call("Fs20Multiplayer","AllowStart"),"Disabled multiplayer must be denied");
                Handshake(report,true,true);Handshake(report,false,true);Handshake(report,true,false);Handshake(report,true,true);Handshake(report,true,true,true);
                holder=new GameObject("FS20 local multiplayer fixtures");holder.SetActive(false);
                Controls(report,holder);NativeDamage(report,holder);SurfaceMotion(report,holder);SurfaceMotionC(report,holder);HostPolicy(report,config);TLayout(report,holder);SurfaceWire(report);
                report.patches=harmony.GetPatchedMethods().Count();report.nativeWirePackets=frames;
                Check(report.patches>=25,"Expected all network integration hooks to bind");
                report.passed=true;
            }
            catch(Exception error){while(error is TargetInvocationException&&error.InnerException!=null)error=error.InnerException;report.error=error.ToString();Debug.LogException(error);}
            finally
            {
                report.checks=checks;report.nativeWirePackets=frames;report.patches=harmony==null?0:harmony.GetPatchedMethods().Count();harmony?.UnpatchSelf();if(holder!=null)Object.DestroyImmediate(holder);
                File.WriteAllText(Path.Combine(output,"native-bench.json"),JsonUtility.ToJson(report,true));
                EditorApplication.Exit(report.passed?0:1);
            }
        }
    }
}
