using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20NALiveries
    {
        private static readonly Dictionary<Fs20NANightScheme,LiveryData> Presets=new Dictionary<Fs20NANightScheme,LiveryData>();
        private static bool registered;
        private const string Provider="FS20NA.NightLivery";
        private static string Key(int index)=>"fa206e6100000000000000000000000"+index;
        internal static void Configure(AircraftParameters parameters)
        {
            if(!registered){
                var map=new ResourceLocationMap("FS20NA night liveries",7);
                Addressables.ResourceManager.ResourceProviders.Add(new NightProvider());
                for(int i=0;i<7;i++)map.Add(Key(i),new ResourceLocationBase(Key(i),Key(i),Provider,typeof(LiveryData)){Data=i});
                Addressables.AddResourceLocator(map);registered=true;
            }
            var native=new List<AircraftParameters.Livery>(parameters.liveries);
            parameters.liveries=new List<AircraftParameters.Livery>();
            foreach(var spec in new[]{new[]{"BDF Navy Standard - NA","6f1aee58aecf7e4459bbb9bd5af2295e"},new[]{"BDF Navy Hex - NA","254bccbe9cef8b244a170514de5fb092"}}){
                var item=native.Find(l=>l.assetReference.AssetGUID==spec[1]);
                if(item.assetReference==null)throw new InvalidOperationException("Native FS-20 BDF livery missing.");
                parameters.liveries.Add(new AircraftParameters.Livery{name=spec[0],faction=item.faction,assetReference=new AssetReferenceLiveryData(spec[1])});
            }
            parameters.liveries.Add(new AircraftParameters.Livery{name="BDF Aggressor Photo",faction=parameters.liveries[0].faction,assetReference=new AssetReferenceLiveryData(Key(2))});
            parameters.liveries.Add(new AircraftParameters.Livery{name="BDF Forest Digital Hex",faction=parameters.liveries[0].faction,assetReference=new AssetReferenceLiveryData(Key(6))});
            foreach(var spec in new[]{new[]{"PALA Desert Splinter - NA","a44bcb67795df70498f7e3e1b51afce5"},new[]{"PALA Matte Black - NA","fe1c1fdd1cf206545b8168ab70714d58"}}){
                var item=native.Find(l=>l.assetReference.AssetGUID==spec[1]);
                if(item.assetReference==null)throw new InvalidOperationException("Native FS-20 PALA livery missing.");
                parameters.liveries.Add(new AircraftParameters.Livery{name=spec[0],faction=item.faction,assetReference=new AssetReferenceLiveryData(spec[1])});
            }
        }
        internal static LiveryData Preset(Fs20NANightScheme scheme)
        {
            if(Presets.TryGetValue(scheme,out var data)&&data!=null)return data;
            data=ScriptableObject.CreateInstance<LiveryData>();data.name="FS20NA native "+scheme;
            data.Texture=Fs20NANightPaint.Palette(scheme);data.Glossiness=.05f;data.Colors=Array.Empty<LiveryData.TextureColor>();
            Presets.Add(scheme,data);return data;
        }
        // Native LiveryKeys and the regular asynchronous loader resolve these
        // registered assets. No detour of struct-valued native loading methods.
        private sealed class NightProvider:ResourceProviderBase
        {
            public override string ProviderId=>Provider;
            public override Type GetDefaultType(IResourceLocation location)=>typeof(LiveryData);
            private static readonly Dictionary<string,AsyncOperationHandle<LiveryData>> NativeLoads=new Dictionary<string,AsyncOperationHandle<LiveryData>>();
            public override void Provide(ProvideHandle handle){
                var choice=(Fs20NANightScheme)(int)handle.Location.Data;int index=(int)choice;
                string guid=index==0?"6f1aee58aecf7e4459bbb9bd5af2295e":index==1||index==6?"254bccbe9cef8b244a170514de5fb092":index==3?"a44bcb67795df70498f7e3e1b51afce5":index==4||index==5?"fe1c1fdd1cf206545b8168ab70714d58":null;
                if(guid==null){handle.Complete(Preset(choice),true,(Exception)null);return;}
                int donorIndex=index==6?1:index==5?4:index;
                if(Fs20NANightPaint.TryNativeReference((Fs20NANightScheme)donorIndex,out var available)){
                    Fs20NANightPaint.SetNativeReference(choice,available);Presets.Remove(choice);handle.Complete(Preset(choice),true,(Exception)null);return;
                }
                // Resolve legacy private keys through the game's own assets too.
                // Retain one immutable native handle per built-in paint.
                if(!NativeLoads.TryGetValue(guid,out var load)){load=Addressables.LoadAssetAsync<LiveryData>(guid);NativeLoads.Add(guid,load);}
                void Complete(AsyncOperationHandle<LiveryData> op){
                    if(op.Status!=AsyncOperationStatus.Succeeded){handle.Complete<LiveryData>(null,false,op.OperationException);return;}
                    Fs20NANightPaint.SetNativeReference((Fs20NANightScheme)donorIndex,op.Result.Texture);Fs20NANightPaint.SetNativeReference(choice,op.Result.Texture);Presets.Remove(choice);handle.Complete(Preset(choice),true,(Exception)null);
                }
                if(load.IsDone)Complete(load);else load.Completed+=Complete;
            }
            public override void Release(IResourceLocation location,object asset){} // Shared immutable cache; released at process teardown.
        }
    }
}
