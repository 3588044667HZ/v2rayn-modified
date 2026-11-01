using System.Reactive;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace ServiceLib.ViewModels;

public class SubEditViewModel : MyReactiveObject
{
    [Reactive]
    public SubItem SelectedSource { get; set; }

    /// <summary>本订阅专属延迟测速 URL（留空用全局）。</summary>
    [Reactive]
    public string? SubPingTestUrl { get; set; }

    /// <summary>本订阅专属带宽测速 URL（留空用全局）。</summary>
    [Reactive]
    public string? SubSpeedTestUrl { get; set; }

    public ReactiveCommand<Unit, Unit> SaveCmd { get; }

    public SubEditViewModel(SubItem subItem, Func<EViewAction, object?, Task<bool>>? updateView)
    {
        _config = AppHandler.Instance.Config;
        _updateView = updateView;

        SaveCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SaveSubAsync();
        });

        SelectedSource = subItem.Id.IsNullOrEmpty() ? subItem : JsonUtils.DeepCopy(subItem);
        var urls = ConfigHandler.GetSubSpeedTestUrls(_config, subItem.Id);
        SubPingTestUrl = urls.PingTestUrl;
        SubSpeedTestUrl = urls.SpeedTestUrl;
    }

    private async Task SaveSubAsync()
    {
        var remarks = SelectedSource.Remarks;
        if (remarks.IsNullOrEmpty())
        {
            NoticeHandler.Instance.Enqueue(ResUI.PleaseFillRemarks);
            return;
        }

        var url = SelectedSource.Url;
        if (url.IsNotEmpty())
        {
            var uri = Utils.TryUri(url);
            if (uri == null)
            {
                NoticeHandler.Instance.Enqueue(ResUI.InvalidUrlTip);
                return;
            }
            //Do not allow http protocol
            if (url.StartsWith(Global.HttpProtocol) && !Utils.IsPrivateNetwork(uri.IdnHost))
            {
                NoticeHandler.Instance.Enqueue(ResUI.InsecureUrlProtocol);
                //return;
            }
        }

        if (await ConfigHandler.AddSubItem(_config, SelectedSource, SubPingTestUrl, SubSpeedTestUrl) == 0)
        {
            NoticeHandler.Instance.Enqueue(ResUI.OperationSuccess);
            _updateView?.Invoke(EViewAction.CloseWindow, null);
        }
        else
        {
            NoticeHandler.Instance.Enqueue(ResUI.OperationFailed);
        }
    }
}
