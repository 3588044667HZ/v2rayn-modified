using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using ServiceLib.ViewModels;

namespace v2rayN.Handler;

/// <summary>
/// 本机 HTTP 控制口：供爬虫/脚本程序化"测速并自动选择活动节点"。
///
/// 端点（仅 127.0.0.1）：
///   GET /status                 当前活动节点 + 本地 socks/http 端口
///   GET /list?sub=<subid>       某订阅下可测速节点（sub 为空=全部）
///   GET /pick?sub=<subid>&action=Realping|Speedtest|Mixedtest&auto=1
///                                 测速并把最优节点设为活动（auto=1 时自动切换）
/// 鉴权：启动前设环境变量 V2RAYN_CTL_TOKEN；未设则仅限本机、不做鉴权。
/// 端口：环境变量 V2RAYN_CTL_PORT（默认 8193）。
/// </summary>
public sealed class AutoCtlServer : IDisposable
{
    private readonly MainWindowViewModel _vm;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly string? _token;

    public AutoCtlServer(MainWindowViewModel vm)
    {
        _vm = vm;
        _token = Environment.GetEnvironmentVariable("V2RAYN_CTL_TOKEN");
        var port = int.TryParse(Environment.GetEnvironmentVariable("V2RAYN_CTL_PORT"), out var p) ? p : 8193;
        _listener = new TcpListener(IPAddress.Loopback, port);
    }

    public void Start()
    {
        _listener.Start();
        var t = new Thread(AcceptLoop) { IsBackground = true };
        t.Start();
    }

    private void AcceptLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient? client = null;
            try
            {
                client = _listener.AcceptTcpClient();
            }
            catch
            {
                break;
            }
            if (client != null)
            {
                _ = ThreadPool.QueueUserWorkItem(_ => Handle(client));
            }
        }
    }

    private async void Handle(TcpClient client)
    {
        try
        {
            using var stream = client.GetStream();
            stream.ReadTimeout = 10000;
            var buf = new List<byte>();
            var tmp = new byte[1024];
            int headerEnd = -1;
            while (buf.Count < 8192)
            {
                int n;
                try
                {
                    n = stream.Read(tmp, 0, tmp.Length);
                }
                catch
                {
                    break;
                }
                if (n <= 0)
                {
                    break;
                }
                buf.AddRange(tmp.Take(n));
                headerEnd = IndexOfDoubleCrlf(buf);
                if (headerEnd >= 0)
                {
                    break;
                }
            }
            var reqText = Encoding.UTF8.GetString(buf.ToArray());
            var firstLine = reqText.Split('\n').FirstOrDefault()?.Trim() ?? "";
            var parts = firstLine.Split(' ');
            var method = parts.Length > 0 ? parts[0] : "";
            var pathAndQuery = parts.Length > 1 ? parts[1] : "/";

            if (!string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                Respond(stream, 405, "{\"ok\":false,\"msg\":\"only GET\"}");
                return;
            }

            var (path, query) = SplitPathQuery(pathAndQuery);
            if (!string.IsNullOrEmpty(_token) && (!query.TryGetValue("token", out var tk) || tk != _token))
            {
                Respond(stream, 401, "{\"ok\":false,\"msg\":\"bad token\"}");
                return;
            }

            object? payload = null;
            Exception? error = null;
            // VM 方法在后台线程 await 执行即可：其 UI 通知(如 Reload)已通过 UpdateViewHandler 内部 Dispatcher 封送
            try
            {
                payload = await DispatchAsync(path, query);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            if (error != null)
            {
                Respond(stream, 500, JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["ok"] = false,
                    ["msg"] = error.Message,
                }));
            }
            else
            {
                Respond(stream, 200, JsonSerializer.Serialize(payload));
            }
        }
        catch (Exception)
        {
            // ignore
        }
        finally
        {
            client.Close();
        }
    }

    private async Task<object> DispatchAsync(string path, Dictionary<string, string> query)
    {
        return path switch
        {
            "/status" => await _vm.GetAutoStatus(),
            "/list" => await _vm.GetAutoTestNodes(GetSub(query)),
            "/pick" => await _vm.AutoPickBestServerAsync(GetSub(query), query.GetValueOrDefault("action", "Realping")),
            _ => new Dictionary<string, object?> { ["ok"] = false, ["msg"] = "unknown path" },
        };
    }

    private static string? GetSub(Dictionary<string, string> query)
    {
        var v = query.GetValueOrDefault("sub", "");
        return string.IsNullOrEmpty(v) ? null : Uri.UnescapeDataString(v);
    }

    private static (string, Dictionary<string, string>) SplitPathQuery(string pathAndQuery)
    {
        var i = pathAndQuery.IndexOf('?');
        var path = i < 0 ? pathAndQuery : pathAndQuery[..i];
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (i >= 0)
        {
            foreach (var kv in pathAndQuery[(i + 1)..].Split('&'))
            {
                var j = kv.IndexOf('=');
                if (j < 0)
                {
                    dict[kv] = "";
                }
                else
                {
                    dict[Uri.UnescapeDataString(kv[..j])] = Uri.UnescapeDataString(kv[(j + 1)..]);
                }
            }
        }
        return (path, dict);
    }

    private static int IndexOfDoubleCrlf(List<byte> data)
    {
        for (var i = 0; i < data.Count - 3; i++)
        {
            if (data[i] == 13 && data[i + 1] == 10 && data[i + 2] == 13 && data[i + 3] == 10)
            {
                return i + 3;
            }
        }
        return -1;
    }

    private static void Respond(NetworkStream stream, int code, string json)
    {
        var status = code switch { 200 => "OK", 401 => "Unauthorized", 404 => "Not Found", 405 => "Method Not Allowed", 500 => "Internal Server Error", _ => "Error" };
        var body = Encoding.UTF8.GetBytes(json);
        var head = Encoding.UTF8.GetBytes(
            $"HTTP/1.1 {code} {status}\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {body.Length}\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n");
        try
        {
            stream.Write(head, 0, head.Length);
            stream.Write(body, 0, body.Length);
            stream.Flush();
        }
        catch
        {
            // ignore
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _listener.Stop();
        }
        catch
        {
            // ignore
        }
    }
}
