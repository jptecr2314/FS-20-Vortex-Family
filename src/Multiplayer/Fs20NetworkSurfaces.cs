using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirage;
using Mirage.Serialization;
using UnityEngine;
using global::NuclearOption.Jobs;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20NetworkSurfaces
    {
        internal static readonly FieldInfo Layers = AccessTools.Field(typeof(Fs20TVapor), "layers");
        internal static readonly FieldInfo TDeployment = AccessTools.Field(typeof(Fs20THighLift), "deployment");
        internal static readonly FieldInfo TSlats = AccessTools.Field(typeof(Fs20THighLift), "slats");
        internal static readonly FieldInfo Position = AccessTools.Field(typeof(HighLiftDevice), "position");
        internal static readonly FieldInfo Moving = AccessTools.Field(typeof(HighLiftDevice), "movingParts");
        internal static readonly FieldInfo Amount = AccessTools.Field(typeof(TailHook), "deployedAmount");
        internal static readonly FieldInfo Deployed = AccessTools.Field(typeof(TailHook), "deployed");
        internal static readonly FieldInfo Hooked = AccessTools.Field(typeof(TailHook), "hooked");
        private static readonly FieldInfo Observers = AccessTools.Field(typeof(NetworkIdentity), "observers");
        internal static bool Family(Aircraft aircraft) => aircraft != null && aircraft.definition != null &&
            (aircraft.definition.jsonKey == Fs20VaporDriver.CKey || aircraft.definition.jsonKey == Fs20TAirframe.Key);

        internal static void Install(Harmony harmony)
        {
            int id = MessagePacker.GetId<Fs20SurfaceMessage>();
            if (MessagePacker.MessageTypes.TryGetValue(id, out var other) && other != typeof(Fs20SurfaceMessage))
                throw new InvalidOperationException("FS-20 surface message ID collision.");
            MessagePacker.RegisterMessage<Fs20SurfaceMessage>();
            Writer<Fs20SurfaceMessage>.Write = (writer, state) => {
                var bytes = state.Encode(); writer.WriteUInt16((ushort)bytes.Length); writer.WriteBytes(bytes, 0, bytes.Length);
            };
            Reader<Fs20SurfaceMessage>.Read = reader => {
                int length = reader.ReadUInt16();
                if (length < 11 || length > Fs20SurfaceMessage.MaxBytes || !reader.CanReadBytes(length)) throw new FormatException("Truncated FS-20 surface packet.");
                var bytes = new byte[length]; reader.ReadBytes(bytes, 0, length); return Fs20SurfaceMessage.Decode(bytes);
            };
            foreach (var patch in new[] { typeof(AircraftTick), typeof(THighLiftAuthority), typeof(CHighLiftAuthority), typeof(HookTickAuthority),
                typeof(HookDepartureAuthority), typeof(HookDeployAuthority), typeof(CompletedLoads) }) harmony.PatchAll(patch);
        }
        internal static void Register(MessageHandler handler, bool server)
        {
            var handlers = (IDictionary)AccessTools.Field(typeof(MessageHandler), "_messageHandlers").GetValue(handler);
            if (handlers.Contains(MessagePacker.GetId<Fs20SurfaceMessage>())) throw new InvalidOperationException("FS-20 surface handler collision.");
            if (server) handler.RegisterHandler<Fs20SurfaceMessage>(ReceiveServer, allowUnauthenticated: false);
            else handler.RegisterHandler<Fs20SurfaceMessage>(ReceiveClient, allowUnauthenticated: false);
        }
        private static void ReceiveServer(INetworkPlayer player, Fs20SurfaceMessage state)
        {
            var server = global::NuclearOption.Networking.NetworkManagerNuclearOption.i.Server;
            if (!Fs20Multiplayer.Accepted(player) || !server.World.TryGetIdentity(state.NetId, out var identity)) return;
            var aircraft = identity.GetComponent<Aircraft>();
            // Native CheckIfLocalSim assigns simulation to Aircraft.Player's owner.
            // A client cannot publish another pilot's loads, or an AI aircraft.
            if (!Family(aircraft) || aircraft.LocalSim || aircraft.Player == null || aircraft.Player.Owner != player)
            { player.Disconnect(); return; }
            var surface = Bind(aircraft);
            if (surface.Receive(state, player)) Relay(identity, state, player);
        }
        private static void ReceiveClient(INetworkPlayer player, Fs20SurfaceMessage state)
        {
            var client = global::NuclearOption.Networking.NetworkManagerNuclearOption.i.Client;
            if (!Fs20Multiplayer.ClientAccepted || client.Player != player || !client.World.TryGetIdentity(state.NetId, out var identity)) return;
            var aircraft = identity.GetComponent<Aircraft>();
            if (Family(aircraft) && !aircraft.LocalSim) Bind(aircraft).Receive(state, aircraft.Player == null ? null : aircraft.Player.Owner);
        }
        internal static void Publish(Aircraft aircraft, Fs20SurfaceMessage state)
        {
            if (!aircraft.LocalSim || !aircraft.Identity.IsSpawned) return;
            if (aircraft.IsServer) Relay(aircraft.Identity, state, null);
            else if (Fs20Multiplayer.ClientAccepted && aircraft.Client.IsConnected) aircraft.Client.Send(state, Channel.Unreliable);
        }
        private static void Relay(NetworkIdentity identity, Fs20SurfaceMessage state, INetworkPlayer source)
        {
            // Native visibility rules also constrain this presentation stream.
            foreach (var observer in (System.Collections.Generic.HashSet<INetworkPlayer>)Observers.GetValue(identity))
                if (!observer.IsHost && observer != source && observer.IsAuthenticated && Fs20Multiplayer.Accepted(observer))
                    observer.Send(state, Channel.Unreliable);
        }
        internal static Fs20NetworkSurfaceState Bind(Aircraft aircraft)
        {
            var state = aircraft.GetComponent<Fs20NetworkSurfaceState>();
            if (state == null) state = aircraft.gameObject.AddComponent<Fs20NetworkSurfaceState>();
            state.Bind(aircraft); return state;
        }
        private static bool Remote(UnitPart part) => part != null && part.parentUnit is Aircraft aircraft && Family(aircraft) && !aircraft.LocalSim;
        [HarmonyPatch(typeof(AeroPart), "ApplyJobFields")]
        private static class CompletedLoads
        {
            private static void Postfix(AeroPart __instance, ref PtrAllocation<AeroPartFields> ___JobFields)
            {
                if (!Fs20Multiplayer.SessionActive || !___JobFields.IsCreated || !(__instance.parentUnit is Aircraft owner) || !owner.LocalSim || !Family(owner)) return;
                var state = owner.GetComponent<Fs20NetworkSurfaceState>();
                if (state != null) state.Capture(__instance, ___JobFields.Ref());
            }
        }
        [HarmonyPatch(typeof(Aircraft), "FixedUpdate")]
        private static class AircraftTick
        {
            private static void Postfix(Aircraft __instance)
            {
                if (!Fs20Multiplayer.SessionActive || !Family(__instance) || !__instance.Identity.IsSpawned) return;
                Bind(__instance).RemoteTick();
            }
        }
        [HarmonyPatch(typeof(Fs20THighLift), "FixedUpdate")]
        private static class THighLiftAuthority { private static bool Prefix(AeroPart ___part) => !Remote(___part); }
        [HarmonyPatch(typeof(HighLiftDevice), "FixedUpdate")]
        private static class CHighLiftAuthority { private static bool Prefix(AeroPart ___aeroPart) => !Remote(___aeroPart); }
        [HarmonyPatch(typeof(TailHook), "FixedUpdate")]
        private static class HookTickAuthority { [HarmonyPriority(Priority.First)] private static bool Prefix(TailHook __instance) => !Remote(__instance.unitPart); }
        [HarmonyPatch(typeof(Fs20HookFairing), "EnforceDepartureLock")]
        private static class HookDepartureAuthority { private static bool Prefix(Fs20HookFairing __instance) => !Remote(__instance.GetComponent<TailHook>()?.unitPart); }
        [HarmonyPatch(typeof(TailHook), "CheckDeployConditions")]
        private static class HookDeployAuthority { [HarmonyPriority(Priority.First)] private static bool Prefix(TailHook __instance) => !Remote(__instance.unitPart); }
    }

    public sealed class Fs20NetworkSurfaceState : MonoBehaviour
    {
        private Aircraft aircraft;
        private Fs20THighLift[] tSlats;
        private HighLiftDevice[] cSlats;
        private TailHook[] hooks;
        private Fs20TVapor vapor;
        private Fs20TVapor.Layer[] layers;
        private float nextSend, nextBindings, receivedAt = float.NegativeInfinity;
        private uint sequence, lastSequence;
        private bool received;
        private INetworkPlayer lastSource;
        private Fs20SurfaceMessage target;
        private sealed class LoadSample { internal float Lift, Area, Time = float.NegativeInfinity; }
        private readonly System.Collections.Generic.Dictionary<AeroPart, LoadSample> loads = new System.Collections.Generic.Dictionary<AeroPart, LoadSample>();
        internal void Bind(Aircraft value) { if (aircraft == null) aircraft = value; }
        private void BindParts()
        {
            if (tSlats != null && Time.unscaledTime < nextBindings) return;
            nextBindings = Time.unscaledTime + .5f;
            tSlats = GetComponentsInChildren<Fs20THighLift>(true);
            cSlats = GetComponentsInChildren<HighLiftDevice>(true).Where(x => x.enabled).ToArray();
            hooks = GetComponentsInChildren<TailHook>(true);
            vapor = GetComponent<Fs20TVapor>();
            layers = vapor == null ? null : (Fs20TVapor.Layer[])Fs20NetworkSurfaces.Layers.GetValue(vapor);
            if (layers != null) foreach (var layer in layers)
                if (layer.SourceOwner != null && !loads.ContainsKey(layer.SourceOwner)) loads.Add(layer.SourceOwner, new LoadSample());
        }
        internal void Capture(AeroPart part, AeroPartFields fields)
        {
            // Independent from the owner's camera culling: nearby observers
            // still need measured loads when the owner is viewing elsewhere.
            if (!loads.TryGetValue(part, out var sample)) return;
            var frame = part.GetLiftTransform();
            var perpendicular = Vector3.ProjectOnPlane(frame == null ? part.transform.up : frame.up, fields.velocity).normalized;
            float force = fields.hasForce == JobForceType.NoForce || fields.submergedAmount > 0f ? 0f : Vector3.Dot(fields.force, perpendicular);
            sample.Lift = float.IsNaN(force) || float.IsInfinity(force) ? 0f : Mathf.Clamp(force, -1e8f, 1e8f);
            sample.Area = float.IsNaN(fields.wingArea) || float.IsInfinity(fields.wingArea) ? 0f : Mathf.Clamp(fields.wingArea, 0f, 1000f);
            sample.Time = Time.fixedTime;
        }
        private void LateUpdate()
        {
            if (aircraft == null || !Fs20Multiplayer.SessionActive || !aircraft.Identity.IsSpawned) return;
            BindParts();
            if (!aircraft.LocalSim) { Animate(Time.deltaTime); return; }
            if (Time.unscaledTime < nextSend || aircraft.partLookup == null) return;
            nextSend = Time.unscaledTime + .2f;
            var samples = layers == null ? new Fs20TVapor.Layer[0] : layers.Where(l => l.SourceOwner != null)
                .GroupBy(l => l.SourceOwner).Select(g => g.First()).Where(l => aircraft.partLookup.Contains(l.SourceOwner)).ToArray();
            if (samples.Length > Fs20SurfaceMessage.MaxLoads || tSlats.Length + cSlats.Length > Fs20SurfaceMessage.MaxMechanisms || hooks.Length > Fs20SurfaceMessage.MaxMechanisms) return;
            if (samples.Any(l => aircraft.partLookup.IndexOf(l.SourceOwner) > 254)) return;
            var state = new Fs20SurfaceMessage { NetId = aircraft.NetId, Sequence = ++sequence,
                PartIds = samples.Select(l => (byte)aircraft.partLookup.IndexOf(l.SourceOwner)).ToArray(),
                Lift = samples.Select(l => l.SourceOwner.IsDetached() || !loads.TryGetValue(l.SourceOwner, out var sample) || Time.fixedTime-sample.Time > .15f ? 0f : sample.Lift).ToArray(),
                Area = samples.Select(l => loads.TryGetValue(l.SourceOwner, out var sample) ? sample.Area : 0f).ToArray(),
                Slats = tSlats.Select(x => x.Deployment).Concat(cSlats.Select(x => (float)Fs20NetworkSurfaces.Position.GetValue(x))).ToArray(),
                Hooks = hooks.Select(x => (float)Fs20NetworkSurfaces.Amount.GetValue(x)).ToArray(),
                HookFlags = hooks.Select(x => (byte)(((bool)Fs20NetworkSurfaces.Deployed.GetValue(x) ? 1 : 0) |
                    ((bool)Fs20NetworkSurfaces.Hooked.GetValue(x) ? 2 : 0) | (x.GetComponent<Fs20HookFairing>()?.Overloaded == true ? 4 : 0))).ToArray() };
            Fs20NetworkSurfaces.Publish(aircraft, state);
        }
        internal bool Receive(Fs20SurfaceMessage state, INetworkPlayer source)
        {
            BindParts();
            if (aircraft.LocalSim || state.Slats.Length != tSlats.Length + cSlats.Length || state.Hooks.Length != hooks.Length || aircraft.partLookup == null) return false;
            foreach (byte id in state.PartIds) if (id >= aircraft.partLookup.Count || !(aircraft.partLookup[id] is AeroPart)) return false;
            if (received && source == lastSource && (!Fs20SurfaceMessage.Newer(state.Sequence, lastSequence) || Time.unscaledTime-receivedAt < .08f)) return false;
            received = true; lastSource = source; lastSequence = state.Sequence; receivedAt = Time.unscaledTime; target = state;
            return true;
        }
        internal void RemoteTick()
        {
            if (aircraft == null || aircraft.LocalSim) return;
            BindParts();
            var nativeEffect = GetComponent<VaporEffect>();
            if (nativeEffect != null) nativeEffect.enabled = false;
            if (received && Time.unscaledTime-receivedAt < .45f && layers != null)
            {
                foreach (var layer in layers)
                {
                    int id = aircraft.partLookup.IndexOf(layer.SourceOwner);
                    int sample = id < 0 ? -1 : Array.IndexOf(target.PartIds, (byte)id);
                    if (sample < 0 || layer.SourceOwner == null || layer.SourceOwner.IsDetached()) continue;
                    layer.Lift = target.Lift[sample]; layer.Area = target.Area[sample]; layer.LoadTime = Time.fixedTime; layer.LoadCount++;
                }
            }
            // Native remote simulation never calls LocalSimFixedUpdate, where
            // the existing vapor runs. Only presentation ticks on this branch.
            if (vapor != null) vapor.NativeTick(Time.fixedDeltaTime);
        }
        private void Animate(float deltaTime)
        {
            if (!received) return;
            float step = Mathf.Min(deltaTime, .1f) * 2f;
            for (int i = 0; i < tSlats.Length; i++)
            {
                var mechanism = tSlats[i];
                float value = Mathf.MoveTowards(mechanism.Deployment, target.Slats[i], step);
                Fs20NetworkSurfaces.TDeployment.SetValue(mechanism, value);
                foreach (var slat in (Fs20TSlat[])Fs20NetworkSurfaces.TSlats.GetValue(mechanism))
                    if (slat != null && Attached(slat)) slat.Deploy(value);
            }
            for (int i = 0; i < cSlats.Length; i++)
            {
                var mechanism = cSlats[i];
                float value = Mathf.MoveTowards((float)Fs20NetworkSurfaces.Position.GetValue(mechanism), target.Slats[tSlats.Length+i], step);
                Fs20NetworkSurfaces.Position.SetValue(mechanism, value);
                foreach (var moving in (Array)Fs20NetworkSurfaces.Moving.GetValue(mechanism))
                {
                    var frame = (Transform)AccessTools.Field(moving.GetType(), "transform").GetValue(moving);
                    if (frame != null && Attached(frame)) AccessTools.Method(moving.GetType(), "Animate").Invoke(moving, new object[] { value });
                }
            }
            for (int i = 0; i < hooks.Length; i++)
            {
                var hook = hooks[i];
                if (!Attached(hook)) continue;
                float amount = Mathf.MoveTowards((float)Fs20NetworkSurfaces.Amount.GetValue(hook), target.Hooks[i], step);
                Fs20NetworkSurfaces.Amount.SetValue(hook, amount);
                Fs20NetworkSurfaces.Deployed.SetValue(hook, (target.HookFlags[i] & 1) != 0);
                Fs20NetworkSurfaces.Hooked.SetValue(hook, (target.HookFlags[i] & 2) != 0);
                var fairing = hook.GetComponent<Fs20HookFairing>(); if (fairing != null) fairing.Overloaded = (target.HookFlags[i] & 4) != 0;
                var arm = (Transform)AccessTools.Field(typeof(TailHook), "hook").GetValue(hook);
                if (arm != null) arm.localEulerAngles = new Vector3(Mathf.Lerp((float)AccessTools.Field(typeof(TailHook), "stowedAngle").GetValue(hook),
                    (float)AccessTools.Field(typeof(TailHook), "deployedAngle").GetValue(hook), amount), 0f, 0f);
            }
        }
        private bool Attached(Component component)
        {
            var owner = component.GetComponentInParent<UnitPart>(true);
            return owner != null && owner.parentUnit == aircraft && !owner.IsDetached() && !(bool)AccessTools.Field(typeof(UnitPart), "fragmented").GetValue(owner);
        }
    }
}
