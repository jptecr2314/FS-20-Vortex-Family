using System;

namespace Japensou.NuclearOption.VortexConventional
{
    internal struct Fs20CompatibilityMessage
    {
        internal byte Protocol, Phase, Flaps;
        internal bool Stock;
        internal byte[] Digest, Nonce;
        internal byte[] Encode()
        {
            if (Digest == null || Digest.Length != 32 || Nonce == null || Nonce.Length != 16 || Phase > 2 || Flaps > 3)
                throw new FormatException("Invalid FS-20 compatibility packet.");
            var bytes = new byte[52];bytes[0] = Protocol;bytes[1] = Phase;bytes[2] = Stock ? (byte)1 : (byte)0;bytes[3] = Flaps;
            Buffer.BlockCopy(Digest, 0, bytes, 4, 32);Buffer.BlockCopy(Nonce, 0, bytes, 36, 16);
            return bytes;
        }
        internal static Fs20CompatibilityMessage Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length != 52 || bytes[1] > 2 || bytes[2] > 1 || bytes[3] > 3) throw new FormatException("Invalid FS-20 compatibility packet.");
            var message = new Fs20CompatibilityMessage { Protocol = bytes[0], Phase = bytes[1], Stock = bytes[2] != 0, Flaps = bytes[3], Digest = new byte[32], Nonce = new byte[16] };
            Buffer.BlockCopy(bytes, 4, message.Digest, 0, 32);Buffer.BlockCopy(bytes, 36, message.Nonce, 0, 16);
            return message;
        }
    }

    internal static class Fs20NetworkPolicy
    {
        internal const byte Protocol = 2;
        internal const int TimeoutMilliseconds = 10000;
        internal const string Mismatch = "FS-20 versions, mod definitions or load order differ. Use matching mod installations.";
        internal static bool Same(byte[] a, byte[] b, int size)
        {
            if (a == null || b == null || a.Length != size || b.Length != size) return false;
            int difference = 0;
            for (int i = 0; i < size; i++) difference |= a[i] ^ b[i];
            return difference == 0;
        }

        internal sealed class Peer
        {
            private readonly byte[] signature;
            private byte[] challenge;
            private bool stock;
            private byte flaps;
            internal bool HelloReceived { get; private set; }
            internal bool NativeAuthenticated { get; private set; }
            internal bool OfferSent { get; private set; }
            internal bool Accepted { get; private set; }
            internal string Failure { get; private set; }
            internal Peer(byte[] signature) { this.signature = (byte[])signature.Clone(); }
            internal bool Hello(byte protocol, byte[] remote)
            {
                if (Failure != null) return false;
                if (protocol != Protocol || !Same(signature, remote, 32)) return Reject(Mismatch);
                HelloReceived = true;
                return true;
            }
            internal void NativeResult(bool success)
            {
                if (!success) { Reject("Native game authentication failed."); return; }
                NativeAuthenticated = true;
            }
            internal bool Offer(byte[] nonce, bool sharedStock, byte flapMode)
            {
                if (Failure != null || !NativeAuthenticated || !HelloReceived || OfferSent || nonce == null || nonce.Length != 16 || flapMode > 3) return false;
                challenge = (byte[])nonce.Clone();stock = sharedStock;flaps = flapMode;OfferSent = true;
                return true;
            }
            internal bool Acknowledge(byte protocol, byte[] remote, byte[] nonce, bool sharedStock, byte flapMode)
            {
                if (Failure != null) return false;
                if (!NativeAuthenticated || !OfferSent || protocol != Protocol || !Same(signature, remote, 32) || !Same(challenge, nonce, 16) || stock != sharedStock || flaps != flapMode)
                    return Reject("FS-20 session acknowledgement is invalid.");
                Accepted = true;
                return true;
            }
            internal bool Reject(string reason) { Failure = reason;Accepted = false;return false; }
        }

        internal sealed class ClientPolicy
        {
            private byte[] signature;
            internal bool OfferReceived { get; private set; }
            internal bool Active { get; private set; }
            internal bool SharedStock { get; private set; }
            internal byte FlapMode { get; private set; }
            internal byte[] Nonce { get; private set; }
            internal void Begin(byte[] digest)
            {
                Reset();signature = (byte[])digest.Clone();
            }
            internal bool Offer(byte protocol, byte[] remote, byte[] nonce, bool stock, byte flaps)
            {
                if (Active || OfferReceived || protocol != Protocol || !Same(signature, remote, 32) || nonce == null || nonce.Length != 16 || flaps > 3) return false;
                Nonce = (byte[])nonce.Clone();SharedStock = stock;FlapMode = flaps;OfferReceived = true;
                return true;
            }
            internal bool Authenticate(bool nativeSuccess)
            {
                Active = nativeSuccess && OfferReceived;
                return Active;
            }
            internal void Reset()
            {
                signature = null;Nonce = null;OfferReceived = Active = SharedStock = false;FlapMode = 0;
            }
        }
    }
}
