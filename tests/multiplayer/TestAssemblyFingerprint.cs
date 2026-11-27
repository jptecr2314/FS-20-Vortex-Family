using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Japensou.NuclearOption.VortexConventional;

internal static class TestAssemblyFingerprint
{
    static int checks;
    static void Check(bool value,string why){checks++;if(!value)throw new Exception(why);}
    static void Reject(Action action,string label){try{action();}catch(InvalidOperationException e){Check(e.Message.Contains(label),"Actionable error: "+e.Message);return;}throw new Exception("Invalid file accepted");}
    static int Main(string[] args)
    {
        var a=Path.GetFullPath(args[0]); var b=Path.GetFullPath(args[1]); var other=Path.GetFullPath(args[2]);
        var memory=Assembly.Load(File.ReadAllBytes(a));
        Check(memory.Location=="","Regression setup is an actual byte-loaded assembly");
        bool oldFailed=false;try{using(var stream=File.OpenRead(memory.Location)){} }catch(ArgumentException){oldFailed=true;}
        Check(oldFailed,"Previous OpenRead(Location) fails");
        using(var hash=SHA256.Create())
        {
            string expected;using(var stream=File.OpenRead(a))expected=Convert.ToBase64String(hash.ComputeHash(stream));
            Check(Fs20AssemblyFingerprint.Digest(memory,a,"byte-loaded plugin",hash)==expected,"Plugin loader path preserves full SHA256");
            Check(Fs20AssemblyFingerprint.Digest(memory,a,"byte-loaded game",hash)==expected,"Managed game fallback preserves full SHA256");
            Check(Fs20AssemblyFingerprint.Digest(memory,b,"changed module",hash)!=expected,"Changed bytes with same assembly identity still mismatch");
            Reject(()=>Fs20AssemblyFingerprint.Digest(memory,null,"missing loader path",hash),"missing loader path");
            Reject(()=>Fs20AssemblyFingerprint.Digest(memory," ","blank loader path",hash),"blank loader path");
            Reject(()=>Fs20AssemblyFingerprint.Digest(memory,a+".absent","missing file",hash),"missing file");
            Reject(()=>Fs20AssemblyFingerprint.Digest(memory,other,"wrong identity",hash),"wrong identity");
            Reject(()=>Fs20AssemblyFingerprint.Digest(memory,args[3],"not an assembly",hash),"not an assembly");
            var disk=Assembly.LoadFrom(a);
            Check(Fs20AssemblyFingerprint.Digest(disk,other,"disk module",hash)==expected,"Valid disk assembly location takes precedence over fallback");
            string nested=Path.Combine(Path.GetDirectoryName(a),"space and Unicode ç",Path.GetFileName(a));Directory.CreateDirectory(Path.GetDirectoryName(nested));File.Copy(a,nested);
            Check(Fs20AssemblyFingerprint.Digest(memory,nested,"relocated peer",hash)==expected,"Paths and installation folder names are absent from digest");
        }
        Console.WriteLine(checks+" assembly fingerprint regression checks passed; actual byte-loaded and disk-loaded .NET assemblies");return 0;
    }
}
