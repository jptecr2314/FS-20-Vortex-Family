using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using Japensou.NuclearOption.SharedAirframes;
using Mirage;
using Mirage.Authentication;
using Mirage.Serialization;
using NuclearOption.Networking;
using NuclearOption.Networking.Authentication;
using UnityEngine;
using Channel = Mirage.Channel;

namespace Japensou.NuclearOption.VortexConventional
{
    // The experimental build substitutes this facade for the offline-only
    // source file. Ordinary builds keep their existing guard unchanged.
    internal static class Fs20SinglePlayer
    {
        internal static void Install(Harmony harmony, ManualLogSource log) => Fs20Multiplayer.Install(harmony, log);
        internal static void DrawNotice() => Fs20Multiplayer.DrawNotice();
    }

    internal static class Fs20CompatibilitySerialization
    {
        internal static void Write(NetworkWriter writer, Fs20CompatibilityMessage message)
        {
            byte[] bytes = message.Encode();writer.WriteBytes(bytes, 0, bytes.Length);
        }
        internal static Fs20CompatibilityMessage Read(NetworkReader reader)
        {
            if (!reader.CanReadBytes(52)) throw new FormatException("Truncated FS-20 compatibility packet.");
            var bytes = new byte[52];reader.ReadBytes(bytes, 0, bytes.Length);
            return Fs20CompatibilityMessage.Decode(bytes);
        }
    }

    internal static class Fs20Multiplayer
    {
        private static ManualLogSource logger;
        private static ConfigEntry<bool> enabled;
        private static ConfigEntry<bool> localStock;
        private static ConfigEntry<Fs20FlapMode> localFlaps;
        private static readonly Dictionary<INetworkPlayer, Fs20NetworkPolicy.Peer> peers = new Dictionary<INetworkPlayer, Fs20NetworkPolicy.Peer>();
        private static readonly Fs20NetworkPolicy.ClientPolicy clientPolicy = new Fs20NetworkPolicy.ClientPolicy();
        private static MessageHandler serverHandler, clientHandler;
        private static byte[] serverDigest, clientDigest;
        private static bool serverOnline, serverStock;
        private static byte serverFlaps;
        private static string notice;
        internal static bool SessionActive => serverOnline || clientPolicy.Active;
        internal static bool ClientAccepted => clientPolicy.Active;
        internal static bool Accepted(INetworkPlayer player) => player.IsHost || (serverOnline && peers.TryGetValue(player, out var p) && p.Accepted);

        internal static void Install(Harmony harmony, ManualLogSource log)
        {
            logger = log;
            var plugin = (BaseUnityPlugin)AccessTools.Field(typeof(VortexConventionalPlugin), "instance").GetValue(null);
            enabled = plugin.Config.Bind("Multiplayer", "Enable matching modded sessions", false,
                "Experimental. Enable on the host and every client, using matching FS-20 builds, mods and definition order. The host authorizes these aircraft and controls shared stock/flap policy for the session. Restart the session after changing settings.");
            localStock = (ConfigEntry<bool>)AccessTools.Field(typeof(SharedAirframeStock), "sharedStock").GetValue(null);
            localFlaps = (ConfigEntry<Fs20FlapMode>)AccessTools.Field(typeof(VortexConventionalPlugin), "flapMode").GetValue(plugin);
            if (localStock == null || localFlaps == null) throw new InvalidOperationException("FS-20 session settings are unavailable.");
            Writer<Fs20CompatibilityMessage>.Write = Fs20CompatibilitySerialization.Write;
            Reader<Fs20CompatibilityMessage>.Read = Fs20CompatibilitySerialization.Read;
            Fs20NetworkSurfaces.Install(harmony);
            foreach (var patch in new[] { typeof(MenuGate), typeof(ClientGate), typeof(HostGate), typeof(TransportClientGate), typeof(TransportServerGate),
                typeof(ClientHello), typeof(Authenticate), typeof(ServerAdmission), typeof(ClientAdmission),
                typeof(ServerDisconnected), typeof(ClientDisconnected), typeof(ServerStopped), typeof(StockPolicy), typeof(FlapPolicy), typeof(FlightAuthority), typeof(HookForceAuthority), typeof(BayMassAuthority) })
                harmony.PatchAll(patch);
            logger.LogInfo("FS-20 experimental multiplayer hooks ready. Modded sessions enabled: " + enabled.Value);
        }

        private static bool Reject(string reason)
        {
            notice = reason;logger?.LogWarning(reason);return false;
        }
        private static bool AllowStart()
        {
            if (enabled == null || !enabled.Value) return Reject("FS-20 multiplayer is disabled. Enable matching modded sessions on the host and every client, or disable this mod to join an ordinary server.");
            try { Fs20NetworkSignature.Create();EnsureMessageType();return true; }
            catch (Exception error) { return Reject("FS-20 network definitions are not ready: " + error.Message); }
        }
        internal static void DrawNotice()
        {
            if (notice == null) return;
            float x = (Screen.width - 540f) / 2f, y = (Screen.height - 170f) / 2f;
            GUI.Box(new Rect(x, y, 540f, 170f), "FS-20 multiplayer compatibility");
            GUI.Label(new Rect(x + 16f, y + 34f, 508f, 92f), notice);
            if (GUI.Button(new Rect(x + 220f, y + 132f, 100f, 25f), "Close")) notice = null;
        }
        private static Fs20NetworkPolicy.Peer Peer(INetworkPlayer player)
        {
            if (!peers.TryGetValue(player, out var peer)) peers[player] = peer = new Fs20NetworkPolicy.Peer(serverDigest);
            return peer;
        }
        private static void EnsureMessageType()
        {
            int id = MessagePacker.GetId<Fs20CompatibilityMessage>();
            if (MessagePacker.MessageTypes.TryGetValue(id, out var type))
            {
                if (type != typeof(Fs20CompatibilityMessage)) throw new InvalidOperationException("FS-20 message ID conflicts with another message.");
                return;
            }
            MessagePacker.RegisterMessage<Fs20CompatibilityMessage>();
        }
        private static void ClearServer()
        {
            foreach (var peer in peers.Values) peer.Reject("Server stopped.");
            peers.Clear();serverOnline = false;serverDigest = null;serverHandler = null;
        }
        private static void Register(MessageHandler handler, bool server)
        {
            if (handler == null || (server ? serverHandler : clientHandler) == handler) return;
            var handlers = (System.Collections.IDictionary)AccessTools.Field(typeof(MessageHandler), "_messageHandlers").GetValue(handler);
            if (handlers.Contains(MessagePacker.GetId<Fs20CompatibilityMessage>())) throw new InvalidOperationException("FS-20 message ID is already registered.");
            // Mirage's flag is allowUnauthenticated (not requireAuthentication).
            // The compatibility exchange precedes admission; Peer still waits
            // for the original game's successful authentication before offering.
            if (server) { handler.RegisterHandler<Fs20CompatibilityMessage>(ReceiveServer, allowUnauthenticated: true);serverHandler = handler; }
            else { handler.RegisterHandler<Fs20CompatibilityMessage>(ReceiveClient, allowUnauthenticated: true);clientHandler = handler; }
            Fs20NetworkSurfaces.Register(handler, server);
        }
        private static void ReceiveServer(INetworkPlayer player, Fs20CompatibilityMessage message)
        {
            if (!serverOnline || player.IsHost) { player.Disconnect();return; }
            var peer = Peer(player);
            if (message.Phase == 0)
            {
                if (!peer.Hello(message.Protocol, message.Digest))
                {
                    logger.LogWarning(peer.Failure);
                    if (player.IsAuthenticated) player.Disconnect();
                }
                // Native authentication delivers the failure reason through
                // the game's normal rejection path once it completes.
                return;
            }
            bool accepted = message.Phase == 2 && peer.Acknowledge(message.Protocol, message.Digest, message.Nonce, message.Stock, message.Flaps);
            if (!accepted) { peer.Reject(peer.Failure ?? "Invalid FS-20 handshake order.");player.Disconnect(); }
        }
        private static void ReceiveClient(INetworkPlayer player, Fs20CompatibilityMessage message)
        {
            if (message.Phase != 1 || clientDigest == null || !clientPolicy.Offer(message.Protocol, message.Digest, message.Nonce, message.Stock, message.Flaps))
            { Reject(Fs20NetworkPolicy.Mismatch);player.Disconnect();return; }
            message.Phase = 2;player.Send(message, Channel.Reliable);
        }
        private static async UniTask<AuthenticationResult> WaitForCompatibility(UniTask<AuthenticationResult> native, INetworkPlayer player, CancellationToken token)
        {
            var result = await native;
            if (!result.Success || player.IsHost) return result;
            if (!serverOnline) return AuthenticationResult.CreateFail("The host has not enabled FS-20 multiplayer.");
            var peer = Peer(player);peer.NativeResult(true);
            float deadline = Time.realtimeSinceStartup + Fs20NetworkPolicy.TimeoutMilliseconds / 1000f;
            while (player.IsConnected && !token.IsCancellationRequested && Time.realtimeSinceStartup < deadline)
            {
                if (peer.Failure != null) return AuthenticationResult.CreateFail(peer.Failure);
                if (peer.HelloReceived && !peer.OfferSent)
                {
                    byte[] nonce = Guid.NewGuid().ToByteArray();
                    if (!peer.Offer(nonce, serverStock, serverFlaps)) return AuthenticationResult.CreateFail("FS-20 host policy could not be offered.");
                    player.Send(new Fs20CompatibilityMessage { Protocol = Fs20NetworkPolicy.Protocol, Phase = 1, Digest = serverDigest, Nonce = nonce, Stock = serverStock, Flaps = serverFlaps }, Channel.Reliable);
                }
                if (peer.Accepted) return result;
                if (peer.Failure != null) return AuthenticationResult.CreateFail(peer.Failure);
                if (!player.IsConnected) break;
                await UniTask.Delay(100, ignoreTimeScale: true, cancellationToken: token);
            }
            peer.Reject("FS-20 handshake timed out. Install matching mods and enable the mod on both sides.");
            return AuthenticationResult.CreateFail(peer.Failure);
        }

        [HarmonyPatch(typeof(MainMenu), "SelectMultiplayer")]
        private static class MenuGate { private static bool Prefix() => enabled.Value || Reject("Enable FS-20 matching modded sessions in the mod configuration first."); }
        [HarmonyPatch(typeof(NetworkManagerNuclearOption), "StartClient")]
        private static class ClientGate { private static bool Prefix() => AllowStart(); }
        [HarmonyPatch(typeof(NetworkManagerNuclearOption), "StartHostAsync")]
        private static class HostGate
        {
            private static bool Prefix(HostOptions __0, ref UniTask __result)
            {
                if (__0 != null && __0.SocketType == SocketType.Offline) return true;
                if (AllowStart()) return true;
                __result = default(UniTask);return false;
            }
        }
        [HarmonyPatch(typeof(NetworkClient), "Connect")]
        private static class TransportClientGate { private static bool Prefix() => AllowStart(); }
        [HarmonyPatch(typeof(NetworkServer), "StartServer")]
        private static class TransportServerGate
        {
            private static bool Prefix(NetworkServer __instance, out bool __state)
            {
                __state = false;
                if (__instance.Active) return true; // Keep the native duplicate-start failure.
                ClearServer();
                if (!__instance.Listening) return true;
                if (!AllowStart()) return false;
                serverDigest = Fs20NetworkSignature.Create();
                serverStock = localStock.Value && (bool)AccessTools.Field(typeof(SharedAirframeStock), "installed").GetValue(null);
                serverFlaps = (byte)localFlaps.Value;
                if (serverFlaps > 3) return Reject("FS-20 host flap policy is invalid.");
                serverOnline = true;peers.Clear();__state = true;return true;
            }
            private static void Postfix(NetworkServer __instance)
            {
                if (__instance.Active && __instance.Listening && serverOnline) Register(__instance.MessageHandler, true);
            }
            private static Exception Finalizer(Exception __exception, bool __state)
            {
                if (__exception != null && __state) ClearServer();
                return __exception;
            }
        }
        [HarmonyPatch(typeof(NetworkAuthenticatorNuclearOption), "OnClientConnected")]
        private static class ClientHello
        {
            private static bool Prefix(INetworkPlayer __0, NetworkClient ___client)
            {
                if (__0.IsHost) return true;
                if (!AllowStart()) { __0.Disconnect();return false; }
                Register(___client.MessageHandler, false);clientDigest = Fs20NetworkSignature.Create();clientPolicy.Begin(clientDigest);
                __0.Send(new Fs20CompatibilityMessage { Protocol = Fs20NetworkPolicy.Protocol, Phase = 0, Digest = clientDigest, Nonce = Guid.NewGuid().ToByteArray() }, Channel.Reliable);
                return true;
            }
        }
        [HarmonyPatch(typeof(NetworkAuthenticatorNuclearOption), "AuthenticateAsync")]
        private static class Authenticate
        {
            private static void Postfix(INetworkPlayer __0, CancellationToken __2, ref UniTask<AuthenticationResult> __result) =>
                __result = WaitForCompatibility(__result, __0, __2);
        }
        [HarmonyPatch(typeof(NetworkManagerNuclearOption), "OnServerAuthenticated")]
        private static class ServerAdmission
        {
            private static bool Prefix(INetworkPlayer __0)
            {
                if (__0.IsHost || (serverOnline && peers.TryGetValue(__0, out var peer) && peer.Accepted)) return true;
                __0.Disconnect();return false;
            }
        }
        [HarmonyPatch(typeof(NetworkManagerNuclearOption), "OnClientAuthenticated")]
        private static class ClientAdmission
        {
            private static bool Prefix(INetworkPlayer __0)
            {
                if (__0.IsHost || clientPolicy.Authenticate(true)) return true;
                Reject("The host did not authorize this FS-20 build. Join a matching, explicitly enabled modded session.");__0.Disconnect();return false;
            }
        }
        [HarmonyPatch(typeof(NetworkAuthenticatorNuclearOption), "OnServerDisconnected")]
        private static class ServerDisconnected { private static void Postfix(INetworkPlayer __0) { if (peers.TryGetValue(__0, out var p)) p.Reject("Disconnected.");peers.Remove(__0); } }
        [HarmonyPatch(typeof(NetworkAuthenticatorNuclearOption), "OnClientDisconnected")]
        private static class ClientDisconnected { private static void Postfix() { clientPolicy.Reset();clientDigest = null;clientHandler = null; } }
        [HarmonyPatch(typeof(NetworkAuthenticatorNuclearOption), "OnStopServer")]
        private static class ServerStopped { private static void Postfix() => ClearServer(); }
        [HarmonyPatch(typeof(SharedAirframeStock), "get_Enabled")]
        private static class StockPolicy { private static void Postfix(ref bool __result) { if (serverOnline) __result = serverStock;else if (clientPolicy.Active) __result = clientPolicy.SharedStock; } }
        [HarmonyPatch(typeof(VortexConventionalPlugin), "get_FlapMode")]
        private static class FlapPolicy { private static void Postfix(ref Fs20FlapMode __result) { if (serverOnline) __result = (Fs20FlapMode)serverFlaps;else if (clientPolicy.Active) __result = (Fs20FlapMode)clientPolicy.FlapMode; } }
        [HarmonyPatch]
        private static class FlightAuthority
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(Fs20FlightController), "PrepareControls");
                yield return AccessTools.Method(typeof(Fs20AFlightController), "PrepareControls");
                yield return AccessTools.Method(typeof(Fs20TFlightController), "PrepareControls");
            }
            private static bool Prefix(Aircraft ___aircraft) => ___aircraft == null || ___aircraft.LocalSim;
        }
        [HarmonyPatch(typeof(TailHook), "ApplyForce")]
        private static class HookForceAuthority
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(TailHook __instance) => __instance.GetComponent<Fs20HookFairing>() == null ||
                __instance.unitPart == null || __instance.unitPart.parentUnit == null || __instance.unitPart.parentUnit.LocalSim;
        }
        [HarmonyPatch(typeof(Fs20BayMassCenter), "Refresh")]
        private static class BayMassAuthority { private static bool Prefix(AeroPart ___part) => ___part == null || ___part.parentUnit == null || ___part.parentUnit.LocalSim; }
    }
}
