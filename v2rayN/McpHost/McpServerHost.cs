using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceLib.Common;
using ServiceLib.Services;

namespace v2rayN.McpHost;

/// <summary>
/// 进程内 MCP (Streamable HTTP) 宿主。仅监听 127.0.0.1。
/// 开关：设置里的 EnableMcp(重启生效)；环境变量 V2RAYN_MCP_ENABLE(0/1) 可强制覆盖。端口 V2RAYN_MCP_PORT(默认 8194)。
/// </summary>
public sealed class McpServerHost : IDisposable
{
    private WebApplication? _app;
    private readonly IAutoControlService _service;
    private readonly int _port;

    private McpServerHost(IAutoControlService service, int port)
    {
        _service = service;
        _port = port;
    }

    public static McpServerHost? TryStart(IAutoControlService service, bool enableFromConfig = false)
    {
        if (!IsEnabled(enableFromConfig))
        {
            return null;
        }
        var host = new McpServerHost(service, GetPort());
        _ = host.StartAsync();
        return host;
    }

    /// <summary>
    /// 优先级：环境变量 V2RAYN_MCP_ENABLE(0/1) > 配置项 EnableMcp。
    /// </summary>
    private static bool IsEnabled(bool enableFromConfig)
    {
        var v = Environment.GetEnvironmentVariable("V2RAYN_MCP_ENABLE");
        if (string.Equals(v, "0", StringComparison.OrdinalIgnoreCase)
            || string.Equals(v, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        if (string.Equals(v, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return enableFromConfig;
    }

    private static int GetPort()
    {
        var v = Environment.GetEnvironmentVariable("V2RAYN_MCP_PORT");
        return int.TryParse(v, out var p) && p > 0 ? p : 8194;
    }

    private async Task StartAsync()
    {
        try
        {
            var builder = WebApplication.CreateSlimBuilder([]);
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls($"http://127.0.0.1:{_port}");
            builder.Services.AddSingleton(_service);
            builder.Services.AddMcpServer()
                .WithHttpTransport()
                .WithTools(new AutoCtlTools(_service));

            var app = builder.Build();
            app.MapMcp("/mcp");
            await app.StartAsync();
            _app = app;
            Logging.SaveLog($"[MCP] listening on http://127.0.0.1:{_port}/mcp");
        }
        catch (Exception ex)
        {
            Logging.SaveLog("[MCP] start failed", ex);
        }
    }

    public void Dispose()
    {
        try
        {
            if (_app == null)
            {
                return;
            }
            _app.StopAsync().GetAwaiter().GetResult();
            _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _app = null;
        }
        catch
        {
            // ignore
        }
    }
}
