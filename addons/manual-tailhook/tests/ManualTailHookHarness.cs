// Offline integration harness: real mod source + real Harmony; simulated Unity/game objects.
// This verifies scope and state transitions, not Unity animation, physics or multiplayer.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Japensou.NuclearOption.ManualTailHook;

namespace UnityEngine
{
    public class Object
    {
        public static readonly List<Object> Objects = new List<Object>();
        public Object() { Objects.Add(this); }
        public static T[] FindObjectsOfType<T>() where T : Object => Objects.FindAll(o => o is T).ConvertAll(o => (T)o).ToArray();
    }
    public class GameObject : Object
    {
        public bool activeInHierarchy = true;
        public readonly List<Component> Components = new List<Component>();
        public T GetComponentInParent<T>() where T : Component => Components.Find(c => c is T) as T;
    }
    public class Component : Object
    {
        public GameObject gameObject = new GameObject();
        public T GetComponent<T>() where T : Component => GetComponent(typeof(T)) as T;
        public Component GetComponent(Type type) => gameObject.Components.Find(c => type.IsInstanceOfType(c));
        public T[] GetComponentsInChildren<T>(bool inactive) where T : Component => gameObject.Components.FindAll(c => c is T).ConvertAll(c => (T)c).ToArray();
    }
    public class Behaviour : Component { public bool enabled = true; }
    public static class Application { public static bool isFocused = true; }
    public static class Time { public static float timeScale = 1f; }
    public enum KeyCode { None, H, LeftControl, LeftShift }
}
namespace UnityEngine.EventSystems
{
    public class EventSystem { public static EventSystem current; public UnityEngine.GameObject currentSelectedGameObject; }
}
namespace UnityEngine.UI { public class InputField : UnityEngine.Component { public bool isFocused; } }
namespace TMPro { public class TMP_InputField : UnityEngine.Component { public bool isFocused; } }
namespace BepInEx.Configuration
{
    public class ConfigEntry<T> { public T Value; }
    public class ConfigFile
    {
        public ConfigEntry<T> Bind<T>(string section, string key, T value, string description) => new ConfigEntry<T> { Value = value };
    }
    public struct KeyboardShortcut
    {
        private readonly bool restore;
        public static int Pending;
        public KeyboardShortcut(UnityEngine.KeyCode key, params UnityEngine.KeyCode[] modifiers) { restore = modifiers.Length == 2; }
        public bool IsDown()
        {
            if (Pending != (restore ? 2 : 1)) return false;
            Pending = 0; return true;
        }
    }
}
namespace BepInEx
{
    [AttributeUsage(AttributeTargets.Class)] public class BepInPlugin : Attribute { public BepInPlugin(string id, string name, string version) { } }
    [AttributeUsage(AttributeTargets.Class)] public class BepInDependency : Attribute
    {
        public enum DependencyFlags { SoftDependency }
        public BepInDependency(string id, DependencyFlags flags) { }
    }
    public class Logger { public void LogInfo(object message) { } public void LogError(object message) { throw new Exception(message.ToString()); } }
    public class BaseUnityPlugin : UnityEngine.Behaviour
    {
        protected Logger Logger = new Logger();
        protected BepInEx.Configuration.ConfigFile Config = new BepInEx.Configuration.ConfigFile();
    }
}
public class Unit : UnityEngine.Component { }
public class Aircraft : Unit { }
public class UnitPart : UnityEngine.Component
{
    public Unit parentUnit;
    public bool Detached;
    public bool IsDetached() => Detached;
}
public class TailHook : UnityEngine.Behaviour
{
    private bool deployed, hooked;
    private Aircraft aircraft;
    public UnitPart unitPart;
    public int AutomaticCalls, AnimationCalls;
    public bool IsDown => deployed;
    public bool IsHooked => hooked;
    public TailHook(Aircraft owner)
    {
        aircraft = owner; unitPart = new UnitPart { parentUnit = owner };
        owner.gameObject.Components.Add(this); gameObject.Components.Add(this);
    }
    public void Catch() { hooked = true; }
    public void Unhook() { hooked = false; }
    [MethodImpl(MethodImplOptions.NoInlining)] public void CheckDeployConditions() { AutomaticCalls++; deployed = false; }
    [MethodImpl(MethodImplOptions.NoInlining)] public void FixedUpdate() { AnimationCalls++; }
}
public class ArrestorGear : UnityEngine.Behaviour
{
    private TailHook attachedHook;
    public bool Attached => attachedHook != null;
    public int ReleaseCalls;
    [MethodImpl(MethodImplOptions.NoInlining)] public bool Hook(TailHook hook) { attachedHook = hook; hook.Catch(); return true; }
    public void Unhook() { ReleaseCalls++; attachedHook.Unhook(); attachedHook = null; }
}
public static class GameManager
{
    public static Aircraft Local;
    public static bool flightControlsEnabled = true;
    public static bool GetLocalAircraft(out Aircraft local) { local = Local; return local != null; }
}
public class AircraftActionsReport
{
    public static AircraftActionsReport i = new AircraftActionsReport();
    public string Last;
    public void ReportText(string text, float seconds) { Last = text; }
}
namespace Japensou.NuclearOption.VortexConventional
{
    public class Fs20HookDepartureLock
    {
        public bool Locked { [MethodImpl(MethodImplOptions.NoInlining)] get { return true; } }
    }
    public class Fs20HookFairing : UnityEngine.Component
    {
        private readonly Fs20HookDepartureLock departure = new Fs20HookDepartureLock();
        public Fs20HookDepartureLock Lock => departure;
        public int Calls;
        [MethodImpl(MethodImplOptions.NoInlining)] public void EnforceDepartureLock() { Calls++; }
    }
}
public static class ManualTailHookHarness
{
    private static int assertions;
    private static void Check(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
    private static void Invoke(object plugin, string method) => plugin.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(plugin, null);
    private static void Key(object plugin, int key) { BepInEx.Configuration.KeyboardShortcut.Pending = key; Invoke(plugin, "Update"); BepInEx.Configuration.KeyboardShortcut.Pending = 0; }
    public static void Main()
    {
        var plugin = new ManualTailHookPlugin(); Invoke(plugin, "Awake");
        var aircraft = new Aircraft(); var hook = new TailHook(aircraft); GameManager.Local = aircraft;
        Invoke(plugin, "Update"); hook.CheckDeployConditions();
        Check(hook.AutomaticCalls == 1, "automatic until first manual command");
        Key(plugin, 1); Check(hook.IsDown && hook.enabled, "manual deploy wakes native animation");
        hook.CheckDeployConditions(); hook.FixedUpdate();
        Check(hook.IsDown && hook.AutomaticCalls == 1 && hook.AnimationCalls == 1, "manual holds while native animation still runs");
        var ai = new TailHook(new Aircraft()); ai.CheckDeployConditions();
        Check(ai.AutomaticCalls == 1, "AI must retain automatic control");
        var wire = new ArrestorGear(); var otherWire = new ArrestorGear();
        Check(wire.Hook(hook), "deployed hook may catch native cable"); otherWire.Hook(ai);
        Key(plugin, 1);
        Check(!hook.IsDown && !hook.IsHooked && !wire.Attached && wire.ReleaseCalls == 1, "retract releases both ends of cable");
        Check(otherWire.Attached && ai.IsHooked, "other aircraft cable unchanged");
        Check(!wire.Hook(hook) && !wire.Attached, "retracting hook cannot recatch cable");
        Key(plugin, 2); hook.CheckDeployConditions();
        Check(hook.AutomaticCalls == 2 && wire.Hook(hook), "restore returns automatic and native catch behavior");
        Key(plugin, 1); // retract hooked hook and take manual control
        GameManager.Local = new Aircraft(); hook.CheckDeployConditions();
        Check(hook.AutomaticCalls == 3, "old aircraft returns automatic immediately after ownership changes");
        Invoke(plugin, "Update"); var next = new TailHook(GameManager.Local); next.CheckDeployConditions();
        Check(next.AutomaticCalls == 1, "new aircraft starts automatic");
        UnityEngine.Application.isFocused = false; Key(plugin, 1); Check(!next.IsDown, "unfocused ignores key"); UnityEngine.Application.isFocused = true;
        UnityEngine.Time.timeScale = 0; Key(plugin, 1); Check(!next.IsDown, "paused ignores key"); UnityEngine.Time.timeScale = 1;
        GameManager.flightControlsEnabled = false; Key(plugin, 1); Check(!next.IsDown, "menus ignore key"); GameManager.flightControlsEnabled = true;
        var field = new TMPro.TMP_InputField { isFocused = true };
        field.gameObject.Components.Add(field);
        UnityEngine.EventSystems.EventSystem.current = new UnityEngine.EventSystems.EventSystem { currentSelectedGameObject = field.gameObject };
        Key(plugin, 1); Check(!next.IsDown, "chat input ignores key"); field.isFocused = false;
        next.unitPart.Detached = true; Key(plugin, 1); Check(!next.IsDown, "detached hook excluded"); next.unitPart.Detached = false;
        var fairing = new Japensou.NuclearOption.VortexConventional.Fs20HookFairing { gameObject = next.gameObject };
        next.gameObject.Components.Add(fairing);
        Key(plugin, 1); fairing.EnforceDepartureLock();
        Check(next.IsDown && fairing.Calls == 0 && !fairing.Lock.Locked, "FS-20C local manual bypasses departure lock");
        var unrelatedLock = new Japensou.NuclearOption.VortexConventional.Fs20HookDepartureLock();
        Check(unrelatedLock.Locked, "unrelated departure lock unchanged");
        Key(plugin, 2); fairing.EnforceDepartureLock();
        Check(fairing.Calls == 1 && fairing.Lock.Locked, "FS-20C automatic restores original interlock");
        Invoke(plugin, "OnDestroy"); next.CheckDeployConditions();
        Check(next.AutomaticCalls == 2, "unload removes patches");
        Console.WriteLine("PASS: " + assertions + " offline assertions with real Harmony; Unity/physics not exercised.");
    }
}
