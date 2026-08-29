from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageFilter
import json

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "video" / "slides"
OUT.mkdir(parents=True, exist_ok=True)

W, H = 1920, 1080
FONT = r"C:\Windows\Fonts\NotoSansSC-VF.ttf"
FONT_B = r"C:\Windows\Fonts\NotoSansSC-VF.ttf"
SCREENSHOT = Path(r"C:\Users\null\AppData\Local\Temp\codex-clipboard-fb35f254-7062-4167-a8d5-03c39224375f.png")
HIDDEN_SCREENSHOT = Path(r"C:\Users\null\AppData\Local\Temp\codex-clipboard-02640e43-7b77-43bf-ad5c-4e64c7724968.png")
RETRY_SCREENSHOT = Path(r"C:\Users\null\AppData\Local\Temp\codex-clipboard-9b12f728-155c-477b-a747-2ca376c1ebad.png")

slides = [
    {
        "kicker": "SEPHIRIA · BEPINEX MOD",
        "title": "SephiriaPlus\n综合便利 MOD",
        "subtitle": "v2.2.4 · 作者 null · 六项实用增强",
        "voice": "这是由 null 制作的赛菲莉娅综合便利模组 Sephiria Plus，当前版本二点二点四。下面介绍模组效果、联机规则和安装方法。",
        "kind": "title",
    },
    {
        "kicker": "核心增强 · 一",
        "title": "刷取与养成更轻松",
        "bullets": ["刷新骰子自动补至 99", "命运刻印天赋点 ×10", "主背包增加 30 格"],
        "voice": "第一组功能包括无限刷新、天赋点十倍和背包扩容。刷新骰子会自动补到九十九，命运刻印提供的额外天赋点乘以十，主背包额外增加三十格。",
        "kind": "features",
    },
    {
        "kicker": "核心增强 · 二",
        "title": "开局与探索更方便",
        "bullets": ["许愿池容量增加 100", "失败界面无限重试", "隐藏房间提示放大 5 倍"],
        "voice": "第二组功能包括许愿池容量增加一百，失败界面无限重试，以及放大五倍的隐藏房间入口提示。许愿池的选择与开局发放上限会同步扩展。",
        "kind": "features",
    },
    {
        "kicker": "效果二",
        "title": "命运刻印天赋点 ×10",
        "subtitle": "基础点数不变 · 已保存的天赋开局自动恢复",
        "badge": "5 + 10  →  5 + 100",
        "detail": "刻印额外点数 ×10",
        "voice": "天赋倍率只作用于命运刻印提供的额外点数，基础点数保持原版数值。二点二点四还修复了开局需要重新加点的问题，保存的天赋会在倍率生效后自动载入。",
        "kind": "screenshot",
    },
    {
        "kicker": "容量增强",
        "title": "背包 +30 · 许愿池 +100",
        "subtitle": "背包从 24 格扩展至 54 格，许愿池同步提高选择与发放上限",
        "badge": "6 × 4  →  6 × 9",
        "voice": "容量方面，主背包从默认二十四格增加到五十四格，共增加三十格。许愿池额外增加一百容量，并同步提高开局神器发放上限。",
        "kind": "grid",
    },
    {
        "kicker": "失败重试",
        "title": "结算界面一键快速重开",
        "subtitle": "点击“重试”或按 F8，直接恢复当前关卡的开场检查点",
        "badge": "重试当前关卡",
        "detail": "保留进入本关时的状态",
        "image": str(RETRY_SCREENSHOT),
        "voice": "挑战失败后，不需要返回基地重新跑图。结算界面会在命运刻印和返回之间增加重试按钮，点击按钮或按下 F 八，就能恢复进入当前关卡时保存的检查点。",
        "kind": "screenshot",
    },
    {
        "kicker": "探索提示",
        "title": "隐藏房间入口更醒目",
        "subtitle": "默认放大 5 倍，可在配置中调整为 1 到 10 倍",
        "badge": "▼ 隐藏房间  ×5",
        "detail": "入口标记可调 1～10 倍",
        "image": str(HIDDEN_SCREENSHOT),
        "voice": "隐藏房间提示经过性能优化，只在楼层变化或间隔时间到达时扫描。入口文字默认放大五倍，配置较低的电脑也不会每帧重复扫描整个场景。",
        "kind": "screenshot",
    },
    {
        "kicker": "联机规则",
        "title": "主要效果由房主同步",
        "bullets": ["房主安装并创建房间", "刷新、天赋、背包等同步给队友", "隐藏房间文字只显示在房主本机"],
        "voice": "联机时由房主安装并创建房间。无限刷新、天赋、背包和开局神器等服务器逻辑会同步给队友；隐藏房间文字属于本机画面提示，只显示在安装模组的房主画面上。",
        "kind": "host",
    },
    {
        "kicker": "安装 · 第一步",
        "title": "安装 BepInEx 5",
        "subtitle": "将 BepInEx_win_x64_5.4.23.5 解压到 Sephiria.exe 所在目录",
        "badge": "首次安装后启动一次游戏，再退出",
        "voice": "安装前先准备 BepInEx 五。把六十四位的五点四点二十三点五版本解压到 Sephiria 点 exe 所在目录。首次安装后启动一次游戏，再退出，让框架创建所需文件夹。",
        "kind": "install",
    },
    {
        "kicker": "安装 · 第二步",
        "title": "合并压缩包里的 BepInEx 文件夹",
        "subtitle": "最终确认插件 DLL 与 config.json 位于同一目录",
        "tree": ["Sephiria", "└─ BepInEx", "   └─ plugins", "      └─ SephiriaPlus", "         ├─ SephiriaPlus.dll", "         └─ config.json"],
        "voice": "然后把发布包里的 BepInEx 文件夹合并到游戏根目录。最终应当能在 BepInEx、plugins、Sephiria Plus 目录中看到 DLL 和 config 点 json。旧版 AddOns 文件夹中的 Sephiria Plus 必须删除。",
        "kind": "tree",
    },
    {
        "kicker": "配置与开关",
        "title": "所有功能均可单独调整",
        "subtitle": "修改 config.json 后需要重启游戏",
        "tree": ["RerollDiceTarget: 99", "TalentPointMultiplier: 10", "ExtraInventorySlots: 30", "ExtraWishPoolCapacity: 100", "HiddenRoomMarkerScale: 5.0"],
        "voice": "所有主要功能都可以在 config 点 json 中单独开关或修改数值。默认刷新次数九十九，天赋十倍，背包加三十，许愿池加一百，隐藏房间标记五倍。修改后需要重启游戏。",
        "kind": "tree",
    },
    {
        "kicker": "完成",
        "title": "启动游戏，开始体验",
        "bullets": ["日志显示 Loading SephiriaPlus 2.2.4", "更新或卸载前必须关闭游戏", "详细变化请查看更新日志与安装手册"],
        "voice": "重新启动游戏，日志中显示正在载入 Sephiria Plus 二点二点四就代表安装成功。更新或卸载前必须关闭游戏，并建议提前备份存档。完整版本变化请查看发布包中的更新日志和安装手册。",
        "kind": "end",
    },
]

def font(size, bold=False):
    return ImageFont.truetype(FONT_B if bold else FONT, size)

def rounded(draw, xy, radius, fill, outline=None, width=1):
    draw.rounded_rectangle(xy, radius=radius, fill=fill, outline=outline, width=width)

def fit_text(draw, text, max_width, start_size, min_size=28):
    for size in range(start_size, min_size - 1, -2):
        f = font(size, True)
        if draw.textbbox((0, 0), text, font=f)[2] <= max_width:
            return f
    return font(min_size, True)

def background():
    img = Image.new("RGB", (W, H), "#080b12")
    d = ImageDraw.Draw(img)
    for y in range(H):
        t = y / H
        c = (8 + int(8*t), 11 + int(10*t), 18 + int(18*t))
        d.line((0, y, W, y), fill=c)
    for x, y, r, color in [(1550, 160, 430, "#3d164d"), (250, 920, 520, "#143e54")]:
        glow = Image.new("RGBA", (W, H), (0,0,0,0))
        gd = ImageDraw.Draw(glow)
        gd.ellipse((x-r, y-r, x+r, y+r), fill=color + "88")
        glow = glow.filter(ImageFilter.GaussianBlur(120))
        img = Image.alpha_composite(img.convert("RGBA"), glow).convert("RGB")
    d = ImageDraw.Draw(img)
    for x in range(0, W, 48):
        d.line((x, 0, x, H), fill="#ffffff08", width=1)
    for y in range(0, H, 48):
        d.line((0, y, W, y), fill="#ffffff08", width=1)
    return img

def base_header(img, slide, idx):
    d = ImageDraw.Draw(img)
    d.text((110, 74), slide["kicker"], font=font(26, True), fill="#62d9ff")
    d.text((1740, 74), f"{idx+1:02d} / {len(slides):02d}", font=font(23), fill="#79869b")
    d.line((110, 125, 1810, 125), fill="#263448", width=2)
    return d

def draw_slide(slide, idx):
    img = background()
    d = base_header(img, slide, idx)
    kind = slide["kind"]
    if kind == "title":
        d.text((120, 250), slide["title"], font=font(104, True), fill="white", spacing=18)
        d.text((125, 530), slide["subtitle"], font=font(40), fill="#c2ccdc")
        rounded(d, (123, 650, 1100, 750), 22, "#151b2a", "#30425c", 2)
        d.text((165, 675), "BepInEx 5 · 房主增强 · v2.2.4", font=font(35, True), fill="#ffcf67")
        d.text((125, 920), "非官方玩家作品 · 使用前请备份存档", font=font(25), fill="#77869a")
    elif kind == "features":
        d.text((110, 190), slide["title"], font=fit_text(d, slide["title"], 1500, 68), fill="white")
        colors = ["#3ccdf2", "#f070d8", "#ffbf5d"]
        for i, text in enumerate(slide["bullets"]):
            x = 115 + i * 565
            rounded(d, (x, 355, x+510, 770), 28, "#141b2a", colors[i], 3)
            d.text((x+42, 397), f"0{i+1}", font=font(34, True), fill=colors[i])
            lines = text.replace("自动", "自动\n").replace("天赋点", "天赋点\n").replace("增加", "增加\n")
            d.text((x+42, 515), lines, font=font(48, True), fill="white", spacing=14)
    elif kind in ("effect", "install"):
        d.text((110, 205), slide["title"], font=fit_text(d, slide["title"], 1600, 72), fill="white")
        d.text((112, 315), slide["subtitle"], font=font(32), fill="#adb9ca")
        rounded(d, (112, 455, 1808, 760), 36, "#141b2a", "#334660", 2)
        badge_font = fit_text(d, slide["badge"], 1500, 86)
        box = d.textbbox((0,0), slide["badge"], font=badge_font)
        d.text(((W-(box[2]-box[0]))/2, 555), slide["badge"], font=badge_font, fill="#65dcff")
    elif kind == "screenshot":
        d.text((110, 180), slide["title"], font=font(68, True), fill="white")
        d.text((112, 280), slide["subtitle"], font=font(30), fill="#adb9ca")
        shot = Image.open(Path(slide.get("image", str(SCREENSHOT)))).convert("RGB")
        shot.thumbnail((1040, 585))
        shot = shot.crop((0, 0, shot.width, shot.height))
        frame = Image.new("RGB", (shot.width+12, shot.height+12), "#ff71d7")
        frame.paste(shot, (6,6))
        img.paste(frame, (90, 400))
        rounded(d, (1235, 455, 1795, 715), 28, "#191627", "#ff71d7", 3)
        d.text((1300, 535), slide["badge"], font=font(48, True), fill="#ff9be5")
        d.text((1300, 635), slide.get("detail", ""), font=font(29), fill="white")
    elif kind == "grid":
        d.text((110, 185), slide["title"], font=font(70, True), fill="white")
        d.text((112, 285), slide["subtitle"], font=font(31), fill="#adb9ca")
        ox, oy, cell, gap = 210, 405, 58, 8
        for row in range(9):
            for col in range(6):
                active = row < 4
                fill = "#243145" if active else "#4b2549"
                outline = "#5edbff" if active else "#ff77db"
                x, y = ox+col*(cell+gap), oy+row*(cell+gap)
                rounded(d, (x,y,x+cell,y+cell), 8, fill, outline, 2)
        d.text((760, 500), slide["badge"], font=font(68, True), fill="#ff84df")
        d.text((765, 625), "+30 格 = +5 行", font=font(37), fill="white")
    elif kind in ("host", "end"):
        d.text((110, 190), slide["title"], font=font(70, True), fill="white")
        for i, text in enumerate(slide["bullets"]):
            y = 360 + i * 165
            rounded(d, (115, y, 1805, y+120), 24, "#141b2a", "#32445d", 2)
            rounded(d, (145, y+26, 215, y+96), 16, "#2b7895")
            d.text((168, y+35), str(i+1), font=font(28, True), fill="white")
            d.text((255, y+34), text, font=font(38, True), fill="white")
    elif kind == "tree":
        d.text((110, 180), slide["title"], font=font(68, True), fill="white")
        d.text((112, 280), slide["subtitle"], font=font(29), fill="#adb9ca")
        rounded(d, (280, 380, 1640, 880), 30, "#101724", "#40536d", 2)
        tree_spacing = 70 if len(slide["tree"]) > 5 else 80
        for i, line in enumerate(slide["tree"]):
            color = "#67dcff" if i < 2 else ("#ffce6b" if i == 2 else "white")
            d.text((390, 420+i*tree_spacing), line, font=font(38, True), fill=color)
    return img

for idx, slide in enumerate(slides):
    draw_slide(slide, idx).save(OUT / f"slide_{idx+1:02d}.png", quality=95)

(ROOT / "video" / "script.json").write_text(json.dumps(slides, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"Created {len(slides)} slides in {OUT}")
