namespace ServiceLib.Services;

/// <summary>
/// 自动化控制业务接口：与 UI 无关，供 GUI、本机 HTTP 控制口、MCP 等共用。
/// </summary>
public interface IAutoControlService
{
    /// <summary>
    /// 切换活动节点后由宿主注入的“应用配置/重载”动作（GUI 中对应 MainWindowViewModel.Reload）。
    /// 未注入时仅更新配置，不重启核心。
    /// </summary>
    Func<Task>? ReloadAction { get; set; }

    /// <summary>读取可测速节点（按订阅过滤）。</summary>
    Task<object> GetAutoTestNodes(string? subid);

    /// <summary>对目标节点集执行测速并把最优节点设为活动服务器。</summary>
    Task<object> AutoPickBestServerAsync(string? subid, string action = "Realping");

    /// <summary>当前活动服务器与本地端口。</summary>
    Task<object> GetAutoStatus();

    /// <summary>最近一次统计到的实时速率快照(上行/下行/local/局域网)。</summary>
    Task<object> GetRealtimeSpeed();

    /// <summary>由统计回调推送实时速率快照。</summary>
    void UpdateRealtimeSpeed(ServerSpeedItem speed);
}
