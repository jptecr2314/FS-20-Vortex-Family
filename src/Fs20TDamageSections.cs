using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    internal sealed class Fs20TDamageSections
    {
        internal sealed class Section
        {
            internal Fs20TAsset.DamageRegion Spec;
            internal AeroPart Part, Budget;
            internal PartJoint Joint;
            internal GameObject[] States = new GameObject[3];
            internal float NativeBudgetMass, NativeWingArea, NativeDragArea;
        }
        internal readonly Dictionary<string, Section> Owners = new Dictionary<string, Section>();
        internal readonly Dictionary<string, int> StageByPiece = new Dictionary<string, int>();
        internal readonly HashSet<AeroPart> Budgets = new HashSet<AeroPart>();

        internal Fs20TDamageSections(Transform root, Fs20TAsset asset)
        {
            var allPieces = asset.Pieces.ToDictionary(p => p.Name);
            foreach (var spec in asset.Regions)
            {
                var attachment = FindPart(root, spec.Attachment);
                var budget = FindPart(root, spec.Budget);
                foreach (var state in spec.States)
                    if (!allPieces.TryGetValue(state.Piece, out var p) || p.Owner != "@" + spec.Id || p.Replace || p.Hulls.Length == 0)
                        throw new InvalidOperationException("Invalid FS-20T region binding: " + state.Piece);

                var frame = Child("FS20T region " + spec.Id, attachment.transform);
                frame.position = root.TransformPoint(spec.Center);
                frame.rotation = root.rotation;
                var part = frame.gameObject.AddComponent<AeroPart>();
                part.parentUnit = root.GetComponent<Aircraft>();
                // Inactive template: native Awake will register the new part
                // exactly once and set up the real part/damage ID and RB.
                var anchor = Child("Structural attachment", frame);
                anchor.position = root.TransformPoint(spec.Anchor);
                var joint = new PartJoint { connectedPart = attachment, anchor = anchor, solverIterations = 12 };
                var donorJoint = budget.Joints.FirstOrDefault();
                if (donorJoint != null) joint.breakSound = donorJoint.breakSound;
                part.Joints = new[] { joint };

                var lift = Child("Section lift direction", frame);
                lift.rotation = (budget.GetLiftTransform() ?? budget.transform).rotation;
                Fs20Tuning.Set(part, "liftNormal", lift);
                foreach (string field in new[] { "airfoil", "streamlining", "wingEffectiveness", "airflowChanneling", "buoyancy", "simplePhysics" })
                    Fs20Tuning.Set(part, field, Fs20Tuning.Field(typeof(AeroPart), field).GetValue(budget));
                foreach (string field in new[] { "armorProperties", "hitSound", "disintegrationEffects" })
                    Fs20Tuning.Set(part, field, Fs20Tuning.Field(typeof(UnitPart), field).GetValue(budget));
                var effects = (List<DamageEffect>)Fs20Tuning.Field(typeof(UnitPart), "damageEffects").GetValue(budget);
                Fs20Tuning.Set(part, "damageEffects", effects == null ? new List<DamageEffect>() : new List<DamageEffect>(effects));
                var material = (DamageMaterial)Fs20Tuning.Field(typeof(UnitPart), "damageMaterial").GetValue(budget);
                Fs20Tuning.Set(part, "damageMaterial", new DamageMaterial { threshold = material?.threshold ?? 0f, renderers = new List<Renderer>(), indices = new byte[0] });
                Fs20Tuning.Set(part, "structuralThreshold", 28f);
                Fs20Tuning.Set(part, "integrityThreshold", 0f);
                Budgets.Add(budget);
                Owners.Add("@" + spec.Id, new Section
                {
                    Spec = spec, Part = part, Budget = budget, Joint = joint,
                    NativeBudgetMass = budget.mass, NativeWingArea = budget.GetWingArea(), NativeDragArea = budget.dragArea
                });
                for (int i = 0; i < spec.States.Length; i++) StageByPiece.Add(spec.States[i].Piece, i);
            }
        }

        internal void Allocate(Transform root)
        {
            foreach (var group in Owners.Values.GroupBy(s => s.Budget))
            {
                AeroPart donor = group.Key;
                var sections = group.ToArray();
                float total = sections.Sum(s => s.Spec.Area);
                var stock = sections[0];
                float retainedMass = stock.NativeBudgetMass;
                float skinMass = donor.mass - retainedMass;
                float extraArea = donor.GetWingArea() - stock.NativeWingArea;
                float extraDrag = donor.dragArea - stock.NativeDragArea;
                if (!(skinMass > 0) || !(extraArea > 0) || !(extraDrag >= 0))
                    throw new InvalidOperationException("FS-20T region budget missing on " + donor.name);
                foreach (var s in sections)
                {
                    float weight = Fs20TDamagePolicy.Fraction(s.Spec.Area, total);
                    float mass = skinMass * weight;
                    s.Part.mass = mass;
                    Fs20Tuning.Set(s.Part, "baseMass", mass);
                    s.Part.SetWingArea(extraArea * weight);
                    s.Part.dragArea = extraDrag * weight;
                    Vector3 center = root.TransformPoint(s.Spec.Center);
                    Fs20TPartPhysics.Configure(s.Part, center, center);
                    // Match the existing game's 10x native joint-force scale;
                    // keep the joint breakable at damage/overload.
                    float nominalForce = donor.Joints.Max(j => j.breakForce) * weight;
                    s.Joint.breakForce = Mathf.Max(nominalForce, mass * 9f * 9.81f * .2f);
                    s.Joint.breakTorque = Mathf.Max(donor.Joints.Max(j => j.breakTorque) * weight, s.Joint.breakForce * Mathf.Max(.25f, Vector3.Distance(s.Part.transform.position, s.Joint.anchor.position)));
                    if (s.States.Any(o => o == null)) throw new InvalidOperationException("Incomplete FS-20T visual states: " + s.Spec.Id);
                    var controller = s.Part.gameObject.AddComponent<Fs20TDamageState>();
                    controller.Configure(s.Part, s.States, s.Spec.States, root.GetComponent<Aircraft>());
                }

                // HighLiftDevice.Start captures this fixed baseline before it
                // adds the slat increment. Panel damage removes only extra lift.
                donor.SetWingArea(stock.NativeWingArea);
                donor.dragArea = stock.NativeDragArea;
                donor.mass = retainedMass;
                Fs20Tuning.Set(donor, "baseMass", retainedMass);
            }
        }

        private static AeroPart FindPart(Transform root, string path) => root.Find(path)?.GetComponent<AeroPart>() ?? throw new InvalidOperationException("Missing FS-20T damage attachment: " + path);
        private static Transform Child(string name, Transform parent)
        {
            var go = new GameObject(name) { layer = parent.gameObject.layer };
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }

    public sealed class Fs20TDamageState : MonoBehaviour
    {
        private static readonly FieldInfo Health = Fs20Tuning.Field(typeof(UnitPart), "hitPoints");
        private static readonly FieldInfo Fragmented = Fs20Tuning.Field(typeof(UnitPart), "fragmented");
        [SerializeField] private AeroPart part;
        [SerializeField] private Aircraft aircraft;
        [SerializeField] private GameObject[] states;
        [SerializeField] private float[] areas, drag;
        private int last = -1;
        private float nextPoll;
        private float slatArea;
        internal float IntactArea => areas[0];
        internal void SetSlatArea(float extra)
        {
            slatArea = Mathf.Max(0f, extra);
            if (part == null || areas == null) return;
            int state = Fs20TDamagePolicy.State((float)Health.GetValue(part), (bool)Fragmented.GetValue(part));
            ApplyArea(state);
        }
        private void ApplyArea(int state) => part.SetWingArea(part.IsDetached() || state == Fs20TDamagePolicy.Lost
            ? 0f : areas[state] + slatArea * areas[state] / areas[0]);
        internal void Configure(AeroPart owner, GameObject[] objects, Fs20TAsset.DamageState[] spec, Aircraft unit)
        {
            part = owner; aircraft = unit; states = objects;
            areas = spec.Select((s, i) => owner.GetWingArea() * (i == 0 ? 1f : s.AreaFraction)).ToArray();
            drag = spec.Select(s => owner.dragArea * s.DragScale).ToArray();
            for (int i = 0; i < states.Length; i++) states[i].SetActive(i == 0);
        }
        private void LateUpdate()
        {
            if (part == null || Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + .05f;
            int state = Fs20TDamagePolicy.State((float)Health.GetValue(part), (bool)Fragmented.GetValue(part));
            if (state == last) return;
            last = state;
            for (int i = 0; i < states.Length; i++) if (states[i] != null) states[i].SetActive(i == state);
            ApplyArea(state);
            part.dragArea = state == Fs20TDamagePolicy.Lost ? 0f : drag[state];
            if (aircraft != null) aircraft.GetComponent<Fs20TNativeContacts>()?.RequestRefresh();
        }
    }
}
