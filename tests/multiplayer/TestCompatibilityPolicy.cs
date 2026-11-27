using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Japensou.NuclearOption.VortexConventional;

internal static class Fs20MultiplayerPolicyTests
{
    private static int checks;
    private static readonly byte[] Digest = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
    private static readonly byte[] Nonce = Enumerable.Range(40, 16).Select(i => (byte)i).ToArray();
    private static void Check(bool value, string reason) { if (!value) throw new Exception(reason);checks++; }
    private static void Invalid(Action action) { try { action(); } catch (FormatException) { checks++;return; }throw new Exception("Malformed packet was accepted."); }
    private static Fs20CompatibilityMessage Read(Stream stream)
    {
        var data = new byte[52];int read = 0;
        while (read < data.Length) { int n = stream.Read(data, read, data.Length - read);if (n == 0) throw new EndOfStreamException();read += n; }
        return Fs20CompatibilityMessage.Decode(data);
    }
    private static void Send(Stream stream, Fs20CompatibilityMessage message)
    {
        byte[] data = message.Encode();stream.Write(data, 0, data.Length);stream.Flush();
    }
    private static void Loopback(bool matching)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = Task.Run(() =>
        {
            using (var socket = listener.AcceptTcpClient())
            {
                socket.ReceiveTimeout = socket.SendTimeout = 3000;
                using (var stream = socket.GetStream())
                {
                    var hello = Read(stream);var peer = new Fs20NetworkPolicy.Peer(Digest);peer.NativeResult(true);
                    if (!peer.Hello(hello.Protocol, hello.Digest)) return false;
                    Check(peer.Offer(Nonce, false, 2), "Host offer not formed.");
                    Send(stream, new Fs20CompatibilityMessage { Protocol = Fs20NetworkPolicy.Protocol, Phase = 1, Digest = Digest, Nonce = Nonce, Stock = false, Flaps = 2 });
                    var ack = Read(stream);return peer.Acknowledge(ack.Protocol, ack.Digest, ack.Nonce, ack.Stock, ack.Flaps);
                }
            }
        });
        try
        {
            using (var socket = new TcpClient())
            {
                socket.ReceiveTimeout = socket.SendTimeout = 3000;socket.Connect(IPAddress.Loopback, port);
                using (var stream = socket.GetStream())
                {
                    byte[] remote = (byte[])Digest.Clone();if (!matching) remote[0] ^= 1;
                    Send(stream, new Fs20CompatibilityMessage { Protocol = Fs20NetworkPolicy.Protocol, Phase = 0, Digest = remote, Nonce = Nonce });
                    if (matching)
                    {
                        var offer = Read(stream);var client = new Fs20NetworkPolicy.ClientPolicy();client.Begin(Digest);
                        Check(client.Offer(offer.Protocol, offer.Digest, offer.Nonce, offer.Stock, offer.Flaps), "Client rejected matching host.");
                        Check(!client.Active, "Client activated before native authentication callback.");
                        offer.Phase = 2;Send(stream, offer);
                        Check(client.Authenticate(true) && !client.SharedStock && client.FlapMode == 2, "Client did not adopt host policy.");
                    }
                }
            }
            Check(server.Wait(4000) && server.Result == matching, "Loopback admission outcome differs.");
        }
        finally { listener.Stop(); }
    }
    private static int Main()
    {
        try
        {
            var message = new Fs20CompatibilityMessage { Protocol = Fs20NetworkPolicy.Protocol, Phase = 1, Stock = true, Flaps = 3, Digest = Digest, Nonce = Nonce };
            var copy = Fs20CompatibilityMessage.Decode(message.Encode());
            Check(copy.Encode().SequenceEqual(message.Encode()), "Packet did not round-trip.");
            for (int size = 0; size < 52; size++) Invalid(() => Fs20CompatibilityMessage.Decode(new byte[size]));
            Invalid(() => Fs20CompatibilityMessage.Decode(new byte[53]));
            foreach (int position in new[] { 1, 2, 3 }) { var bytes = message.Encode();bytes[position] = 255;Invalid(() => Fs20CompatibilityMessage.Decode(bytes)); }
            var before = new Fs20NetworkPolicy.Peer(Digest);
            Check(before.Hello(Fs20NetworkPolicy.Protocol, Digest) && !before.Offer(Nonce, true, 0), "Offer bypassed native authentication.");
            Check(!before.Acknowledge(Fs20NetworkPolicy.Protocol, Digest, Nonce, true, 0) && !before.Accepted, "ACK bypassed native authentication.");
            for (int i = 0; i < Digest.Length; i++)
            {
                var wrong = (byte[])Digest.Clone();wrong[i] ^= 1;var peer = new Fs20NetworkPolicy.Peer(Digest);peer.NativeResult(true);
                Check(!peer.Hello(Fs20NetworkPolicy.Protocol, wrong) && !peer.Accepted && !peer.Offer(Nonce, true, 0), "Different installation was admitted.");
            }
            var protocol = new Fs20NetworkPolicy.Peer(Digest);Check(!protocol.Hello((byte)(Fs20NetworkPolicy.Protocol+1), Digest), "Protocol mismatch admitted.");
            var nativeFail = new Fs20NetworkPolicy.Peer(Digest);nativeFail.Hello(Fs20NetworkPolicy.Protocol, Digest);nativeFail.NativeResult(false);
            Check(!nativeFail.Offer(Nonce, true, 0), "Native failure was overridden.");
            for (int i = 0; i < Nonce.Length; i++)
            {
                var peer = new Fs20NetworkPolicy.Peer(Digest);peer.Hello(Fs20NetworkPolicy.Protocol, Digest);peer.NativeResult(true);peer.Offer(Nonce, true, 0);
                var wrong = (byte[])Nonce.Clone();wrong[i] ^= 1;
                Check(!peer.Acknowledge(Fs20NetworkPolicy.Protocol, Digest, wrong, true, 0), "Invalid/replayed nonce admitted.");
            }
            var accepted = new Fs20NetworkPolicy.Peer(Digest);accepted.Hello(Fs20NetworkPolicy.Protocol, Digest);accepted.NativeResult(true);accepted.Offer(Nonce, true, 1);
            Check(accepted.Acknowledge(Fs20NetworkPolicy.Protocol, Digest, Nonce, true, 1), "Valid peer rejected.");
            Check(accepted.Acknowledge(Fs20NetworkPolicy.Protocol, Digest, Nonce, true, 1), "Duplicate valid ACK is not idempotent.");
            accepted.Reject("Disconnected.");Check(!accepted.Acknowledge(Fs20NetworkPolicy.Protocol, Digest, Nonce, true, 1), "Disconnected session reused.");
            var stock = new Fs20NetworkPolicy.Peer(Digest);stock.Hello(Fs20NetworkPolicy.Protocol, Digest);stock.NativeResult(true);stock.Offer(Nonce, false, 2);
            Check(!stock.Acknowledge(Fs20NetworkPolicy.Protocol, Digest, Nonce, true, 2), "Client changed host stock setting.");
            var flaps = new Fs20NetworkPolicy.Peer(Digest);flaps.Hello(Fs20NetworkPolicy.Protocol, Digest);flaps.NativeResult(true);flaps.Offer(Nonce, false, 2);
            Check(!flaps.Acknowledge(Fs20NetworkPolicy.Protocol, Digest, Nonce, false, 0), "Client changed host flap policy.");
            var clientPolicy = new Fs20NetworkPolicy.ClientPolicy();clientPolicy.Begin(Digest);
            Check(!clientPolicy.Authenticate(true), "Vanilla/no-offer server admitted.");
            Check(clientPolicy.Offer(Fs20NetworkPolicy.Protocol, Digest, Nonce, true, 1) && !clientPolicy.Authenticate(false), "Client overrode native rejection.");
            Check(clientPolicy.Authenticate(true), "Matching client failed activation.");clientPolicy.Reset();
            Check(!clientPolicy.Active && !clientPolicy.OfferReceived && !clientPolicy.SharedStock && clientPolicy.Nonce == null, "Host settings leaked after disconnect.");
            Loopback(true);Loopback(false);
            Console.WriteLine("FS20_MULTIPLAYER_POLICY_PASS checks=" + checks + " loopbackCases=2 gameLaunched=false");return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error);return 1; }
    }
}
