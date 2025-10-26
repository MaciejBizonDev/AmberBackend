using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class WebSocketServer
{
    private readonly HttpListener _listener = new();
    private readonly MessageHandlerService _handlers;

    public WebSocketServer(MessageHandlerService handlers)
    {
        _handlers = handlers;
        _listener.Prefixes.Add("http://localhost:5000/ws/");
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _listener.Start();
        Console.WriteLine("WS server on ws://localhost:5000/ws/");

        while (!ct.IsCancellationRequested)
        {
            var ctx = await _listener.GetContextAsync();
            if (!ctx.Request.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = 400; ctx.Response.Close(); continue;
            }

            var wsctx = await ctx.AcceptWebSocketAsync(null);
            _ = HandleClientAsync(wsctx.WebSocket);
        }
    }

    private async Task HandleClientAsync(WebSocket ws)
    {
        var buffer = new byte[4096];
        string currentPlayerId = null;

        while (ws.State == WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
            if (result.MessageType != WebSocketMessageType.Text) continue;

            string msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
            var baseMsg = JsonConvert.DeserializeObject<TileClickMessage>(msg);
            if (baseMsg == null || string.IsNullOrEmpty(baseMsg.type)) continue;

            currentPlayerId = await _handlers.HandleMessageAsync(ws, baseMsg.type, msg, currentPlayerId);
        }
    }
}
