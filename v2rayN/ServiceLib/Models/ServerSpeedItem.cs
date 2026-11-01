namespace ServiceLib.Models;

[Serializable]
public class ServerSpeedItem : ServerStatItem
{
    public long ProxyUp { get; set; }

    public long ProxyDown { get; set; }

    public long DirectUp { get; set; }

    public long DirectDown { get; set; }

    // 本地回环入站(socks/socks2)的实时上传/下载速率
    public long LocalUp { get; set; }

    public long LocalDown { get; set; }

    // 局域网入站(socks3)的实时上传/下载速率
    public long LanUp { get; set; }

    public long LanDown { get; set; }
}

[Serializable]
public class TrafficItem
{
    public ulong Up { get; set; }

    public ulong Down { get; set; }
}
