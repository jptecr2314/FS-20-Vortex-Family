using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20AssemblyFingerprint
    {
        // BepInEx prepatching loads assemblies from bytes. Location is then
        // empty; the loader's registered path (or managed game path) is the
        // authoritative fallback, not a recursive search by DLL filename.
        internal static string Resolve(Assembly assembly, string registeredPath, string label)
        {
            if (assembly == null) throw new InvalidOperationException("FS-20 cannot identify " + label + ".");
            string location = null;
            try { location = assembly.Location; } catch (NotSupportedException) { }
            string path = !string.IsNullOrWhiteSpace(location) ? location : registeredPath;
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("FS-20 cannot locate " + label + ": the assembly is in memory and its loader path is unavailable. Check that mod's installation.");
            try
            {
                path = Path.GetFullPath(path);
                var disk = AssemblyName.GetAssemblyName(path);
                if (!string.Equals(disk.FullName, assembly.GetName().FullName, StringComparison.Ordinal))
                    throw new InvalidDataException("Assembly identity differs from the loaded module.");
                return path;
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is ArgumentException || error is BadImageFormatException || error is NotSupportedException)
            {
                throw new InvalidOperationException("FS-20 cannot verify " + label + ": " + error.Message, error);
            }
        }

        internal static string Digest(Assembly assembly, string registeredPath, string label, HashAlgorithm hash)
        {
            string path = Resolve(assembly, registeredPath, label);
            try { using (var stream = File.OpenRead(path)) return Convert.ToBase64String(hash.ComputeHash(stream)); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            { throw new InvalidOperationException("FS-20 cannot read " + label + " for multiplayer compatibility: " + error.Message, error); }
        }
    }
}
