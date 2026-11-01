namespace ServiceLib.Handler.Fmt;

/// <summary>
/// HTTP 代理分享链接解析（http://host:port 或带可选用户口令），用于导入裸 HTTP 代理节点。
/// 注：与订阅 URL 同前缀 http://，故仅当该行可解析为“无路径的主机:端口”时才接受。
/// </summary>
public class HttpFmt : BaseFmt
{
    public static ProfileItem? Resolve(string str, out string msg)
    {
        msg = ResUI.ConfigurationFormatIncorrect;

        var parsedUrl = Utils.TryUri(str);
        if (parsedUrl == null)
        {
            return null;
        }
        var scheme = parsedUrl.Scheme.ToLowerInvariant();
        if (scheme != "http" && scheme != "https")
        {
            return null;
        }
        // 带路径的普通网页地址不算代理节点
        var path = parsedUrl.AbsolutePath;
        if (!string.IsNullOrEmpty(path) && path != "/")
        {
            return null;
        }
        var item = new ProfileItem()
        {
            ConfigType = EConfigType.HTTP,
            Remarks = parsedUrl.GetComponents(UriComponents.Fragment, UriFormat.Unescaped),
            Address = parsedUrl.IdnHost,
            Port = parsedUrl.Port,
        };
        if (string.IsNullOrEmpty(item.Address) || item.Port <= 0)
        {
            return null;
        }
        // 可选 user:password（明文 http 形式，如 http://user:pass@host:port）
        var userInfo = parsedUrl.UserInfo;
        if (!string.IsNullOrEmpty(userInfo) && userInfo.Contains(':'))
        {
            var parts = userInfo.Split([':'], 2);
            item.Security = Utils.UrlDecode(parts[0]);
            item.Id = Utils.UrlDecode(parts[1]);
        }
        if (item.Remarks.IsNullOrEmpty())
        {
            item.Remarks = $"{item.Address}:{item.Port}";
        }
        return item;
    }

    public static string? ToUri(ProfileItem? item)
    {
        if (item == null || string.IsNullOrEmpty(item.Address) || item.Port <= 0)
        {
            return null;
        }
        var remark = string.Empty;
        if (item.Remarks.IsNotEmpty())
        {
            remark = "#" + Utils.UrlEncode(item.Remarks);
        }
        var auth = string.Empty;
        if (item.Security.IsNotEmpty() || item.Id.IsNotEmpty())
        {
            auth = $"{Utils.UrlEncode(item.Security ?? "")}:{Utils.UrlEncode(item.Id ?? "")}@";
        }
        return $"http://{auth}{item.Address}:{item.Port}{remark}";
    }
}
