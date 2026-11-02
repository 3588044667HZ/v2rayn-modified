using System.Collections.Concurrent;

namespace ServiceLib.Services;

/// <summary>
/// 自动化控制业务实现：无 UI 依赖。GUI 的 AutoCtlServer / 未来的 MCP host 都调用它。
/// </summary>
public class AutoControlService : IAutoControlService
{
    private readonly Config _config;
    private readonly object _speedLock = new();
    private ServerSpeedItem _lastSpeed = new();
    private DateTime _lastSpeedAt = DateTime.MinValue;

    public AutoControlService(Config config)
    {
        _config = config;
    }

    public Func<Task>? ReloadAction { get; set; }

    public async Task<object> GetAutoTestNodes(string? subid)
    {
        var lst = await LoadAutoTestProfilesAsync(subid);
        var nodes = lst.Select(x => new Dictionary<string, object?>
        {
            ["indexId"] = x.IndexId,
            ["remarks"] = x.Remarks,
            ["address"] = x.Address,
            ["port"] = x.Port,
            ["configType"] = x.ConfigType.ToString(),
            ["subid"] = x.Subid,
        }).ToList();
        return new Dictionary<string, object?> { ["total"] = lst.Count, ["nodes"] = nodes };
    }

    public async Task<object> AutoPickBestServerAsync(string? subid, string action = "Realping")
    {
        var lst = await LoadAutoTestProfilesAsync(subid);
        if (lst.Count == 0)
        {
            return new Dictionary<string, object?> { ["ok"] = false, ["msg"] = "无可测速节点" };
        }
        if (!Enum.TryParse(action, true, out ESpeedActionType actType))
        {
            actType = ESpeedActionType.Realping;
        }

        // 测速结果收集器；SpeedtestService 在跑完后会回调一次 IndexId 为空的结束事件
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var results = new ConcurrentDictionary<string, (string delay, string speed)>();
        var svc = new SpeedtestService(_config, r =>
        {
            if (r.IndexId.IsNullOrEmpty())
            {
                tcs.TrySetResult(true);
            }
            else if (r.IndexId.IsNotEmpty())
            {
                results[r.IndexId!] = (r.Delay ?? "", r.Speed ?? "");
            }
        });
        svc.RunLoop(actType, lst);

        try
        {
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(200));
        }
        catch (TimeoutException)
        {
            svc.ExitLoop();
        }

        // 选优：延迟>0 取最小；延迟并列再比带宽
        string? best = null;
        var bestDelay = int.MaxValue;
        decimal bestSpeed = -1;
        foreach (var p in lst)
        {
            if (!results.TryGetValue(p.IndexId, out var v))
            {
                continue;
            }
            if (!int.TryParse(v.delay, out var delay) || delay <= 0)
            {
                continue;
            }
            var speed = decimal.TryParse(v.speed, out var sp) ? sp : 0;
            if (delay < bestDelay || (delay == bestDelay && speed > bestSpeed))
            {
                bestDelay = delay;
                bestSpeed = speed;
                best = p.IndexId;
            }
        }

        if (best.IsNullOrEmpty())
        {
            return new Dictionary<string, object?> { ["ok"] = false, ["msg"] = "全部节点测速失败" };
        }

        if (best != _config.IndexId)
        {
            if (await ConfigHandler.SetDefaultServerIndex(_config, best) != 0)
            {
                return new Dictionary<string, object?> { ["ok"] = false, ["msg"] = "设置活动服务器失败" };
            }
            if (ReloadAction != null)
            {
                await ReloadAction();
            }
        }
        var item = lst.FirstOrDefault(x => x.IndexId == best);
        return new Dictionary<string, object?>
        {
            ["ok"] = true,
            ["indexId"] = best,
            ["remarks"] = item?.Remarks,
            ["address"] = item?.Address,
            ["port"] = item?.Port,
            ["delayMs"] = bestDelay,
            ["speedKBs"] = bestSpeed >= 0 ? bestSpeed : 0,
            ["testedCount"] = lst.Count,
            ["total"] = lst.Count,
        };
    }

    public async Task<object> GetAutoStatus()
    {
        var node = await AppHandler.Instance.GetProfileItem(_config.IndexId);
        var socksPort = _config.Inbound?.FirstOrDefault(x => x.Protocol == "socks")?.LocalPort ?? 0;
        var httpPort = _config.Inbound?.FirstOrDefault(x => x.Protocol == "http")?.LocalPort ?? 0;
        return new Dictionary<string, object?>
        {
            ["running"] = _config.RunningCoreType != default(ECoreType),
            ["indexId"] = _config.IndexId,
            ["remarks"] = node?.Remarks,
            ["address"] = node?.Address,
            ["port"] = node?.Port,
            ["subid"] = node?.Subid,
            ["socksPort"] = socksPort,
            ["httpPort"] = httpPort,
        };
    }

    public void UpdateRealtimeSpeed(ServerSpeedItem speed)
    {
        if (speed == null)
        {
            return;
        }
        lock (_speedLock)
        {
            // 拷贝字段，避免调用方后续修改影响快照
            _lastSpeed = new ServerSpeedItem
            {
                ProxyUp = speed.ProxyUp,
                ProxyDown = speed.ProxyDown,
                DirectUp = speed.DirectUp,
                DirectDown = speed.DirectDown,
                LocalUp = speed.LocalUp,
                LocalDown = speed.LocalDown,
                LanUp = speed.LanUp,
                LanDown = speed.LanDown,
            };
            _lastSpeedAt = DateTime.Now;
        }
    }

    public Task<object> GetRealtimeSpeed()
    {
        ServerSpeedItem speed;
        DateTime at;
        lock (_speedLock)
        {
            speed = _lastSpeed;
            at = _lastSpeedAt;
        }
        var enabled = _config.GuiItem?.EnableStatistics == true || _config.GuiItem?.DisplayRealTimeSpeed == true;
        var result = new Dictionary<string, object?>
        {
            ["enabled"] = enabled,
            ["stale"] = at == DateTime.MinValue || (DateTime.Now - at).TotalSeconds > 5,
            ["updatedAt"] = at == DateTime.MinValue ? null : at.ToString("yyyy-MM-dd HH:mm:ss"),
            ["proxyUp"] = speed.ProxyUp,
            ["proxyDown"] = speed.ProxyDown,
            ["localUp"] = speed.LocalUp,
            ["localDown"] = speed.LocalDown,
            ["lanUp"] = speed.LanUp,
            ["lanDown"] = speed.LanDown,
            ["proxyUpText"] = $"{Utils.HumanFy(speed.ProxyUp)}/s",
            ["proxyDownText"] = $"{Utils.HumanFy(speed.ProxyDown)}/s",
            ["localUpText"] = $"{Utils.HumanFy(speed.LocalUp)}/s",
            ["localDownText"] = $"{Utils.HumanFy(speed.LocalDown)}/s",
            ["lanUpText"] = $"{Utils.HumanFy(speed.LanUp)}/s",
            ["lanDownText"] = $"{Utils.HumanFy(speed.LanDown)}/s",
        };
        return Task.FromResult<object>(result);
    }

    private async Task<List<ProfileItem>> LoadAutoTestProfilesAsync(string? subid)
    {
        var all = await SQLiteHelper.Instance.TableAsync<ProfileItem>()
            .Where(t => t.ConfigType != EConfigType.Custom && t.Port > 0)
            .ToListAsync();
        return subid.IsNullOrEmpty() ? all : all.Where(t => t.Subid == subid).ToList();
    }
}
