using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Bootstrap;
using HarmonyLib;
using Mirage;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20NetworkSignature
    {
        internal static byte[] Create()
        {
            var encyclopedia = Encyclopedia.i;
            if (encyclopedia == null || Encyclopedia.Lookup == null || Encyclopedia.WeaponLookup == null || encyclopedia.IndexLookup == null ||
                !Encyclopedia.Lookup.ContainsKey("JP_FS20_CTOL") || !Encyclopedia.Lookup.ContainsKey(Fs20AirframeA.Key) || !Encyclopedia.Lookup.ContainsKey(Fs20TAirframe.Key) ||
                !Encyclopedia.WeaponLookup.ContainsKey(Fs20AugerMount.Key))
                throw new InvalidOperationException("FS-20 aircraft and weapon definitions are not ready.");
            var text = new StringBuilder("FS20/network/2/0.6.8/ABC T\n");
            using (var hash = SHA256.Create())
            {
                foreach (var plugin in Chainloader.PluginInfos.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    string file = plugin.Value.Instance.GetType().Assembly.Location;
                    text.Append(plugin.Key).Append(':').Append(plugin.Value.Metadata.Version).Append(':');
                    using (var stream = File.OpenRead(file)) text.Append(Convert.ToBase64String(hash.ComputeHash(stream)));
                    text.Append('\n');
                }
                using (var stream = File.OpenRead(typeof(Aircraft).Assembly.Location)) text.Append(Convert.ToBase64String(hash.ComputeHash(stream))).Append('\n');
                for (int i = 0; i < encyclopedia.IndexLookup.Count; i++)
                {
                    var definition = encyclopedia.IndexLookup[i];
                    if (definition == null || definition.LookupIndex != i) throw new InvalidOperationException("Network definition indices are inconsistent.");
                    var unit = definition as UnitDefinition;var mount = definition as WeaponMount;
                    string key = unit != null ? unit.jsonKey : mount != null ? mount.jsonKey : ((UnityEngine.Object)definition).name;
                    text.Append(i).Append(':').Append(definition.GetType().FullName).Append(':').Append(key);
                    if (unit != null && unit.unitPrefab != null)
                    {
                        var identity = unit.unitPrefab.GetComponent<NetworkIdentity>();
                        if (identity != null) text.Append(':').Append(identity.PrefabHash);
                        if (unit.jsonKey == "JP_FS20_CTOL" || unit.jsonKey == Fs20AirframeA.Key || unit.jsonKey == Fs20TAirframe.Key)
                        {
                            var parts = unit.unitPrefab.GetComponentsInChildren<UnitPart>(true);
                            if (parts.Length > 255) throw new InvalidOperationException("FS-20 exceeds the native byte part-index limit.");
                            foreach (var part in parts)
                            {
                                string path = part.name;
                                for (var p = part.transform.parent; p != null && p != unit.unitPrefab.transform; p = p.parent) path = p.name + "/" + path;
                                text.Append("\npart:").Append(path).Append(':').Append(part.GetType().FullName);
                            }
                            foreach (var behaviour in unit.unitPrefab.GetComponentsInChildren<NetworkBehaviour>(true))
                                text.Append("\nnet:").Append(behaviour.GetType().FullName);
                        }
                    }
                    text.Append('\n');
                }
                // A failed stock-hook install must not be hidden by a host
                // policy that enables sharing on only one participant.
                text.Append("stock-hooks:").Append(AccessTools.Field(typeof(Japensou.NuclearOption.SharedAirframes.SharedAirframeStock), "installed").GetValue(null));
                return hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()));
            }
        }
    }
}
