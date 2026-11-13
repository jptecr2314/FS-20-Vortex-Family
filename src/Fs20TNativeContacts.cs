using System.Collections.Generic;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    public sealed class Fs20TNativeContacts : MonoBehaviour
    {
        [SerializeField] private UnitPart[] owners;
        [SerializeField] private Collider[] colliders;
        private bool[] detached, generated;
        private Rigidbody[] bodies;
        private UnitPart[] colliderOwners;
        private bool dirty;
        private readonly Dictionary<ulong, Pair> ignored = new Dictionary<ulong, Pair>();
        private readonly List<ulong> stale = new List<ulong>();
        private readonly List<int> active = new List<int>();
        private struct Pair { internal int A, B; }

        internal static void Configure(Transform root)
        {
            var controller = root.gameObject.AddComponent<Fs20TNativeContacts>();
            controller.owners = root.GetComponentsInChildren<UnitPart>(true);
            controller.colliders = root.GetComponentsInChildren<Collider>(true);
        }

        private void Start()
        {
            detached = new bool[owners.Length];
            bodies = new Rigidbody[owners.Length];
            generated = new bool[colliders.Length];
            colliderOwners = new UnitPart[colliders.Length];
            for (int i = 0; i < colliders.Length; i++)
            {
                var c = colliders[i];
                if (c == null) continue;
                var proxy = c.GetComponent<Fs20TDamageProxy>();
                generated[i] = proxy != null;
                colliderOwners[i] = proxy != null ? proxy.Owner : c.GetComponentInParent<UnitPart>(true);
            }
            Apply();
        }

        internal void RequestRefresh() => dirty = true;
        private void FixedUpdate()
        {
            if (detached == null) return;
            for (int i = 0; i < owners.Length; i++)
            {
                var owner = owners[i];
                bool next = owner == null || owner.IsDetached();
                var body = owner == null ? null : owner.rb;
                if (next != detached[i] || body != bodies[i]) { detached[i] = next; bodies[i] = body; dirty = true; }
            }
            if (dirty) Apply();
        }

        private bool Active(int index)
        {
            var c = colliders[index];
            return c != null && c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger;
        }

        private bool Attached(int index)
        {
            var owner = colliderOwners[index];
            return owner != null && !owner.IsDetached();
        }

        private void Apply()
        {
            dirty = false;
            stale.Clear();
            foreach (var pair in ignored)
            {
                int a = pair.Value.A, b = pair.Value.B;
                if (!Active(a) || !Active(b)) { stale.Add(pair.Key); continue; }
                if (!Attached(a) || !Attached(b))
                {
                    Physics.IgnoreCollision(colliders[a], colliders[b], false);
                    stale.Add(pair.Key);
                }
                else if (!Physics.GetIgnoreCollision(colliders[a], colliders[b])) Physics.IgnoreCollision(colliders[a], colliders[b], true);
            }
            foreach (ulong key in stale) ignored.Remove(key);

            // Only enumerate the active damage state's cells. Inactive
            // alternatives must neither collide nor enter the pair budget.
            active.Clear();
            for (int i = 0; i < colliders.Length; i++) if (Active(i) && Attached(i)) active.Add(i);
            for (int i = 0; i < active.Count; i++)
            for (int j = i + 1; j < active.Count; j++)
            {
                int a = active[i], b = active[j];
                if ((!generated[a] && !generated[b]) || colliderOwners[a] == colliderOwners[b]) continue;
                ulong key = ((ulong)(uint)a << 32) | (uint)b;
                if (ignored.ContainsKey(key) || Physics.GetIgnoreCollision(colliders[a], colliders[b])) continue;
                Physics.IgnoreCollision(colliders[a], colliders[b], true);
                ignored.Add(key, new Pair { A = a, B = b });
            }
        }

        private void OnDestroy()
        {
            foreach (var p in ignored.Values)
                if (Active(p.A) && Active(p.B)) Physics.IgnoreCollision(colliders[p.A], colliders[p.B], false);
        }
    }
}
