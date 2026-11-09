using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

internal static class SinglePlayerChecks
{
    private static int checks;
    private const BindingFlags HiddenStatic = BindingFlags.NonPublic | BindingFlags.Static;
    private static void Check(bool ok, string message)
    {
        checks++;
        if (!ok)
            throw new Exception(message);
    }

    private static void Main(string[] args)
    {
        string[] folders =
        {
            Path.GetDirectoryName(Path.GetFullPath(args[0])),
            Path.Combine(args[1], "BepInEx/core"),
            Path.Combine(args[1], "NuclearOption_Data/Managed")
        };
        AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) =>
        {
            string name = new AssemblyName(eventArgs.Name).Name + ".dll";
            string file = folders.Select(folder => Path.Combine(folder, name)).FirstOrDefault(File.Exists);
            return file == null ? null : Assembly.LoadFrom(file);
        };
        var plugin = Assembly.LoadFrom(args[0]);
        var guard = plugin.GetType("Japensou.NuclearOption.VortexConventional.Fs20SinglePlayer", true);
        foreach (string name in new[]
        {
            "MenuGate",
            "ClientGate",
            "HostGate",
            "TransportClientGate",
            "TransportServerGate"
        }

        )
        {
            var patch = guard.GetNestedType(name, BindingFlags.NonPublic);
            var attribute = patch.GetCustomAttributesData().Single(a => a.AttributeType.Name == "HarmonyPatch");
            var targetType = (Type)attribute.ConstructorArguments[0].Value;
            string method = (string)attribute.ConstructorArguments[1].Value;
            var targets = targetType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Where(m => m.Name == method).ToArray();
            Check(targets.Length == 1, name + " binds exactly one native method");
            var prefix = patch.GetMethod("Prefix", HiddenStatic);
            if (name == "HostGate")
            {
                var optionType = targets[0].GetParameters()[0].ParameterType;
                var socket = optionType.GetField("SocketType");
                foreach (object transport in Enum.GetValues(socket.FieldType).Cast<object>().Concat(new[] { Enum.ToObject(socket.FieldType, 999) }))
                {
                    object options = FormatterServices.GetUninitializedObject(optionType);
                    socket.SetValue(options, transport);
                    object[] call =
                    {
                        options,
                        Activator.CreateInstance(targets[0].ReturnType)
                    };
                    bool allowed = (bool)prefix.Invoke(null, call);
                    Check(allowed == (transport.ToString() == "Offline"), "Only explicit Offline hosting is permitted: " + transport);
                }
            }
            else if (name == "TransportServerGate")
            {
                object server = FormatterServices.GetUninitializedObject(targetType);
                var listening = targetType.GetField("Listening");
                listening.SetValue(server, false);
                Check((bool)prefix.Invoke(null, new[] { server }), "Local non-listening server is permitted");
                listening.SetValue(server, true);
                Check(!(bool)prefix.Invoke(null, new[] { server }), "Listening server is refused before startup");
            }
            else
                Check(!(bool)prefix.Invoke(null, null), name + " blocks online entry");
        }

        Check(plugin.GetTypes().Count(t => t.GetCustomAttributesData().Any(a => a.AttributeType.Name == "BepInPlugin")) == 1, "One integrated plugin");
        Console.WriteLine("SINGLEPLAYER_GATE_PASS checks=" + checks + "; actual prefixes and native contracts; no sockets or game launched");
    }
}
