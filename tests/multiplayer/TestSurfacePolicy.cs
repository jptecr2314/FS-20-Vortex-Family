using System;
using System.Linq;
using Japensou.NuclearOption.VortexConventional;

internal static class SurfaceChecks
{
    private static int checks;
    private static void Check(bool ok) { if (!ok) throw new Exception("Surface policy failure at " + checks); checks++; }
    private static void Invalid(Action action) { try { action(); } catch (FormatException) { checks++; return; } throw new Exception("Malformed surface packet accepted"); }
    private static void Main()
    {
        var state = new Fs20SurfaceMessage { NetId=57, Sequence=0xffffffff, PartIds=new byte[]{0,23,254},
            Lift=new[]{12f,-7f,0f}, Area=new[]{3f,2f,0f}, Slats=new[]{0f,.5f,1f}, Hooks=new[]{.5f}, HookFlags=new byte[]{7} };
        byte[] bytes=state.Encode(); var decoded=Fs20SurfaceMessage.Decode(bytes);
        Check(decoded.NetId==57 && decoded.Sequence==0xffffffff && decoded.Lift.SequenceEqual(state.Lift) && decoded.Slats.SequenceEqual(state.Slats));
        Check(decoded.PartIds.SequenceEqual(state.PartIds) && decoded.HookFlags.SequenceEqual(state.HookFlags));
        for(int i=0;i<bytes.Length;i++){int count=i;Invalid(()=>Fs20SurfaceMessage.Decode(bytes.Take(count).ToArray()));}
        Invalid(()=>Fs20SurfaceMessage.Decode(bytes.Concat(new byte[]{0}).ToArray()));
        state.Lift[0]=float.NaN;Invalid(()=>state.Encode());state.Lift[0]=float.PositiveInfinity;Invalid(()=>state.Encode());state.Lift[0]=12f;
        state.Area[0]=-1;Invalid(()=>state.Encode());state.Area[0]=3;
        state.PartIds[1]=0;Invalid(()=>state.Encode());state.PartIds[1]=23;
        state.Slats[0]=1.01f;Invalid(()=>state.Encode());state.Slats[0]=0;
        state.HookFlags[0]=8;Invalid(()=>state.Encode());state.HookFlags[0]=7;
        state.NetId=0;Invalid(()=>state.Encode());state.NetId=57;
        Check(Fs20SurfaceMessage.Newer(0,0xffffffff));Check(!Fs20SurfaceMessage.Newer(57,57));Check(!Fs20SurfaceMessage.Newer(56,57));
        state.PartIds=Enumerable.Range(0,64).Select(i=>(byte)i).ToArray();state.Lift=new float[64];state.Area=new float[64];state.Slats=new float[8];state.Hooks=new float[8];state.HookFlags=new byte[8];
        Check(state.Encode().Length<=Fs20SurfaceMessage.MaxBytes);Check(Fs20SurfaceMessage.Decode(state.Encode()).PartIds.Length==64);
        Console.WriteLine("FS20_SURFACE_POLICY_PASS checks="+checks+" maxBytes="+state.Encode().Length+" updatesPerSecond=5");
    }
}
