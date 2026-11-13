using System;
using System.IO;
using System.Linq;
using System.Reflection;

internal static class Fs20FamilyIntegration
{
    private static int checks;
    private const BindingFlags Hidden=BindingFlags.Static|BindingFlags.NonPublic;
    private static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
    private static void Main(string[] args)
    {
        string[] folders={Path.GetDirectoryName(Path.GetFullPath(args[0])),Path.Combine(args[1],"BepInEx/core"),Path.Combine(args[1],"NuclearOption_Data/Managed")};
        AppDomain.CurrentDomain.AssemblyResolve+=(sender,eventArgs)=>{
            string file=folders.Select(p=>Path.Combine(p,new AssemblyName(eventArgs.Name).Name+".dll")).FirstOrDefault(File.Exists);
            return file==null?null:Assembly.LoadFrom(file);
        };
        var assembly=Assembly.LoadFrom(args[0]);
        var pool=assembly.GetType("Japensou.NuclearOption.SharedAirframes.AirframePool",true);
        object Call(string method,params object[] values)=>pool.GetMethod(method,Hidden).Invoke(null,values);
        string[] keys={"JP_FS20_A","SmallFighter1","JP_FS20_CTOL","JP_FS20_T"};
        foreach(string a in keys)foreach(string b in keys)Check((bool)Call("Same",a,b),"A/B/C/T must share the family identity");
        Check(!(bool)Call("IsFamily","OtherAircraft"),"Unrelated aircraft must remain separate");
        foreach(string requested in keys){
            int[] stock={0,48,0,0};
            for(int flight=0;flight<48;flight++){
                Check((int)Call("Count",stock)==48-flight,"The shared balance must not be copied");
                var debit=(int[])Call("PlanDebit",stock,1L);
                Check(debit.Sum()==1,"Each sortie consumes one airframe");
                for(int i=0;i<stock.Length;i++)stock[i]-=debit[i];
            }
            Check(stock.Sum()==0,"48 sorties exhaust 48 airframes for "+requested);
        }
        var returned=(int[])Call("PlanDebit",new[]{0,0,0,3},2L);
        Check(returned.SequenceEqual(new[]{0,0,0,2}),"Native T inventory participates in the same pool");
        var owned=(int[])Call("SelectOwned",new[]{"OtherAircraft","JP_FS20_T","SmallFighter1","JP_FS20_A"},new[]{false,false,true,false},"JP_FS20_CTOL",3L);
        Check(owned.SequenceEqual(new[]{3,2,1}),"Owned T cells can supply C without consuming unrelated ownership");
        var layout=assembly.GetType("Japensou.NuclearOption.VortexConventional.Fs20VariantOverlayLayout",true);
        float Field(object value,string name)=>(float)layout.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);
        foreach(var size in new[]{new[]{1024f,576f},new[]{1920f,1080f},new[]{2560f,1440f},new[]{720f,1280f}})
            foreach(float scale in new[]{.75f,1f,1.5f,2f})foreach(float edge in new[]{-.95f,0f,.95f}){
                float w=size[0]/scale,h=size[1]/scale,x=edge*w/2;
                var panel=layout.GetMethod("Fit",Hidden).Invoke(null,new object[]{-w/2,-h/2,w/2,h/2,x-30,x+30,h/2-20,4,60f,60f});
                float px=Field(panel,"X"),py=Field(panel,"Y"),pw=Field(panel,"Width"),ph=Field(panel,"Height");
                Check(px>=-w/2+15.99f&&px+pw<=w/2-15.99f&&py<=h/2-15.99f&&py-ph>=-h/2+15.99f,"Four-variant panel must fit the canvas");
                float tile=(pw-12-6*3)/4;
                Check(tile>=59.99f&&6+3*(tile+6)+tile<=pw-5.99f,"The fourth T tile must fit at native tile size");
            }
        Console.WriteLine("FS20_FAMILY_INTEGRATION_PASS checks="+checks+"; compiled A/B/C/T pool and four-tile layout, no game launched");
    }
}
