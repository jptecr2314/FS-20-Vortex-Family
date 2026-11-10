using BepInEx.Logging;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using Mirage;
using NuclearOption.Networking;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20SinglePlayer
    {
        private static ManualLogSource logger;
        private static bool showNotice;
        internal static void Install(Harmony harmony, ManualLogSource log)
        {
            logger = log;
            foreach (var patch in new[]
            {
                typeof(MenuGate),
                typeof(ClientGate),
                typeof(HostGate),
                typeof(TransportClientGate),
                typeof(TransportServerGate)
            }

            )
                harmony.PatchAll(patch);
        }

        private static bool Reject()
        {
            showNotice = true;
            logger?.LogWarning("FS-20 Vortex Family is single-player only. Disable the mod and restart to use multiplayer.");
            return false;
        }

        internal static void DrawNotice()
        {
            if (!showNotice)
                return;
            float left = (Screen.width - 480f) / 2f;
            float top = (Screen.height - 140f) / 2f;
            GUI.Box(new Rect(left, top, 480f, 140f), "FS-20 Vortex Family — Single-player only");
            GUI.Label(new Rect(left + 20f, top + 35f, 440f, 55f), "Multiplayer is disabled while this mod is installed.\nDisable the mod and restart the game to play online.");
            if (GUI.Button(new Rect(left + 190f, top + 100f, 100f, 25f), "Close"))
                showNotice = false;
        }

        [HarmonyPatch(typeof(MainMenu), "SelectMultiplayer")]
        internal static class MenuGate
        {
            private static bool Prefix() => Reject();
        }

        [HarmonyPatch(typeof(NetworkManagerNuclearOption), "StartClient")]
        internal static class ClientGate
        {
            private static bool Prefix() => Reject();
        }

        [HarmonyPatch(typeof(NetworkManagerNuclearOption), "StartHostAsync")]
        internal static class HostGate
        {
            private static bool Prefix(HostOptions __0, ref UniTask __result)
            {
                if (__0 != null && __0.SocketType == SocketType.Offline)
                    return true;
                __result = default(UniTask);
                return Reject();
            }
        }

        // ConnectHost is the local single-player path; Connect is remote.
        [HarmonyPatch(typeof(NetworkClient), "Connect")]
        internal static class TransportClientGate
        {
            private static bool Prefix() => Reject();
        }

        [HarmonyPatch(typeof(NetworkServer), "StartServer")]
        internal static class TransportServerGate
        {
            private static bool Prefix(NetworkServer __instance) => !__instance.Listening || Reject();
        }
    }
}
