using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Japensou.NuclearOption.ManualTailHook
{
    [BepInPlugin(Id, "NO Manual Tailhook", Version)]
    [BepInDependency("br.japensou.nuclearoption.vortexctol", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class ManualTailHookPlugin : BaseUnityPlugin
    {
        public const string Id = "br.japensou.nuclearoption.manualtailhook";
        public const string Version = "0.1.1";
        private static ManualTailHookPlugin instance;
        private static readonly FieldInfo Deployed = AccessTools.Field(typeof(TailHook), "deployed");
        private static readonly FieldInfo Hooked = AccessTools.Field(typeof(TailHook), "hooked");
        private static readonly FieldInfo HookAircraft = AccessTools.Field(typeof(TailHook), "aircraft");
        private static readonly FieldInfo AttachedHook = AccessTools.Field(typeof(ArrestorGear), "attachedHook");
        private Harmony harmony;
        private ConfigEntry<KeyboardShortcut> toggle, automatic;
        private Aircraft controlled;
        private TailHook[] controlledHooks = Array.Empty<TailHook>();
        private bool? command;
        private Type fairingType;
        private FieldInfo departureField;

        private void Awake()
        {
            instance = this;
            toggle = Config.Bind("Controls", "Toggle tailhook", new KeyboardShortcut(KeyCode.H, KeyCode.LeftControl),
                "Deploy or retract the tailhook. The first command takes manual control until automatic mode is restored or you change aircraft.");
            automatic = Config.Bind("Controls", "Restore automatic", new KeyboardShortcut(KeyCode.H, KeyCode.LeftControl, KeyCode.LeftShift),
                "Restore the game's original automatic tailhook control.");
            try
            {
                Require(Deployed, Hooked, HookAircraft, AttachedHook);
                harmony = new Harmony(Id);
                Patch(typeof(TailHook), "CheckDeployConditions", nameof(BeforeAutomatic));
                Patch(typeof(TailHook), "FixedUpdate", nameof(BeforeAnimation));
                Patch(typeof(ArrestorGear), "Hook", nameof(BeforeCatch));
                InstallVortexBridge();
                Logger.LogInfo("Manual tailhook ready: Ctrl+H toggle; Ctrl+Shift+H automatic. Local pilot only.");
            }
            catch (Exception error)
            {
                harmony?.UnpatchSelf();
                instance = null;
                enabled = false;
                Logger.LogError("Manual tailhook disabled: " + error);
            }
        }

        private static void Require(params FieldInfo[] fields)
        {
            foreach (var field in fields)
                if (field == null) throw new MissingFieldException("Tailhook API changed; required field unavailable.");
        }

        private void Patch(Type type, string method, string prefix)
        {
            var target = AccessTools.Method(type, method);
            if (target == null) throw new MissingMethodException(type.FullName, method);
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(ManualTailHookPlugin), prefix) { priority = Priority.Last });
        }

        private void InstallVortexBridge()
        {
            fairingType = AccessTools.TypeByName("Japensou.NuclearOption.VortexConventional.Fs20HookFairing");
            if (fairingType == null) return;
            departureField = AccessTools.Field(fairingType, "departure");
            Require(departureField);
            var locked = AccessTools.PropertyGetter(departureField.FieldType, "Locked");
            if (locked == null) throw new MissingMethodException("FS-20C hook interlock API changed.");
            Patch(fairingType, "EnforceDepartureLock", nameof(BeforeFairingInterlock));
            harmony.Patch(locked, postfix: new HarmonyMethod(typeof(ManualTailHookPlugin), nameof(AfterDepartureLock)));
            Logger.LogInfo("FS-20C manual hook bridge enabled; native bay-door animation retained.");
        }

        private void Update()
        {
            Aircraft local;
            if (!GameManager.GetLocalAircraft(out local)) local = null;
            if (controlled != local)
            {
                controlled = local;
                command = null;
                controlledHooks = Array.Empty<TailHook>();
            }
            if (controlled == null || !CanAcceptInput()) return;
            if (automatic.Value.IsDown())
            {
                command = null;
                // The native slow update will reevaluate conditions (up to one second).
                Report("Tailhook: AUTOMATIC");
            }
            else if (toggle.Value.IsDown()) Toggle();
        }

        private static bool CanAcceptInput()
        {
            if (!Application.isFocused || !GameManager.flightControlsEnabled || Time.timeScale <= 0f) return false;
            var selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            if (selected == null) return true;
            var text = selected.GetComponentInParent<InputField>();
            var tmp = selected.GetComponentInParent<TMP_InputField>();
            return (text == null || !text.isFocused) && (tmp == null || !tmp.isFocused);
        }

        private bool Owns(TailHook hook)
        {
            Aircraft local;
            return controlled != null && GameManager.GetLocalAircraft(out local) && local == controlled &&
                hook != null && hook.gameObject.activeInHierarchy && hook.unitPart != null &&
                hook.unitPart.parentUnit == controlled && !hook.unitPart.IsDetached() &&
                HookAircraft.GetValue(hook) as Aircraft == controlled;
        }

        private static bool IsManual(TailHook hook) => instance != null && instance.command.HasValue && instance.Owns(hook);

        private void Toggle()
        {
            var hooks = controlled.GetComponentsInChildren<TailHook>(true);
            controlledHooks = hooks;
            bool found = false, extended = false;
            foreach (var hook in hooks)
            {
                if (!Owns(hook)) continue;
                found = true;
                extended |= (bool)Deployed.GetValue(hook) || (bool)Hooked.GetValue(hook);
            }
            if (!found) { Report("Tailhook: no hook equipped"); return; }
            command = !extended;
            foreach (var hook in hooks) if (Owns(hook)) Apply(hook);
            Report(command.Value ? "Tailhook: DEPLOYED (manual)" : "Tailhook: RETRACTED (manual)");
        }

        private void Apply(TailHook hook)
        {
            if (!command.Value && (bool)Hooked.GetValue(hook))
            {
                // TailHook.Unhook alone leaves ArrestorGear applying cable force.
                // Release both sides through the native arrestor API, only on command.
                foreach (var wire in UnityEngine.Object.FindObjectsOfType<ArrestorGear>())
                    if (AttachedHook.GetValue(wire) as TailHook == hook) wire.Unhook();
                hook.Unhook();
            }
            Deployed.SetValue(hook, command.Value);
            hook.enabled = true; // A stowed hook may have disabled its native animation.
        }

        private static bool BeforeAutomatic(TailHook __instance)
        {
            if (!IsManual(__instance)) return true;
            instance.Apply(__instance);
            return false;
        }

        private static void BeforeAnimation(TailHook __instance)
        {
            if (IsManual(__instance)) instance.Apply(__instance);
        }

        private static bool BeforeCatch(TailHook __0, ref bool __result)
        {
            if (!IsManual(__0) || instance.command.Value) return true;
            __result = false; // Do not immediately recatch a cable during retraction.
            return false;
        }

        private static bool BeforeFairingInterlock(Component __instance)
        {
            return !IsManual(__instance.GetComponent<TailHook>());
        }

        private static void AfterDepartureLock(object __instance, ref bool __result)
        {
            if (instance == null || !instance.command.HasValue || instance.controlled == null) return;
            foreach (var hook in instance.controlledHooks)
            {
                if (!IsManual(hook)) continue;
                var fairing = hook.GetComponent(instance.fairingType);
                if (fairing != null && ReferenceEquals(instance.departureField.GetValue(fairing), __instance))
                { __result = false; return; }
            }
        }

        private void Report(string message)
        {
            Logger.LogInfo(message);
            if (AircraftActionsReport.i != null) AircraftActionsReport.i.ReportText(message, 4f);
        }

        private void OnDestroy()
        {
            command = null;
            harmony?.UnpatchSelf();
            if (instance == this) instance = null;
        }
    }
}
