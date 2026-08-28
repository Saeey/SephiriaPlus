# SephiriaPlus

《赛菲莉娅》的房主增强 Mod。房主进入一局游戏后，所有玩家的刷新骰子低于 99 时都会自动补到 99；命运刻印提供的天赋点变为原版的 10 倍；背包增加 18 格。

## 功能

- 奖励和铁匠等消耗刷新骰子的界面可以持续刷新。
- 神器选择界面提供“无 + 全部流派”筛选框；选择流派后再点刷新，候选神器只从该流派中生成。
- 未打破的隐藏房入口墙会显示闪烁的“隐藏房间”标记。
- 右上角显示同层玩家的当前地点伤害、DPS 和伤害占比，按 `F7` 可显示/隐藏。
- 所有玩家从命运刻印获得的天赋点调整为原版的 10 倍，基础点数保持原版数值。
- 所有玩家的主背包增加 18 格，即增加 3 整行。
- 每次进入新关卡自动保存入口检查点；失败结算时由房主点击“重试”按钮（或按 `F8`）恢复原装备、背包、等级和资源并无限重试。
- 使用游戏自带的 HorayModAPI，无需 MelonLoader 或 BepInEx。
- Mod 本身不直接改写游戏程序集；使用前建议备份存档，卸载前应清空额外背包区域并将天赋重置到原版上限。
- 联机只需房主安装，队友无需安装；房主会为房间内所有玩家补充骰子。
- 普通客户端单独安装时不会生效，也不会向房主发送修改请求。

## 构建

```powershell
dotnet build .\SephiriaPlus\SephiriaPlus.csproj -c Release `
  -p:SephiriaPath="D:\game\Steam\steamapps\common\Sephiria"
```

## 安装

完整图文式步骤与故障排查请阅读 `安装手册.md`。

将以下三个文件放入游戏目录：

```text
Sephiria\AddOns\SephiriaPlus\SephiriaPlus.dll
Sephiria\AddOns\SephiriaPlus\metadata.json
Sephiria\AddOns\SephiriaPlus\config.json
```

## 配置

关闭游戏后编辑 `Sephiria\AddOns\SephiriaPlus\config.json`：

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

七个 `Enable...` 项可以分别关闭无限刷新、天赋倍率、背包扩容、检查点重试、神器流派筛选、隐藏房标记和 DPS 面板；数字项分别控制目标骰子数、天赋倍率和额外格数。失败结算界面中由房主点击“命运刻印”和“返回”之间的“重试”按钮恢复本关入口状态，`CheckpointRetryKey` 是备用快捷键，`DpsMeterToggleKey` 用于显示或隐藏 DPS 面板。配置由房主启动时读取，修改后需要重启游戏。

启动游戏并进入城镇或地牢后，可以在以下日志中确认加载：

```text
%USERPROFILE%\AppData\LocalLow\TEAMHORAY\Sephiria\Player.log
```

日志应包含：

```text
[AddOnLoader] 'SephiriaPlus' v1.6.0
[SephiriaPlus] loaded with config: reroll=True (target 99), talent=True (x10), inventory=True (+18), checkpointRetry=True (F8), artifactFilter=True, hiddenRooms=True, dpsMeter=True (F7)
```

## 卸载

关闭游戏后删除 `Sephiria\AddOns\SephiriaPlus` 文件夹。
