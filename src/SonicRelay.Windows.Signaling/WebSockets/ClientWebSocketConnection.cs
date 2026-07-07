using System.Buffers;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;

namespace SonicRelay.Windows.Signaling.WebSockets;

internal sealed class ClientWebSocketConnectionFactory : IWebSocketConnectionFactory
{
    public IWebSocketConnection Create() => new ClientWebSocketConnection();
}

internal sealed class ClientWebSocketConnection : IWebSocketConnection
{
    private readonly ClientWebSocket socket = new();

    public ClientWebSocketConnection()
    {
        // Protocol-level keepalive: send Pings every 20 s and abort the socket if
        // no Pong arrives within 10 s. A silently dropped connection then surfaces
        // as a WebSocketException from ReceiveAsync, feeding the reconnect path
        // instead of leaving the UI stuck on "Connected".
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
        socket.Options.KeepAliveTimeout = TimeSpan.FromSeconds(10);
    }

    public WebSocketState State => socket.State;

    public Task ConnectAsync(Uri uri, string accessToken, CancellationToken cancellationToken)
    {
        socket.Options.SetRequestHeader("Authorization", new AuthenticationHeaderValue("Bearer", accessToken).ToString());
        return socket.ConnectAsync(uri, cancellationToken);
    }

    public Task SendTextAsync(string message, CancellationToken cancellationToken) =>
        socket.SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, true, cancellationToken);

    public async Task<WebSocketInboundMessage> ReceiveAsync(CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(4096);
        try
        {
            using var content = new MemoryStream();
            ValueWebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return new WebSocketInboundMessage(result.MessageType, null, socket.CloseStatus);
                }
                content.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            return new WebSocketInboundMessage(result.MessageType, Encoding.UTF8.GetString(content.GetBuffer(), 0, checked((int)content.Length)), null);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public Task CloseAsync(WebSocketCloseStatus status, string description, CancellationToken cancellationToken) =>
        socket.State is WebSocketState.Open or WebSocketState.CloseReceived
            ? socket.CloseAsync(status, description, cancellationToken)
            : Task.CompletedTask;

    public ValueTask DisposeAsync()
    {
        socket.Dispose();
        return ValueTask.CompletedTask;
    }
}
