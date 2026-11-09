using System;
using System.IO;
using System.Linq;
using System.Reflection;

internal static class StockConfigChecks
{
    private static void Main(string[] args)
    {
        string plugin = Path.GetFullPath(args[0]), game = Path.GetFullPath(args[1]);
        string[] references = { Path.GetDirectoryName(plugin), Path.Combine(game, "BepInEx/core"), Path.Combine(game, "NuclearOption_Data/Managed") };
        AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) =>
        {
            string name = new AssemblyName(eventArgs.Name).Name + ".dll";
            string path = references.Select(folder => Path.Combine(folder, name)).FirstOrDefault(File.Exists);
            return path == null ? null : Assembly.LoadFrom(path);
        };
        var assembly = Assembly.LoadFrom(plugin);
        var type = assembly.GetType("Japensou.NuclearOption.SharedAirframes.SharedAirframeStock", true);
        var configType = Assembly.Load("BepInEx").GetType("BepInEx.Configuration.ConfigFile", true);
        object config = Activator.CreateInstance(configType, new object[] { args[2], false, null });
        configType.GetProperty("SaveOnConfigSet").SetValue(config, false);
        var flags = BindingFlags.NonPublic | BindingFlags.Static;
        object entry = type.GetMethod("Configure", flags).Invoke(null, new[] { config });
        var value = entry.GetType().GetProperty("Value");
        var installed = type.GetField("installed", flags);
        var enabled = type.GetProperty("Enabled", flags);
        Action<bool> check = expected =>
        {
            if ((bool)enabled.GetValue(null) != expected) throw new Exception("Stock toggle mismatch");
        };
        try
        {
            installed.SetValue(null, true);
            value.SetValue(entry, true); check(true);
            value.SetValue(entry, false); check(false);
            configType.GetMethod("Save").Invoke(config, null);
            object reloaded = Activator.CreateInstance(configType, new object[] { args[2], false, null });
            var bind = configType.GetMethods().Single(m => m.Name == "Bind" && m.IsGenericMethod &&
                m.GetParameters().Length == 4 && m.GetParameters()[3].ParameterType == typeof(string));
            object saved = bind.MakeGenericMethod(typeof(bool)).Invoke(reloaded,
                new object[] { "Inventory", "Shared airframe stock", true, "Test reload" });
            if ((bool)value.GetValue(saved)) throw new Exception("Saved OFF value was not retained");
            value.SetValue(entry, true); check(true);
            installed.SetValue(null, false); check(false);
        }
        finally { installed.SetValue(null, false); }
        Console.WriteLine("PASS actual ConfigEntry ON/OFF/ON, persistence and installed guard; no game launched");
    }
}
