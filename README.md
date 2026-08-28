# SephiriaPlus

《赛菲莉娅》的房主增强 Mod。房主进入一局游戏后，所有玩家的刷新骰子低于 99 时都会自动补到 99；命运刻印提供的天赋点变为原版的 10 倍；背包增加 18 格。

## 功能

- 奖励和铁匠等消耗刷新骰子的界面可以持续刷新。
- 所有玩家从命运刻印获得的天赋点调整为原版的 10 倍，基础点数保持原版数值。
- 所有玩家的主背包增加 18 格，即增加 3 整行。
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
  "ExtraInventorySlots": 18
}
```

三个 `Enable...` 项可以分别关闭无限刷新、天赋倍率和背包扩容；数字项分别控制目标骰子数、天赋倍率和额外格数。配置由房主启动时读取，修改后需要重启游戏。

启动游戏并进入城镇或地牢后，可以在以下日志中确认加载：

```text
%USERPROFILE%\AppData\LocalLow\TEAMHORAY\Sephiria\Player.log
```

日志应包含：

```text
[AddOnLoader] 'SephiriaPlus' v1.4.0
[SephiriaPlus] loaded with config: reroll=True (target 99), talent=True (x10), inventory=True (+18)
```

## 卸载

关闭游戏后删除 `Sephiria\AddOns\SephiriaPlus` 文件夹。
