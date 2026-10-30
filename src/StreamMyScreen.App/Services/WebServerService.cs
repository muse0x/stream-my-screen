using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace StreamMyScreen.App.Services;

public class WebServerService
{
    private IHost? _host;
    private readonly ConcurrentDictionary<string, WebSocket> _receivers = new();
    private readonly ConcurrentDictionary<string, WebSocket> _senders = new();

    public string LocalIp { get; private set; } = "127.0.0.1";
    public int HttpsPort { get; } = 8443;
    public int HttpPort { get; } = 8080;
    public bool IsRunning { get; private set; }

    public event Action<string, bool>? ClientStateChanged;

    public async Task StartAsync()
    {
        if (IsRunning)
        {
            return;
        }

        LocalIp = LanDiscoveryService.GetActiveLocalIp();
        var cert = CertificateService.GenerateSelfSignedCertificate(LocalIp);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Any, HttpsPort, listenOptions =>
            {
                listenOptions.UseHttps(cert);
            });
            options.Listen(IPAddress.Any, HttpPort);
        });

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
        });

        var app = builder.Build();
        app.UseCors();
        app.UseWebSockets();

        var uiDir = ResolveUiDirectory();
        if (Directory.Exists(uiDir))
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(uiDir),
                RequestPath = ""
            });
        }

        // Redirect root to receiver for PC
        app.MapGet("/", (HttpContext ctx) => Results.Redirect("/receiver/index.html"));

        // Status API
        app.MapGet("/api/status", () => Results.Json(new
        {
            localIp = LocalIp,
            httpsPort = HttpsPort,
            httpPort = HttpPort,
            senderUrl = $"https://{LocalIp}:{HttpsPort}/mobile/index.html"
        }));

        // Launch Windows Miracast receiver / settings
        app.MapPost("/api/launch-miracast", () =>
        {
            try
            {
                var wirelessDisplayPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WirelessDisplay.exe");
                var connectAppPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "ConnectApp.exe");

                if (File.Exists(wirelessDisplayPath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(wirelessDisplayPath) { UseShellExecute = true });
                }
                else if (File.Exists(connectAppPath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(connectAppPath) { UseShellExecute = true });
                }
                else
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:projecting-to-this-pc") { UseShellExecute = true });
                }

                return Results.Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        });

        // WebSocket Signaling endpoint
        app.Map("/ws", async (HttpContext context) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            var role = context.Request.Query["role"].ToString().ToLowerInvariant();
            var socket = await context.WebSockets.AcceptWebSocketAsync();
            var connectionId = Guid.NewGuid().ToString("N");

            if (role == "receiver")
            {
                _receivers[connectionId] = socket;
                ClientStateChanged?.Invoke("Receiver", true);
            }
            else
            {
                _senders[connectionId] = socket;
                ClientStateChanged?.Invoke("Sender", true);
                await BroadcastToReceiversAsync(JsonSerializer.Serialize(new { type = "sender-joined" }));
            }

            await HandleWebSocketAsync(socket, role, connectionId);
        });

        _host = app;
        await _host.StartAsync();
        IsRunning = true;
    }

    public async Task StopAsync()
    {
        if (!IsRunning || _host == null)
        {
            return;
        }

        await _host.StopAsync();
        _host.Dispose();
        _host = null;
        IsRunning = false;
    }

    private async Task HandleWebSocketAsync(WebSocket socket, string role, string connectionId)
    {
        var buffer = new byte[1024 * 64];

        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    break;
                }

                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                if (role == "sender")
                {
                    // Relay from Android to PC receivers
                    await BroadcastToReceiversAsync(message);
                }
                else
                {
                    // Relay from PC receiver to Android senders
                    await BroadcastToSendersAsync(message);
                }
            }
        }
        catch
        {
            // Socket disconnected
        }
        finally
        {
            if (role == "receiver")
            {
                _receivers.TryRemove(connectionId, out _);
                ClientStateChanged?.Invoke("Receiver", false);
            }
            else
            {
                _senders.TryRemove(connectionId, out _);
                ClientStateChanged?.Invoke("Sender", false);
                await BroadcastToReceiversAsync(JsonSerializer.Serialize(new { type = "sender-left" }));
            }
        }
    }

    private async Task BroadcastToReceiversAsync(string message)
    {
        var data = Encoding.UTF8.GetBytes(message);
        foreach (var (id, socket) in _receivers)
        {
            if (socket.State == WebSocketState.Open)
            {
                await socket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }

    private async Task BroadcastToSendersAsync(string message)
    {
        var data = Encoding.UTF8.GetBytes(message);
        foreach (var (id, socket) in _senders)
        {
            if (socket.State == WebSocketState.Open)
            {
                await socket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }

    private static string ResolveUiDirectory()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "UI"),
            Path.Combine(baseDir, "..", "UI"),
            Path.Combine(baseDir, "..", "..", "..", "UI"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "StreamMyScreen.App", "UI"),
            Path.Combine(Directory.GetCurrentDirectory(), "UI")
        };

        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
    }
}
