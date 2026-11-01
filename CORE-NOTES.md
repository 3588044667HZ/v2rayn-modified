# v2rayN 7.11.1 fork 备忘（内核 + 自研功能 + 构建）

> 本 fork 的一切维护信息集中于此：内核基线/升级边界、自研补丁说明、本地构建与用法。
> 内核版本依据：本机 `D:\v2rayN-windows-64-SelfContained\bin\` 实测（2026-09）。

## 1. 当前内核基线

| 内核 | 本机版本 | 备注 |
|---|---|---|
| xray | 25.1.1 | ~2025-01，与 v2rayN 7.11.1 同期 |
| sing-box | 1.11.5 | ~2025 初 |
| mihomo | 1.19.1 | 2024-12 |
| hysteria2 | v2.12.2 | 已为最新 |

上游最新（供对照，2026-09）：xray v26.3.27 / sing-box v1.14.0 / mihomo v1.19.30 / hysteria v2.12.2。

## 2. 核心理念

- 内核由 v2rayN **自动下载**存在 MITM 风险 → 本 fork 一律**手动下载**放入 `bin\`，禁用内置“下载/更新内核”功能。
- fork 的配置生成（v2rayN 7.11.1）只**稳配其同期内核**；升级内核 = 配置漂移风险，不是越新越好。
- 新内核可能删除/改动老选项（例：上游已移除 xray 的 `allowInsecure`）→ 升级后可能“反而连不上”。

## 3. 升级策略

- **默认不升级** xray / sing-box；mihomo 可小升补丁版（同小版本内），hysteria2 已是新无需动。
- 确需升级某个内核时：
  1. 备份旧 exe（如 `xray.exe.bak-2501`）；
  2. 替换后做回归（见下）；
  3. 连不上时**回退内核**，不要改 fork 配置去迁就新内核。

## 4. 内核升级回归清单（每项都过）

- [ ] 日常协议连通：vmess / vless / trojan / reality / hysteria2
- [ ] 裸代理出站：HTTP / SOCKS 节点（网关 /v2rayn 导入的那批）
- [ ] 每订阅级测速 URL（国内/国际分别）仍生效
- [ ] 控制口 `/status`、`/list`、`/pick` 正常工作
- [ ] 内核启动日志无配置告警/拒绝

## 5. 运行目录核对命令

```powershell
D:\v2rayN-windows-64-SelfContained\bin\xray\xray.exe -version
D:\v2rayN-windows-64-SelfContained\bin\sing_box\sing-box.exe version
D:\v2rayN-windows-64-SelfContained\bin\mihomo\mihomo-windows-amd64-compatible.exe -v
D:\v2rayN-windows-64-SelfContained\bin\hysteria2\hysteria-windows-amd64.exe version
```

---

# 附：本 fork 自研功能与操作备忘

## A. 订阅级测速 URL（国内/国际不混淆）

- 编辑订阅时新增两字段：**延迟测速 URL（订阅级）**、**带宽测速 URL（订阅级）**；留空=用全局设置。
- 推荐值：国际 `https://www.gstatic.com/generate_204`；国内 `https://connect.rom.miui.com/generate_204`。
  带宽：国际 `https://speed.cloudflare.com/__down?bytes=52428800`；国内 `https://dldir1.qq.com/weixin/Windows/WeChatSetup.exe`。
- 代码落点：`ServiceLib/Models/ConfigItems.cs`（SubSpeedTestItem）、`Config.cs`、`ConfigHandler`（存取+清理）、
  `Services/SpeedtestService.cs`（按 Subid 覆盖）、`ServerTestItem.cs`、`SubEditViewModel` + WPF/Avalonia 订阅编辑窗口。

## B. HTTP/SOCKS 裸代理池导入（配合 E:\proxy_crawler 网关）

- 网关端点 `/v2rayn` 返回 base64 的 `http://host:port` / `socks://host:port` 节点行，供本 fork 订阅导入。
- fork 补丁：新增 `ServiceLib/Handler/Fmt/HttpFmt.cs`（http/https 分享链接解析，带路径的普通网页会被拒绝）；
  `FmtHandler` 增加 HTTP 分支；批量入库 `ConfigHandler.AddBatchServersCommon` 的 switch 增加 `EConfigType.HTTP` 分支。
- 测速目标即 A 的订阅级/全局 URL；手动测速后手选即可，本批节点类型为 HTTP/SOCKS。

## C. 本机控制口（供爬虫自动化）

- 启动 v2rayN 后监听 `127.0.0.1`，默认端口 8193（环境变量 `V2RAYN_CTL_PORT`）。
- 鉴权：可选，启动前设 `V2RAYN_CTL_TOKEN`；未设则仅限本机访问、不做鉴权。
- 端点：
  - `GET /status` → 活动节点 + 本地 socks/http 端口
  - `GET /list?sub=<subid>` → 订阅下可测速节点（sub 空=全部）
  - `GET /pick?sub=<subid>&action=Realping|Speedtest|Mixedtest` → 测速并把最优（延迟最小/同延迟比带宽）设为活动节点并重启内核
- 代码落点：`ServiceLib/ViewModels/MainWindowViewModel.cs`（GetAutoStatus/GetAutoTestNodes/AutoPickBestServerAsync）、
  `v2rayN/Handler/AutoCtlServer.cs`、`v2rayN/Views/MainWindow.xaml.cs`（启动/释放）。
- 依赖：需 v2rayN 常驻运行；未运行内核时 /pick 只改默认节点、下次启动生效。

## D. 本地构建与发布

```powershell
# ServiceLib 快速编译校验
dotnet build "E:\v2rayN-source\v2rayN\ServiceLib\ServiceLib.csproj" -c Release

# 完整多文件发布（非单文件）
dotnet publish "E:\v2rayN-source\v2rayN\v2rayN\v2rayN.csproj" -c Release -r win-x64 --self-contained -p:PublishSingleFile=false -o "E:\v2rayN-source\publish\v2rayN-normal"
```

- 部署：**退出 v2rayN** 后把 `publish\v2rayN-normal\*` 覆盖到运行目录（保留原 `bin\` 内核与 profile db），覆盖前先备份整个运行目录。
- 产出目录核对：`E:\v2rayN-source\publish\v2rayN-normal\v2rayN.exe`。

## E. 相关外部项目（非本 fork 代码）

- 代理网关（独立）：`E:\proxy_crawler\`（proxy_gateway.py 壳 + proxy_sources 插件），端点 /clash、/txt、/v2rayn。
- 雪球采集（独立）：`E:\PyCharmProjects\my-agent-raspi\xueqiu_crawler\`。
