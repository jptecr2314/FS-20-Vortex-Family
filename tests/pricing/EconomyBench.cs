using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Mirage;
using Mirage.SocketLayer;
using NuclearOption;
using NuclearOption.Networking;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
using Player=NuclearOption.Networking.Player;
using NetworkPlayer=Mirage.NetworkPlayer;

namespace FS20EconomyBench
{
    public static class LocalBench
    {
        const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        static Assembly plugin;
        static int checks;
        static uint netId=100;
        static GameObject holder;
        static NetworkServer server;
        static List<GameObject> players=new List<GameObject>();
        [Serializable] sealed class Trade {public string bought,sold;public bool shared,rejected;public float start,end;}
        [Serializable] sealed class Report {
            public bool passed,legacyExploit,installed=false,steamSessionTested=false;
            public string error,pluginSha256,scope="Native server purchase/sale/ownership/flight methods and real SyncList/SyncDictionary, controlled inactive Mirage server identity; no socket, mission or real-client RPC transport.";
            public int checks;public List<Trade> trades=new List<Trade>();
        }
        static Type P(string name)=>plugin.GetType("Japensou.NuclearOption."+name,true);
        static FieldInfo F(Type type,string name){for(;type!=null;type=type.BaseType){var f=type.GetField(name,All|BindingFlags.DeclaredOnly);if(f!=null)return f;}throw new MissingFieldException(name);}
        static void Set(object o,string n,object v)=>F(o.GetType(),n).SetValue(o,v);
        static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,All).Invoke(o,args);
        static object Static(string t,string n,params object[] args)=>P(t).GetMethod(n,All).Invoke(null,args);
        static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
        sealed class Wire:IConnection {
            public IConnectionHandle Handle=>null;public ConnectionState State=>ConnectionState.Connected;
            public void Disconnect(){}public void Disconnect(DisconnectReason r){}
            public void SendReliable(byte[] b,int o,int c){}public void SendUnreliable(byte[] b,int o,int c){}
            public INotifyToken SendNotify(byte[] b,int o,int c)=>null;
            public void SendNotify(byte[] b,int o,int c,INotifyCallBack cb){}public void FlushBatch(){}
        }
        static T Net<T>(bool client=false) where T:NetworkBehaviour {
            var go=new GameObject(typeof(T).Name);go.SetActive(false);go.transform.SetParent(holder.transform,false);players.Add(go);
            var identity=go.AddComponent<NetworkIdentity>();Set(identity,"<NetId>k__BackingField",++netId);
            Set(identity,"<Server>k__BackingField",client?null:server);Set(identity,"_owner",new NetworkPlayer(new Wire(),false,null,null));
            Set(identity,"<SyncVarSender>k__BackingField",new SyncVarSender());
            var value=go.AddComponent<T>();Set(value,"_identity",identity);value.SyncSettings=new SyncSettings{From=(SyncFrom)2,To=(SyncTo)2};
            Check(value.IsServer==!client,"Controlled native server identity failed");return value;
        }
        static Player Buyer(float funds=500){var p=Net<Player>();Set(p,"<Allocation>k__BackingField",funds);return p;}
        static void Buy(Player p,AircraftDefinition d)=>Call(p,"UserCode_CmdPurchaseAirframe_787674834",d);
        static void Sell(Player p,AircraftDefinition d)=>Call(p,"UserCode_CmdSellAirframe_1853434019",d);
        static void InstallShared(ConfigFile config,bool enabled) {
            Static("SharedAirframes.SharedAirframeStock","Disable");
            var entry=(ConfigEntry<bool>)Static("SharedAirframes.SharedAirframeStock","Configure",config);entry.Value=enabled;
            Static("SharedAirframes.SharedAirframeStock","Enable",new ManualLogSource("FS20 economy local bench"));
            Check((bool)P("SharedAirframes.SharedAirframeStock").GetProperty("Enabled",All).GetValue(null)==enabled,"Stock hooks failed to install");
        }
        public static void Run(){
            string output=Environment.GetEnvironmentVariable("FS20_ECONOMY_OUTPUT"),file=Environment.GetEnvironmentVariable("FS20_ECONOMY_DLL");
            Directory.CreateDirectory(output);var report=new Report();var definitions=new List<AircraftDefinition>();bool legacy=Environment.GetEnvironmentVariable("FS20_ECONOMY_LEGACY")=="1";
            try {
                plugin=Assembly.LoadFrom(file);
                using(var sha=System.Security.Cryptography.SHA256.Create())report.pluginSha256=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))).Replace("-","").ToLowerInvariant();
                typeof(BepInEx.Paths).GetProperty("BepInExConfigPath",All).SetValue(null,Path.Combine(output,"bepinex.cfg"));
                typeof(BepInEx.Paths).GetProperty("ConfigPath",All).SetValue(null,output);
                holder=new GameObject("FS20 inactive economy bench");holder.SetActive(false);server=holder.AddComponent<NetworkServer>();Set(server,"<Active>k__BackingField",true);
                string[] keys={"JP_FS20_A","SmallFighter1","JP_FS20_CTOL","JP_FS20_T","JP_FS20_NA"};float[] prices={75,90,105,125,150};
                for(int i=0;i<keys.Length;i++) {
                    var d=ScriptableObject.CreateInstance<AircraftDefinition>();d.jsonKey=d.name=d.unitName=keys[i];d.value=90;
                    if(legacy)d.value=prices[i];else Static("VortexConventional.Fs20VariantPricing","Apply",d);
                    Check(d.value==prices[i],"Native definition price mismatch: "+keys[i]);definitions.Add(d);
                }
                var config=new ConfigFile(Path.Combine(output,"fixture.cfg"),false);
                if(legacy){
                    InstallShared(config,true);var p=Buyer();Buy(p,definitions[0]);Sell(p,definitions[4]);
                    report.legacyExploit=p.Allocation==575&&p.OwnedAirframes.Count==0;
                    Check(report.legacyExploit,"Old shared ownership did not reproduce proposed-price arbitrage");
                    report.trades.Add(new Trade{bought=keys[0],sold=keys[4],shared=true,start=500,end=p.Allocation});
                }else foreach(bool shared in new[]{true,false}) {
                    InstallShared(config,shared);
                    foreach(var bought in definitions)foreach(var sold in definitions) {
                        var p=Buyer();Buy(p,bought);
                        Check(p.Allocation==500-bought.value&&p.OwnedAirframes.Count==1,"Native purchase did not debit exact price");
                        Check(p.OwnsAirframe(bought,false)&&p.OwnedAirframeTypeCount(bought,true)==1,"Native owned identity missing");
                        Sell(p,sold);bool same=bought==sold;
                        Check(p.Allocation==(same?500:500-bought.value),"Sale credited a different variant or wrong amount");
                        Check(p.OwnedAirframes.Count==(same?0:1),"Sale consumed wrong ownership");
                        if(!same){
                            Check(!p.OwnsAirframe(sold,true)&&p.OwnedAirframeTypeCount(sold,true)==0,"Cross-variant ownership still accepted");
                            Call(p,"FlyOwnedAirframe",sold);Check(p.OwnedAirframes.Count==1,"Flying expensive model consumed cheap ownership");
                            var inUse=F(typeof(Player),"AirframeInUse").GetValue(p);Check(inUse==null,"Cross-variant flight populated in-use airframe");
                            Sell(p,bought);Check(p.Allocation==500&&p.OwnedAirframes.Count==0,"Legitimate resale after refused swap failed");
                        }
                        Sell(p,sold);Check(p.Allocation==500&&p.OwnedAirframes.Count==0,"Repeated sale created allocation");
                        report.trades.Add(new Trade{bought=bought.jsonKey,sold=sold.jsonKey,shared=shared,start=500,end=p.Allocation,rejected=!same});
                    }
                    foreach(var d in definitions){
                        var p=Buyer();Call(p,"CreditAirframe",d,1,true);Sell(p,d);
                        Check(p.Allocation==500&&p.OwnedAirframes.Count==1&&!p.OwnsAirframe(d,false),"Reserved aircraft became sellable");
                        Buy(p,d);Call(p,"CreditAirframe",d,1,true);Sell(p,d);
                        Check(p.Allocation==500&&p.OwnedAirframes.Count==2&&p.OwnedAirframes.All(c=>c.Reserved),"Sale consumed reservation instead of paid cell");
                        Sell(p,d);Check(p.Allocation==500&&p.OwnedAirframes.Count==2,"Repeated sale cashed a reserved cell");
                        var flight=Buyer();Buy(flight,d);Call(flight,"FlyOwnedAirframe",d);
                        var cell=(OwnedAirframe?)F(typeof(Player),"AirframeInUse").GetValue(flight);
                        Check(flight.OwnedAirframes.Count==0&&cell.HasValue&&cell.Value.Definition==d&&!cell.Value.Reserved,"Native flight lost purchased identity");
                        Call(flight,"RecoverAirframeInUse",d==definitions[4]?definitions[0]:definitions[4]);
                        Check(flight.OwnedAirframes.Count==1&&flight.OwnedAirframes[0].Definition==d&&!flight.OwnedAirframes[0].Reserved,"Native recovery converted the owned model");
                        Sell(flight,d);
                        Check(flight.Allocation==500,"Exact-model flight/return/resale changed allocation");
                        var borrowed=Buyer();Call(borrowed,"CreditAirframe",d,1,true);Call(borrowed,"FlyOwnedAirframe",d);
                        Call(borrowed,"RecoverAirframeInUse",d==definitions[4]?definitions[0]:definitions[4]);Sell(borrowed,d);
                        Check(borrowed.Allocation==500&&borrowed.OwnedAirframes.Count==1&&borrowed.OwnedAirframes[0].Definition==d&&borrowed.OwnedAirframes[0].Reserved,"Flying/recovering a reservation made it sellable");
                        var poor=Buyer(d.value-1);Buy(poor,d);Check(poor.OwnedAirframes.Count==0&&poor.Allocation==d.value-1,"Insufficient funds purchase accepted");
                    }
                    var hq=Net<FactionHQ>();hq.AircraftSupply.Add(definitions[1],new FactionHQ.RuntimeSupply(48));
                    Check(hq.GetUnitSupply(definitions[4])==(shared?48:0),"Faction shared-supply selection changed");
                    if(shared){hq.ModifyUnitSupply(definitions[4],-1);Check(hq.AircraftSupply[definitions[1]].Count==47&&hq.AircraftSupply.Count==1,"Shared stock was duplicated/reassigned");}
                    var client=Net<Player>(true);bool denied=false;try{Call(client,"CreditAirframe",definitions[0],-1,false);}catch(TargetInvocationException e){denied=e.InnerException is MethodInvocationException;}
                    Check(denied,"Client bypassed native server restriction");
                }
                report.passed=true;
            }catch(Exception e){while(e is TargetInvocationException&&e.InnerException!=null)e=e.InnerException;report.error=e.ToString();Debug.LogException(e);}
            finally {
                if(plugin!=null)try{Static("SharedAirframes.SharedAirframeStock","Disable");}catch{}
                if(server!=null)Set(server,"<Active>k__BackingField",false);
                if(holder!=null)Object.DestroyImmediate(holder);foreach(var d in definitions)Object.DestroyImmediate(d);
                report.checks=checks;File.WriteAllText(Path.Combine(output,"economy.json"),JsonUtility.ToJson(report,true));
            }
            EditorApplication.Exit(report.passed?0:1);
        }
    }
}
