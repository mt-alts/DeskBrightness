using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DeskBrightness.Test;

public sealed class MobileProtocolTests
{
    [Fact]
    public async Task SendAndReceiveFrame()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var server = AcceptThenSendAckAsync(listener);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using var stream = client.GetStream();

        var json = "{\"lux\":100.5,\"source\":\"lightSensor\"}";
        await SendFrameAsync(stream, json);

        int ack = await ReadAckAsync(stream);
        (string receivedJson, _) = await server;

        Assert.Equal(json, receivedJson);
        Assert.Equal(0, ack);
    }

    [Fact]
    public async Task SendMultipleFramesOnSameConnection()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var server = AcceptThenReadMultipleAsync(listener, 3);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using var stream = client.GetStream();

        var frames = new[]
        {
            "{\"lux\":10.0,\"source\":\"lightSensor\"}",
            "{\"lux\":20.0,\"source\":\"lightSensor\"}",
            "{\"brightness\":75,\"source\":\"screenBrightness\"}"
        };

        foreach (var frame in frames)
        {
            await SendFrameAsync(stream, frame);
            int ack = await ReadAckAsync(stream);
            Assert.Equal(0, ack);
        }

        var received = await server;
        Assert.Equal(frames, received);
    }

    [Fact]
    public async Task InvalidFrameLength_ReturnsErrorAck()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        _ = ServerThatRejectsInvalidLengthAsync(listener);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using var stream = client.GetStream();

        var lengthBuffer = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(lengthBuffer, -1);
        await stream.WriteAsync(lengthBuffer);

        int ack = await ReadAckAsync(stream);
        Assert.Equal(1, ack);
    }

    private static async Task SendFrameAsync(NetworkStream stream, string json)
    {
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var lengthBuffer = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(lengthBuffer, jsonBytes.Length);

        await stream.WriteAsync(lengthBuffer);
        await stream.WriteAsync(jsonBytes);
        await stream.FlushAsync();
    }

    private static async Task<int> ReadAckAsync(NetworkStream stream)
    {
        var buffer = new byte[1];
        await ReadExactlyAsync(stream, buffer);
        return buffer[0];
    }

    private static async Task<(string, int)> AcceptThenSendAckAsync(TcpListener listener)
    {
        using var client = await listener.AcceptTcpClientAsync();
        using var stream = client.GetStream();

        var lengthBuffer = new byte[4];
        await ReadExactlyAsync(stream, lengthBuffer);
        int length = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);

        var jsonBuffer = new byte[length];
        await ReadExactlyAsync(stream, jsonBuffer);
        var json = Encoding.UTF8.GetString(jsonBuffer);

        stream.WriteByte(0);
        await stream.FlushAsync();

        return (json, 0);
    }

    private static async Task<string[]> AcceptThenReadMultipleAsync(
        TcpListener listener, int count)
    {
        var frames = new List<string>();
        using var client = await listener.AcceptTcpClientAsync();
        using var stream = client.GetStream();

        for (int i = 0; i < count; i++)
        {
            var lengthBuffer = new byte[4];
            await ReadExactlyAsync(stream, lengthBuffer);
            int length = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);

            var jsonBuffer = new byte[length];
            await ReadExactlyAsync(stream, jsonBuffer);
            frames.Add(Encoding.UTF8.GetString(jsonBuffer));

            stream.WriteByte(0);
            await stream.FlushAsync();
        }

        return frames.ToArray();
    }

    private static async Task ServerThatRejectsInvalidLengthAsync(TcpListener listener)
    {
        using var client = await listener.AcceptTcpClientAsync();
        using var stream = client.GetStream();

        var lengthBuffer = new byte[4];
        await ReadExactlyAsync(stream, lengthBuffer);
        int length = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);

        stream.WriteByte((byte)(length <= 0 || length > 65536 ? 1 : 0));
        await stream.FlushAsync();
    }

    private static async Task ReadExactlyAsync(NetworkStream stream, byte[] buffer)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset));
            if (read == 0)
                throw new EndOfStreamException("Client disconnected");
            offset += read;
        }
    }
}
