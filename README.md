# SephiriaPlus

作者：null
当前版本：2.2.4（BepInEx 版）

SephiriaPlus 是《赛菲莉娅（Sephiria）》的综合便利性 Mod。2.x 已从游戏 AddOn 迁移到 BepInEx 5，并使用 HarmonyX 对游戏逻辑进行补丁。

## 功能

- 无限刷新：刷新次数自动补充至 99。
- 神器流派筛选：正在重新适配正确的神器刷新界面；不会显示在奇迹三选一界面。
- 天赋点数 ×10，并在开始游戏时自动恢复已保存的天赋分配。
- 背包格子 +30。
- 失败重试：失败结算界面增加“重试”按钮，也可按 F8。
- 隐藏房间提示：在场景中显示放大 5 倍的隐藏房间入口标记。
- 许愿池容量 +100：同步扩展许愿泉选择上限与开局神器发放上限。

## 安装

1. 下载 `SephiriaPlus-v2.2.4.zip`。
2. 将压缩包内的全部内容直接解压到游戏根目录，即 `Sephiria.exe` 所在目录。
3. 启动游戏；发布包已内置 Windows x64 版 BepInEx 5.4.23.5，无需另外下载。
4. 确认游戏根目录存在 `winhttp.dll`、`doorstop_config.ini` 和 `BepInEx` 文件夹。
5. 插件文件位于：
   - `BepInEx/plugins/SephiriaPlus/SephiriaPlus.dll`
   - `BepInEx/plugins/SephiriaPlus/config.json`

从 1.x 升级时，请删除旧的 `AddOns/SephiriaPlus`，不要同时加载 AddOn 版和 BepInEx 版。

详细步骤见 [安装手册](./安装手册.md)。

从 v1.4.0 至今的版本变化见 [更新日志](./更新日志.md)。

## 配置

编辑 `BepInEx/plugins/SephiriaPlus/config.json`，保存后重启游戏：

配置文件内含中文说明。所有 `Enable...` 项均可独立设置，`true` 为开启，`false` 为关闭。

```json
{
  "EnableInfiniteReroll": true,
  "RerollDiceTarget": 99,
  "EnableTalentPointMultiplier": true,
  "TalentPointMultiplier": 10,
  "EnableExtraInventorySlots": true,
  "ExtraInventorySlots": 30,
  "EnableCheckpointRetry": true,
  "CheckpointRetryKey": "F8",
  "EnableArtifactSchoolFilter": true,
  "EnableHiddenRoomReveal": true,
  "HiddenRoomMarkerScale": 5.0,
  "EnableExtraWishPoolCapacity": true,
  "ExtraWishPoolCapacity": 100
}
```

## v1.4.0 与 v2.2.4 对比

| 项目 | v1.4.0 | v2.2.4 |
| --- | --- | --- |
| 模组架构 | 游戏原生 AddOn | BepInEx 5 + HarmonyX |
| 安装方式 | 复制到 `AddOns` | 内置 BepInEx 的一体化安装包 |
| 无限刷新 | 自动补充至 99 | 保留，可独立开关和调整目标值 |
| 天赋点数 | 命运刻印额外点数 ×10 | 保留，并修复开局需要重新加点的问题 |
| 背包扩容 | +18 格 | +30 格，并修复天赋更新后失效的问题 |
| 失败重试 | 结算界面重试按钮 | 重试按钮 + F8 快捷键 |
| 隐藏房间 | 无 | 入口提示，默认放大 5 倍且优化扫描性能 |
| 许愿池 | 无 | 容量 +100，并同步扩展开局发放上限 |
| 配置 | 基础开关 | 每项独立开关并附中文说明 |

从 1.x 升级前必须删除旧的 `AddOns/SephiriaPlus`，避免 AddOn 版与 BepInEx 版同时加载。

## 构建

需要 .NET SDK、游戏本体以及 BepInEx 5：

```powershell
dotnet build .\SephiriaPlus\SephiriaPlus.csproj -c Release `
  -p:SephiriaPath='D:\game\Steam\steamapps\common\Sephiria' `
  -p:BepInExPath='D:\game\Steam\steamapps\common\Sephiria\BepInEx\core'
```

## 说明

- 本 Mod 为非官方作品，游戏更新后可能需要适配。
- 联机时建议所有玩家使用相同版本与配置。由房主控制的游戏逻辑通常只需房主安装。
- 神器筛选不会注入奇迹三选一界面，也不会修改房主全局神器池。
- DPS 统计已从 SephiriaPlus 移除；需要伤害统计时可单独安装 Sephiria DPS Meter。
