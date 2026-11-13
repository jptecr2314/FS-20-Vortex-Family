using System;
using System.IO;

namespace Japensou.NuclearOption.VortexConventional
{
    // Presentation only. Damage, detachments, controls, pose and supply remain
    // on native RPCs/SyncVars; this packet never carries a force or health edit.
    internal struct Fs20SurfaceMessage
    {
        internal const int MaxLoads = 64, MaxMechanisms = 8, MaxBytes = 667;
        internal uint NetId, Sequence;
        internal byte[] PartIds, HookFlags;
        internal float[] Lift, Area, Slats, Hooks;
        internal byte[] Encode()
        {
            Validate();
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(NetId); writer.Write(Sequence);
                writer.Write((byte)PartIds.Length); writer.Write((byte)Slats.Length); writer.Write((byte)Hooks.Length);
                for (int i = 0; i < PartIds.Length; i++) { writer.Write(PartIds[i]); writer.Write(Lift[i]); writer.Write(Area[i]); }
                foreach (float value in Slats) writer.Write(value);
                for (int i = 0; i < Hooks.Length; i++) { writer.Write(Hooks[i]); writer.Write(HookFlags[i]); }
                return stream.ToArray();
            }
        }
        internal static Fs20SurfaceMessage Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 11 || bytes.Length > MaxBytes) throw new FormatException("Invalid FS-20 surface packet length.");
            using (var stream = new MemoryStream(bytes))
            using (var reader = new BinaryReader(stream))
            {
                var state = new Fs20SurfaceMessage { NetId = reader.ReadUInt32(), Sequence = reader.ReadUInt32() };
                int loads = reader.ReadByte(), slats = reader.ReadByte(), hooks = reader.ReadByte();
                if (loads > MaxLoads || slats > MaxMechanisms || hooks > MaxMechanisms || bytes.Length != 11 + loads * 9 + slats * 4 + hooks * 5)
                    throw new FormatException("Invalid FS-20 surface counts.");
                state.PartIds = new byte[loads]; state.Lift = new float[loads]; state.Area = new float[loads];
                state.Slats = new float[slats]; state.Hooks = new float[hooks]; state.HookFlags = new byte[hooks];
                for (int i = 0; i < loads; i++) { state.PartIds[i] = reader.ReadByte(); state.Lift[i] = reader.ReadSingle(); state.Area[i] = reader.ReadSingle(); }
                for (int i = 0; i < slats; i++) state.Slats[i] = reader.ReadSingle();
                for (int i = 0; i < hooks; i++) { state.Hooks[i] = reader.ReadSingle(); state.HookFlags[i] = reader.ReadByte(); }
                state.Validate(); return state;
            }
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal void Validate()
        {
            if (NetId == 0 || PartIds == null || Lift == null || Area == null || Slats == null || Hooks == null || HookFlags == null ||
                PartIds.Length > MaxLoads || Lift.Length != PartIds.Length || Area.Length != PartIds.Length ||
                Slats.Length > MaxMechanisms || Hooks.Length > MaxMechanisms || Hooks.Length != HookFlags.Length)
                throw new FormatException("Invalid FS-20 surface layout.");
            for (int i = 0; i < PartIds.Length; i++)
            {
                if (!Finite(Lift[i]) || Math.Abs(Lift[i]) > 1e8f || !Finite(Area[i]) || Area[i] < 0f || Area[i] > 1000f)
                    throw new FormatException("Invalid FS-20 surface load.");
                for (int j = 0; j < i; j++) if (PartIds[j] == PartIds[i]) throw new FormatException("Duplicate FS-20 part.");
            }
            foreach (float value in Slats) if (!Finite(value) || value < 0f || value > 1f) throw new FormatException("Invalid FS-20 slat position.");
            for (int i = 0; i < Hooks.Length; i++) if (!Finite(Hooks[i]) || Hooks[i] < 0f || Hooks[i] > 1f || HookFlags[i] > 7)
                throw new FormatException("Invalid FS-20 hook state.");
        }
        internal static bool Newer(uint incoming, uint previous) => unchecked((int)(incoming - previous)) > 0;
    }
}
