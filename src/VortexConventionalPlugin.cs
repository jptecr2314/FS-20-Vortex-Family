using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Mirage;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    [BepInPlugin(PluginId, "FS-20 Vortex Family", "0.3.11")]
    [BepInDependency("com.nikkorap.blueprinter")]
    public sealed class VortexConventionalPlugin : BaseUnityPlugin
    {
        private const string PluginId = "br.japensou.nuclearoption.vortexctol";
        private const string StockKey = "SmallFighter1";
        private const string VariantKey = "JP_FS20_CTOL";
        private const int PrefabHash = 0x6A20C701;
        private const string Label = "FS-20C Vortex";
        // Budget removed hardware separately from retained structures.
        private const float LiftFanHardwareMass = 300f;
        private const float RollPostHardwareMass = 25f;
        private const float RemovedVtolMass = LiftFanHardwareMass + 2f * RollPostHardwareMass;
        private static readonly FieldInfo HangarTypes = AccessTools.Field(typeof(Hangar), "availableAircraft");
        private static VortexConventionalPlugin instance;
        private AircraftDefinition original;
        private AircraftDefinition variant;
        private AircraftDefinition variantA;
        private Harmony harmony;
        private ConfigEntry<Fs20FlapMode> flapMode;
        internal static Fs20FlapMode FlapMode => instance == null ? Fs20FlapMode.Auto : instance.flapMode.Value;

        private void Awake()
        {
            instance = this;
            flapMode = Config.Bind("FS-20C", "Flap mode", Fs20FlapMode.Auto, "Auto schedules takeoff, approach and combat flaps. Other modes override the schedule; high-speed retraction stays active.");
            var runner = AccessTools.TypeByName("Blueprinter.PatchRunner");
            var apply = runner == null ? null : AccessTools.Method(runner, "ApplyAllOps");
            var hangarAwake = AccessTools.Method(typeof(Hangar), "Awake");
            if (apply == null || hangarAwake == null || HangarTypes == null)
            {
                Logger.LogError("Vortex CTOL: required game or Blueprinter API missing.");
                return;
            }

            harmony = new Harmony(PluginId);
            harmony.Patch(apply, postfix: new HarmonyMethod(typeof(VortexConventionalPlugin), nameof(AfterBlueprinter)) { priority = Priority.Last });
            harmony.Patch(hangarAwake, postfix: new HarmonyMethod(typeof(VortexConventionalPlugin), nameof(AfterHangarAwake)));
            harmony.PatchAll(typeof(Fs20FlightController.FlightSample));
            harmony.PatchAll(typeof(Fs20FlightController.PrepareEnvelope));
            harmony.PatchAll(typeof(Fs20FlightController.ElevonJob));
            harmony.PatchAll(typeof(VtolGauge));
            harmony.PatchAll(typeof(Fs20FamilyMenu.InitializePatch));
            harmony.PatchAll(typeof(Fs20FamilyMenu.RefreshPatch));
            harmony.PatchAll(typeof(Fs20HookFairing.BayInterlock));
            harmony.PatchAll(typeof(Fs20HookFairing.DepartureInterlock));
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }

        private static void AfterBlueprinter(Encyclopedia __0)
        {
            try
            {
                instance.Register(__0);
            }
            catch (Exception error)
            {
                instance.Logger.LogError("Vortex CTOL registration failed: " + error);
            }

            try
            {
                instance.RegisterA(__0);
            }
            catch (Exception error)
            {
                instance.Logger.LogError("FS-20A registration failed: " + error);
            }
        }

        private void RegisterA(Encyclopedia encyclopedia)
        {
            if (variantA != null || encyclopedia == null || Encyclopedia.Lookup == null || encyclopedia.IndexLookup == null)
                return;
            if (original == null)
                original = encyclopedia.aircraft.FirstOrDefault(d => d != null && d.jsonKey == StockKey);
            if (original == null)
                throw new InvalidOperationException("FS-20A donor unavailable.");
            variantA = Fs20AirframeA.Register(encyclopedia, original);
            TryInstallVapor(variantA, false);
            foreach (var hangar in Resources.FindObjectsOfTypeAll<Hangar>())
                AddToHangar(hangar);
            Logger.LogInfo($"Registered FS-20A Vortex ({Fs20AirframeA.Key}), original wings, native thrust, extra internal fuel and A-specific 11 G FBW, " + $"nominal empty {variantA.aircraftInfo.emptyWeight:F0} kg, prefab part mass {variantA.mass:F0} kg, " + $"internal fuel {variantA.unitPrefab.GetComponentsInChildren<FuelTank>(true).Sum(t => t.GetCapacity()):F0} kg; runway configuration awaits flight testing.");
        }

        private void Register(Encyclopedia encyclopedia)
        {
            if (variant != null || encyclopedia == null || Encyclopedia.Lookup == null || encyclopedia.IndexLookup == null || Encyclopedia.Lookup.ContainsKey(VariantKey))
                return;
            original = encyclopedia.aircraft.FirstOrDefault(definition => definition != null && definition.jsonKey == StockKey);
            if (original == null || original.unitPrefab == null || original.aircraftParameters == null)
                throw new InvalidOperationException("Native FS-20 definition is unavailable.");
            if (encyclopedia.IndexLookup.OfType<UnitDefinition>().Any(definition => definition != null && definition.unitPrefab != null && definition.unitPrefab.TryGetComponent<NetworkIdentity>(out var identity) && identity.PrefabHash == PrefabHash))
                throw new InvalidOperationException("Vortex CTOL prefab hash is already in use.");
            GameObject holder = null;
            AircraftDefinition definitionCopy = null;
            AircraftParameters parameters = null;
            try
            {
                holder = new GameObject(VariantKey + " templates");
                holder.SetActive(false);
                DontDestroyOnLoad(holder);
                var prefab = Instantiate(original.unitPrefab, holder.transform, false);
                prefab.name = VariantKey;
                var aircraft = prefab.GetComponent<Aircraft>();
                var network = prefab.GetComponent<NetworkIdentity>();
                var swivel = prefab.GetComponent<SwivelDuctSystem>();
                if (aircraft == null || network == null || swivel == null)
                    throw new InvalidOperationException("Native FS-20 prefab layout changed.");
                Fs20Geometry.Apply(prefab.transform);
                RemoveVtolHardware(prefab.transform, swivel);
                ConfigureMassAndStructure(prefab.transform);
                Fs20Tuning.Configure(prefab.transform);
                prefab.AddComponent<Fs20FlightController>();
                parameters = Instantiate(original.aircraftParameters);
                parameters.aircraftName = Label;
                parameters.verticalLanding = false;
                parameters.takeoffSpeed = 45f;
                // Short-field design target, to be validated on the Cursor.
                parameters.takeoffDistance = 50f;
                parameters.approachSpeed = 72f;
                parameters.landingSpeed = 72f;
                parameters.shortLandingSpeed = 72f;
                parameters.cornerSpeed = 140f;
                parameters.DefaultFuelLevel = 0.35f;
                definitionCopy = Instantiate(original);
                definitionCopy.name = definitionCopy.jsonKey = VariantKey;
                definitionCopy.unitName = Label;
                definitionCopy.code = "FS-20C";
                definitionCopy.description = "Composite-wing Vortex with a lighter fixed engine installation, 110/165 kN thrust, " + "integrated flaperons, adaptive 11 G flight assist and maneuvering thrust vectoring. Conventional landing; experimental short-deck takeoff configuration.";
                definitionCopy.width = Fs20Geometry.Span;
                definitionCopy.aircraftParameters = parameters;
                var stockInfo = original.aircraftInfo;
                definitionCopy.aircraftInfo = new AircraftInfo
                {
                    emptyWeight = Fs20Tuning.EmptyMass,
                    maxSpeed = stockInfo.maxSpeed,
                    stallSpeed = stockInfo.stallSpeed,
                    maneuverability = Fs20FlightEnvelope.MaximumG,
                    maxWeight = stockInfo.maxWeight
                };
                definitionCopy.unitPrefab = prefab;
                ((Unit)aircraft).definition = definitionCopy;
                network.PrefabHash = PrefabHash;
                prefab.SetActive(true);
                definitionCopy.mass = prefab.GetComponentsInChildren<UnitPart>(true).Sum(part => part.GetMass());
                encyclopedia.aircraft.Add(definitionCopy);
                Encyclopedia.Lookup.Add(VariantKey, definitionCopy);
                ((INetworkDefinition)definitionCopy).LookupIndex = encyclopedia.IndexLookup.Count;
                encyclopedia.IndexLookup.Add(definitionCopy);
            }
            catch
            {
                if (definitionCopy != null)
                {
                    encyclopedia.aircraft.Remove(definitionCopy);
                    if (Encyclopedia.Lookup.TryGetValue(VariantKey, out var registered) && registered == definitionCopy)
                        Encyclopedia.Lookup.Remove(VariantKey);
                    encyclopedia.IndexLookup.Remove(definitionCopy);
                    Destroy(definitionCopy);
                }

                if (holder != null)
                    Destroy(holder);
                if (parameters != null)
                    Destroy(parameters);
                throw;
            }

            variant = definitionCopy;
            TryInstallVapor(variant, true);
            Fs20TailHook.Add(encyclopedia, variant);
            foreach (var hangar in Resources.FindObjectsOfTypeAll<Hangar>())
                AddToHangar(hangar);
            Logger.LogInfo($"Registered {Label} ({VariantKey}), span {variant.width:F2} m, " + $"110/165 kN, nominal empty {variant.aircraftInfo.emptyWeight:F0} kg, " + $"prefab part mass {variant.mass:F0} kg; short-deck performance requires flight validation.");
        }

        private void TryInstallVapor(AircraftDefinition definition, bool extendedWing)
        {
            try
            {
                Fs20ManeuverVapor.Install(definition.unitPrefab.transform, extendedWing);
                Logger.LogInfo(definition.code + " maneuver vapor template configured: 4 layers, native material, structural owners bound.");
            }
            catch (Exception error)
            {
                Logger.LogError(definition.code + " wing vapor setup failed: " + error);
            }
        }

        internal static void RemoveVtolHardware(Transform root, SwivelDuctSystem swivel)
        {
            // Remove VTOL-only mounts; retain the bay skin and main nozzle.
            DestroyImmediate(swivel);
            RemoveEquipment(root, "liftFan", LiftFanHardwareMass, "liftFan_blades", "liftFan_thrustTransform");
            RemoveEquipment(root, "wing1a_L/wing2a_L", RollPostHardwareMass, "rollPost_L");
            RemoveEquipment(root, "wing1a_R/wing2a_R", RollPostHardwareMass, "rollPost_R");
        }

        [HarmonyPatch(typeof(NozzleGauge), "Refresh")]
        private static class VtolGauge
        {
            private static bool Prefix(NozzleGauge __instance)
            {
                if (__instance.GetComponent<Fs20HiddenGauge>() != null)
                    return false;
                if (!GameManager.GetLocalAircraft(out var local) || local == null || (local.GetComponent<Fs20FlightController>() == null && local.GetComponent<Fs20ConventionalAirframe>() == null))
                    return true;
                // HUDAppManager retains serialized references to these widgets.
                // Hide the VTOL gauge while preserving the manager's app list.
                __instance.gameObject.AddComponent<Fs20HiddenGauge>();
                __instance.gameObject.SetActive(false);
                return false;
            }
        }

        private static void RemoveEquipment(Transform root, string path, float mass, params string[] hardware)
        {
            var mount = root.Find(path);
            var part = mount == null ? null : mount.GetComponent<AeroPart>();
            if (part == null || part.mass <= mass)
                throw new InvalidOperationException("Invalid Vortex VTOL assembly mass: " + path);
            foreach (var name in hardware)
            {
                var component = mount.Find(name);
                if (component == null)
                    throw new InvalidOperationException("Vortex VTOL component is missing: " + name);
                DestroyImmediate(component.gameObject);
            }

            // Set part mass before Awake and definition mass caching.
            part.mass -= mass;
        }

        private static void ConfigureMassAndStructure(Transform root)
        {
            // C mass budget: 350 kg VTOL + 400 kg engine/nozzle + 450 kg wings.
            root.Find("engine").GetComponent<AeroPart>().mass -= 200f;
            root.Find("engine/nozzle").GetComponent<AeroPart>().mass -= 200f;
            var wings = Fs20Geometry.WingPaths.SelectMany(path => root.Find(path).GetComponentsInChildren<AeroPart>(true)).ToArray();
            float wingMass = wings.Sum(part => part.mass);
            float ratio = (wingMass - 450f) / wingMass;
            var jointsField = Fs20Tuning.Field(typeof(AeroPart), "joints");
            foreach (var part in wings)
            {
                part.mass *= ratio;
                // Allow for the longer moment arm while retaining breakaway joints.
                foreach (var joint in (Array)jointsField.GetValue(part))
                {
                    foreach (string name in new[]
                    {
                        "breakForce",
                        "breakTorque"
                    }

                    )
                    {
                        var field = Fs20Tuning.Field(joint.GetType(), name);
                        field.SetValue(joint, (float)field.GetValue(joint) * 1.25f);
                    }
                }
            }
        }

        private static void AfterHangarAwake(Hangar __instance)
        {
            instance.AddToHangar(__instance);
        }

        private void AddToHangar(Hangar hangar)
        {
            if (hangar == null || original == null)
                return;
            var available = HangarTypes.GetValue(hangar) as AircraftDefinition[];
            if (available == null || !available.Contains(original))
                return;
            var updated = available.ToList();
            if (variant != null && !updated.Contains(variant))
                updated.Add(variant);
            if (variantA != null && !updated.Contains(variantA) && !(hangar.attachedUnit is Ship) && hangar.GetComponentInParent<Ship>() == null)
                updated.Add(variantA);
            if (updated.Count != available.Length)
                HangarTypes.SetValue(hangar, updated.ToArray());
        }
    }

    public sealed class Fs20HiddenGauge : MonoBehaviour
    {
    }
}
