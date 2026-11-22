# 随附素材与本地配置

Host 可独立启动。用 GitHub Release 的 Windows 一键包，或执行 `sh build.sh --host-only` 后从 `dist/host/` 启动，都会直接加载随仓库提供的网页素材。只看 [判定演示](http://127.0.0.1:7857/judgement?preview=true) 时无需游戏。

## 随附网页素材

| 用途 | 路径 | 缺少文件时 |
| --- | --- | --- |
| SD/DX、Rank、DX 星级 | `web/assets/` 下的 `Standard.png`、`Deluxe.png`、`UI_GAM_Rank_*.png`、`UI_GAM_DXScoreIcon_*.png` | 文字标识 |
| 曲目分类、等级和结算状态 | `web/assets/songinfo/`，文件映射见 `web/songinfo.mjs` | 文字标识 |
| 判定图 | `web/assets/judgements/`，文件映射见 `web/judge-sprites.mjs` | 文字判定 |
| 触摸区几何 | `web/assets/sensor-layout.json`、`sensor-layout.svg`，运行时加载 JSON | 内置触摸区示意图 |
| 外键图层和按压遮罩 | `web/assets/cad-keys-v1/`，文件映射见 `web/input-overlay.mjs` | 内置外键示意图 |
| 字体 | `web/assets/` 下的 MiSans 和 minimoon，字体映射见 `web/style.css` | 系统字体 |

素材路径相对于 Host 数据目录。Windows 一键包默认使用解压目录，手动执行 Host 时默认使用当前工作目录。若设置了 `STELLAR_TOOL_HOME`，请将 `web/` 连同 `assets/` 保留在该目录下，再放入需要的本地配置文件。

## 曲目封面

Windows 一键包的解压目录自带 `jackets.zip`，Host 会自动读取。ZIP 内的封面路径是 `assets/jackets/UI_Jacket_000001.png` 这类格式，数字为六位，文件名以游戏报告的曲目封面名为准。

源码运行时可将同样结构的 `jackets.zip` 放在 Host 数据目录。若要覆盖默认封面，可任选一种方式。

- 在 Host 数据目录创建 `resource-path.txt`，写入 ZIP 的绝对路径，整行无需引号，例如 `D:\StellarTool\assets.zip`。
- 在启动 Host 的同一终端设置环境变量 `STELLAR_ASSETS_ZIP`。此变量优先于 `resource-path.txt`。

例如 Windows PowerShell 中：

```powershell
$env:STELLAR_ASSETS_ZIP = 'D:\StellarTool\assets.zip'
.\Start-StellarTool.cmd
```

配置后可通过 `http://127.0.0.1:7857/assets/jackets/UI_Jacket_000001.png` 检查 ZIP 中实际存在的封面。无 ZIP 或缺少某首歌的封面时，叠加层仍可显示其他曲目信息。

## 游戏与本机文件

- 仓库的 `dependencies/sdez170-melon064/` 提供 `MelonLoader.dll`、`0Harmony.dll`、`mscorlib.dll`、`System.dll`、`System.Core.dll` 和 `Assembly-CSharp-firstpass.dll`，供源码构建使用。运行游戏时使用本机安装的兼容游戏及 Loader。安装 Mod 时只把 `StellarTool.Mod.dll` 放入游戏的 `Mods/`。
- Host 首次启动会创建 `config.json` 和 `bridge-token.txt`。按 [README](../README.md#安装与配置-mod) 在游戏本机填写 `UserData/StellarTool/bridge.cfg`。
- `web/` 会由 Host 提供给浏览器。请将封面 ZIP、本机配置、token、游戏目录和运行日志放在 `web/` 之外。
