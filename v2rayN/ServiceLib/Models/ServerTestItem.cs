namespace ServiceLib.Models;

[Serializable]
public class ServerTestItem
{
    public string? IndexId { get; set; }
    public string? Address { get; set; }
    public int Port { get; set; }
    public EConfigType ConfigType { get; set; }
    public bool AllowTest { get; set; }
    public int QueueNum { get; set; }

    /// <summary>节点所属订阅 ID（用于解析订阅级测速 URL 覆盖）。</summary>
    public string? Subid { get; set; }

    /// <summary>订阅级延迟测速 URL 覆盖；空则用全局。</summary>
    public string? SpeedPingTestUrl { get; set; }

    /// <summary>订阅级带宽测速 URL 覆盖；空则用全局。</summary>
    public string? SpeedTestUrl { get; set; }
}
