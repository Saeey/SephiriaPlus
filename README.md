# SephiriaPlus

作者：null
当前版本：2.0.0（BepInEx 版）

SephiriaPlus 是《赛菲莉娅（Sephiria）》的综合便利性 Mod。2.0.0 已从游戏 AddOn 迁移到 BepInEx 5，并使用 HarmonyX 对游戏逻辑进行补丁。

## 功能

- 无限刷新：刷新次数自动补充至 99。
- 神器流派筛选：刷新界面可按流派限定候选神器。
- 天赋点数 ×10。
- 背包格子 +18。
- 失败重试：失败结算界面增加“重试”按钮，也可按 F8。
- 隐藏房间提示：在小地图上显示隐藏房间标记。
- DPS 统计：按 F7 显示或隐藏当前房间伤害面板。

DPS 统计基于游戏实际伤害反馈事件记录伤害，不再通过生命值轮询估算；进入新战斗房间后会开始新的统计周期。面板为本机显示，不会同步给联机队友。

## 安装

1. 从 BepInEx 官方 Releases 下载 `BepInEx_win_x64_5.4.23.5.zip`。
2. 将 BepInEx 压缩包内容解压到游戏根目录，即 `Sephiria.exe` 所在目录。
3. 启动一次游戏，让 BepInEx 创建目录，然后退出游戏。
4. 将本项目发布包中的 `BepInEx` 文件夹合并到游戏根目录。
5. 最终应存在：
   - `BepInEx/plugins/SephiriaPlus/SephiriaPlus.dll`
   - `BepInEx/plugins/SephiriaPlus/config.json`

从 1.x 升级时，请删除旧的 `AddOns/SephiriaPlus`，不要同时加载 AddOn 版和 BepInEx 版。

详细步骤见 [安装手册](./安装手册.md)。

## 配置

编辑 `BepInEx/plugins/SephiriaPlus/config.json`，保存后重启游戏：

```json
{
  "EnableInfiniteReroll": true,
  "RerollDiceTarget": 99,
  "EnableTalentPointMultiplier": true,
  "TalentPointMultiplier": 10,
  "EnableExtraInventorySlots": true,
  "ExtraInventorySlots": 18,
  "EnableCheckpointRetry": true,
  "CheckpointRetryKey": "F8",
  "EnableArtifactSchoolFilter": true,
  "EnableHiddenRoomReveal": true,
  "EnableDpsMeter": true,
  "DpsMeterToggleKey": "F7"
}
```

## 构建

需要 .NET SDK、游戏本体以及 BepInEx 5：

```powershell
dotnet build .\SephiriaPlus\SephiriaPlus.csproj -c Release `
  -p:SephiriaPath='D:\game\Steam\steamapps\common\Sephiria' `
  -p:BepInExPath='D:\game\Steam\steamapps\common\Sephiria\BepInEx\core'
```

## 说明

- 本 Mod 为非官方作品，游戏更新后可能需要适配。
- 联机时建议所有玩家使用相同版本与配置。由房主控制的游戏逻辑通常只需房主安装；本地 UI（例如 DPS 面板）只会显示在安装者客户端。
- DPS 功能研究过 Sephiria DPS Meter 的公开实现思路，但本项目为独立实现，没有复制其代码或资源。
