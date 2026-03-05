using FluentAssertions;
using SignalBench.Core.Decoding;
using SignalBench.Core.Ingestion;
using SignalBench.Core.Models.Schema;
using System.Net;
using System.Net.Sockets;
using Xunit;

namespace SignalBench.Tests;

public class NetworkStreamingTests
{
    [Fact]
    public async Task UdpTelemetrySource_Should_Receive_Packets()
    {
        // 1. Setup Schema
        var schema = new PacketSchema
        {
            Name = "Test",
            SyncWord = 0xAA55,
            Endianness = Endianness.Little
        };
        schema.Fields.Add(new FieldDefinition { Name = "val1", Type = FieldType.Uint16, BitOffset = 0 });
        schema.Fields.Add(new FieldDefinition { Name = "val2", Type = FieldType.Int32, BitOffset = 16 });

        // 2. Setup Source (Listen on localhost, random port)
        int port = GetFreePort();
        var source = new NetworkTelemetrySource("127.0.0.1", port, schema, NetworkProtocol.Udp);
        
        var tcs = new TaskCompletionSource<DecodedPacket>();
        source.PacketReceived += (packet) => tcs.TrySetResult(packet);

        try
        {
            source.Start();

            // 3. Send UDP packet
            using var client = new UdpClient();
            // Packet: AA 55 (sync) 34 12 (val1=0x1234=4660) 78 56 34 12 (val2=0x12345678=305419896)
            byte[] data = [0xAA, 0x55, 0x34, 0x12, 0x78, 0x56, 0x34, 0x12];
            await client.SendAsync(data, data.Length, "127.0.0.1", port);

            // 4. Wait for receipt
            var receivedPacket = await Task.WhenAny(tcs.Task, Task.Delay(2000)) == tcs.Task 
                ? await tcs.Task 
                : null;

            // 5. Verify
            receivedPacket.Should().NotBeNull("Packet should be received within timeout");
            receivedPacket!.Fields["val1"].Should().Be((uint)4660);
            receivedPacket!.Fields["val2"].Should().Be(305419896);
        }
        finally
        {
            source.Stop();
        }
    }

    [Fact]
    public async Task TcpTelemetrySource_Should_Receive_Packets()
    {
        // 1. Setup Schema
        var schema = new PacketSchema
        {
            Name = "Test",
            SyncWord = 0xAA55,
            Endianness = Endianness.Little
        };
        schema.Fields.Add(new FieldDefinition { Name = "val1", Type = FieldType.Uint16, BitOffset = 0 });
        schema.Fields.Add(new FieldDefinition { Name = "val2", Type = FieldType.Int32, BitOffset = 16 });

        // 2. Start a TCP Server to send data
        int port = GetFreePort();
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        
        // Start source as a client
        var source = new NetworkTelemetrySource("127.0.0.1", port, schema, NetworkProtocol.Tcp);
        var tcs = new TaskCompletionSource<DecodedPacket>();
        source.PacketReceived += (packet) => tcs.TrySetResult(packet);

        var serverTask = Task.Run(async () => {
            using var client = await listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();
            // Packet: AA 55 (sync) 34 12 (val1=0x1234=4660) 78 56 34 12 (val2=0x12345678=305419896)
            byte[] data = [0xAA, 0x55, 0x34, 0x12, 0x78, 0x56, 0x34, 0x12];
            await stream.WriteAsync(data);
            await stream.FlushAsync();
        });

        try
        {
            source.Start();

            // 3. Wait for receipt
            var receivedPacket = await Task.WhenAny(tcs.Task, Task.Delay(2000)) == tcs.Task 
                ? await tcs.Task 
                : null;

            // 4. Verify
            receivedPacket.Should().NotBeNull("Packet should be received within timeout");
            receivedPacket!.Fields["val1"].Should().Be((uint)4660);
            receivedPacket!.Fields["val2"].Should().Be(305419896);
        }
        finally
        {
            source.Stop();
            listener.Stop();
            await serverTask;
        }
    }

    private static int GetFreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        int port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }
}
