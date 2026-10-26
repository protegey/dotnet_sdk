using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Protegey.Sdk.Tests;

/// <summary>
/// Local HTTP server (<see cref="HttpListener"/>, built into .NET — no mocking library needed)
/// instead of a real network call to Protegey's API. Same approach as the Java SDK's tests
/// (<c>com.sun.net.httpserver.HttpServer</c>).
/// </summary>
internal sealed class FakeServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly Func<HttpListenerContext, string, Task> _handler;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;

    public string BaseUrl { get; }
    public string? LastRequestBody { get; private set; }

    public FakeServer(Func<HttpListenerContext, string, Task> handler)
    {
        _handler = handler;
        var port = GetFreePort();
        BaseUrl = $"http://localhost:{port}";
        _listener = new HttpListener();
        _listener.Prefixes.Add($"{BaseUrl}/");
        _listener.Start();
        _loop = Loop();
    }

    /// <summary>Always-200, fixed-body convenience constructor for the common single-response case.</summary>
    public FakeServer(int status, string responseBody) : this(async (context, _) =>
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        var bytes = Encoding.UTF8.GetBytes(responseBody);
        await context.Response.OutputStream.WriteAsync(bytes);
    })
    {
    }

    private async Task Loop()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception) when (_cts.IsCancellationRequested)
            {
                return;
            }

            using var reader = new StreamReader(context.Request.InputStream);
            LastRequestBody = await reader.ReadToEndAsync().ConfigureAwait(false);
            await _handler(context, context.Request.HttpMethod).ConfigureAwait(false);
            context.Response.OutputStream.Close();
        }
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        _listener.Close();
    }
}
