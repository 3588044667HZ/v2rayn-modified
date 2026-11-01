namespace ServiceLib.Services.Statistics;

public class StatisticsXrayService
{
    private const long linkBase = 1024;
    private ServerSpeedItem _serverSpeedItem = new();
    private readonly Config _config;
    private bool _exitFlag;
    private Action<ServerSpeedItem>? _updateFunc;
    private string Url => $"{Global.HttpProtocol}{Global.Loopback}:{AppHandler.Instance.StatePort}/debug/vars";

    public StatisticsXrayService(Config config, Action<ServerSpeedItem> updateFunc)
    {
        _config = config;
        _updateFunc = updateFunc;
        _exitFlag = false;

        _ = Task.Run(Run);
    }

    public void Close()
    {
        _exitFlag = true;
    }

    private async Task Run()
    {
        while (!_exitFlag)
        {
            await Task.Delay(1000);
            try
            {
                if (_config.RunningCoreType != ECoreType.Xray)
                {
                    continue;
                }

                var result = await HttpClientHelper.Instance.TryGetAsync(Url);
                if (result != null)
                {
                    var server = ParseOutput(result) ?? new ServerSpeedItem();
                    _updateFunc?.Invoke(server);
                }
            }
            catch
            {
                // ignored
            }
        }
    }

    private ServerSpeedItem? ParseOutput(string result)
    {
        try
        {
            var source = JsonUtils.Deserialize<V2rayMetricsVars>(result);
            if (source?.stats == null)
            {
                return null;
            }

            ServerSpeedItem server = new();
            if (source.stats.outbound != null)
            {
                foreach (var key in source.stats.outbound.Keys.Cast<string>())
                {
                    var value = source.stats.outbound[key];
                    if (value == null)
                    {
                        continue;
                    }
                    var state = JsonUtils.Deserialize<V2rayMetricsVarsLink>(value.ToString());

                    if (key.StartsWith(Global.ProxyTag))
                    {
                        server.ProxyUp += state.uplink / linkBase;
                        server.ProxyDown += state.downlink / linkBase;
                    }
                    else if (key == Global.DirectTag)
                    {
                        server.DirectUp = state.uplink / linkBase;
                        server.DirectDown = state.downlink / linkBase;
                    }
                }
            }

            if (source.stats.inbound != null)
            {
                //socks/socks2 为本地回环入站，socks3 为局域网入站
                foreach (var key in source.stats.inbound.Keys.Cast<string>())
                {
                    var value = source.stats.inbound[key];
                    if (value == null)
                    {
                        continue;
                    }
                    var state = JsonUtils.Deserialize<V2rayMetricsVarsLink>(value.ToString());

                    if (key == EInboundProtocol.socks.ToString() || key == EInboundProtocol.socks2.ToString())
                    {
                        server.LocalUp += state.uplink / linkBase;
                        server.LocalDown += state.downlink / linkBase;
                    }
                    else if (key == EInboundProtocol.socks3.ToString())
                    {
                        server.LanUp += state.uplink / linkBase;
                        server.LanDown += state.downlink / linkBase;
                    }
                }
            }

            if (server.DirectDown < _serverSpeedItem.DirectDown
                || server.ProxyDown < _serverSpeedItem.ProxyDown
                || server.LocalDown < _serverSpeedItem.LocalDown
                || server.LanDown < _serverSpeedItem.LanDown)
            {
                _serverSpeedItem = new();
                return null;
            }

            ServerSpeedItem curItem = new()
            {
                ProxyUp = server.ProxyUp - _serverSpeedItem.ProxyUp,
                ProxyDown = server.ProxyDown - _serverSpeedItem.ProxyDown,
                DirectUp = server.DirectUp - _serverSpeedItem.DirectUp,
                DirectDown = server.DirectDown - _serverSpeedItem.DirectDown,
                LocalUp = server.LocalUp - _serverSpeedItem.LocalUp,
                LocalDown = server.LocalDown - _serverSpeedItem.LocalDown,
                LanUp = server.LanUp - _serverSpeedItem.LanUp,
                LanDown = server.LanDown - _serverSpeedItem.LanDown,
            };
            _serverSpeedItem = server;
            return curItem;
        }
        catch
        {
            // ignored
        }

        return null;
    }
}
