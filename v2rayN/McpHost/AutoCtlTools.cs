using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using ServiceLib.Services;

namespace v2rayN.McpHost;

/// <summary>
/// 通过 MCP 暴露的自动化控制工具，全部委托给 IAutoControlService。
/// </summary>
[McpServerToolType]
public sealed class AutoCtlTools
{
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = false };
    private readonly IAutoControlService _svc;

    public AutoCtlTools(IAutoControlService svc)
    {
        _svc = svc;
    }

    [McpServerTool, Description("获取当前活动节点、运行状态与本地 socks/http 端口")]
    public async Task<string> get_status()
        => JsonSerializer.Serialize(await _svc.GetAutoStatus(), _json);

    [McpServerTool, Description("获取最近一次实时速率快照(上行/下行/local/局域网)。需先开启显示实时速度")]
    public async Task<string> get_speed()
        => JsonSerializer.Serialize(await _svc.GetRealtimeSpeed(), _json);

    [McpServerTool, Description("列出可测速节点；subId 为空表示全部")]
    public async Task<string> list_nodes(string? subId = null)
        => JsonSerializer.Serialize(await _svc.GetAutoTestNodes(subId), _json);

    [McpServerTool, Description("对节点测速并切换到最优节点。action: Realping | Speedtest | Mixedtest | Tcping")]
    public async Task<string> pick_best(string? subId = null, string action = "Realping")
        => JsonSerializer.Serialize(await _svc.AutoPickBestServerAsync(subId, action), _json);
}
