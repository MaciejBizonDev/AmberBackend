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
            try
            {
                var ctx = await _listener.GetContextAsync();
                if (!ctx.Request.IsWebSocketRequest)
                {
                    ctx.Response.StatusCode = 400; 
                    ctx.Response.Close(); 
                    continue;
                }

                var wsctx = await ctx.AcceptWebSocketAsync(null);
                // Fire and forget with proper error handling
                _ = Task.Run(async () => await HandleClientAsync(wsctx.WebSocket), ct);
            }
            catch (HttpListenerException ex)
            {
                if (ct.IsCancellationRequested) break;
                Console.WriteLine($"HttpListener error: {ex.Message}");
            }
        }
    }

    private async Task HandleClientAsync(WebSocket ws)
    {
        var buffer = new byte[4096];
        string currentPlayerId = null;

        try
        {
            while (ws.State == WebSocketState.Open)
            {
                try
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", CancellationToken.None);
                        break;
                    }

                    if (result.MessageType != WebSocketMessageType.Text) 
                        continue;

                    string msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var baseMsg = JsonConvert.DeserializeObject<BaseMessage>(msg);
                    if (baseMsg == null || string.IsNullOrEmpty(baseMsg.type)) 
                        continue;

                    currentPlayerId = await _handlers.HandleMessageAsync(ws, baseMsg.type, msg, currentPlayerId);
                }
                catch (WebSocketException)
                {
                    // Client disconnected - exit loop
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in WebSocket handler: {ex.Message}");
        }
        finally
        {
            // Cleanup player on disconnect
            if (ws.State != WebSocketState.Closed)
            {
                try
                {
                    await ws.CloseAsync(WebSocketCloseStatus.InternalServerError, "Connection closed", CancellationToken.None);
                }
                catch { /* Ignore */ }
            }
        }
    }
}
