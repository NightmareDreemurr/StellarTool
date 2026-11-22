# StellarTool

用于舞萌 DX 的 OBS 浏览器叠加层与本地遥测工具。Windows 游戏侧由 MelonLoader Mod 采集输入、曲目、谱面和判定等状态，.NET 8 Host 将数据提供给 OBS 浏览器源。Mod 不修改成绩或账号数据，也不向控制器发送指令。

Independent community project. Not affiliated with or endorsed by SEGA.

## 下载与启动

从 [GitHub Releases](https://github.com/NightmareDreemurr/StellarTool/releases/latest) 下载所需文件。

| 文件 | 用途 |
| --- | --- |
| `StellarTool.Web-win-x64.zip` | Windows x64 一键运行包，含 Host、ASP.NET Core 8 运行时、网页素材、曲目封面，以及 `mod/StellarTool.Mod.dll`。 |
| `StellarTool.Mod.dll` | 同版游戏端适配器的单独下载，适合只更新 Mod。 |

1. 将 Web ZIP 完整解压到有写权限的目录，双击 `Start-StellarTool.cmd`，保持窗口开启。
2. 打开 [本机管理页](http://127.0.0.1:7856/)。首次启动会在解压目录生成 `config.json` 和 `bridge-token.txt`。
3. 可以先打开 [判定演示](http://127.0.0.1:7857/judgement?preview=true)。只预览网页时无需安装 Mod 或配置游戏。

macOS 或 Linux 可以使用下文的源码构建方式启动 Host。

## 游戏与加载器版本

| 游戏版本 | 加载器与验证范围 |
| --- | --- |
| **SDEZ 1.70** | 构建使用 MelonLoader **0.6.4 Open-Beta** 的 Mono/net35 版和对应 Harmony。该游戏程序集已有 Mod 加载、连接及空闲输入遥测记录。 |
| **SDEZ 1.71** | 对应覆盖包的主程序集已通过 Mod 加载、连接及空闲输入遥测验证。实机游玩仍有待验证。 |
| **SDGB**（具体发行版本号未确认） | 适配其反射接口，尚无实机运行验证。 |

Mod 启动时检查已加载的游戏程序集是否提供计时、谱面读取和核心流程接口。以下 SHA-256 用于识别已对照的程序集。

| 游戏 | SHA-256 |
| --- | --- |
| SDEZ 1.70 | `dc0e46a4133d4ee4781ba6deb2fe30063dd3f82ee91c31a5a8a7e61e644d9446` |
| SDEZ 1.71 | `cccf157459c6d81c8cd4ee0661338d0d554d502be2866e34ed39643b7aff2356` |
| SDGB | `c291f0febe70cc24a679683ef07c059c1f856c052e1f6be7a22b55c6b16a94eb` |

在 PowerShell 中从游戏根目录核对：

```powershell
(Get-FileHash ".\Sinmai_Data\Managed\Assembly-CSharp.dll" -Algorithm SHA256).Hash.ToLowerInvariant()
```

外键实时 RGB 还依赖兼容的 AquaMai/Maimoller 和手台链路。

## 安装与配置 Mod

1. 在游戏所在的 Windows 电脑启动 Host。在游戏根目录的 `Mods/` 放入 `StellarTool.Mod.dll`。
2. 在游戏根目录创建 `UserData/StellarTool/bridge.cfg`，将 `token` 替换为 Host 生成的 `bridge-token.txt` 中的整行内容。

```ini
endpoint=http://127.0.0.1:7856/api/ingest
token=REPLACE_WITH_LOCAL_BRIDGE_TOKEN
player=0
```

3. 启动游戏。日志里的 `Missing ... bridge.cfg` 表示找不到桥接配置，`Incompatible game API` 会列出缺失的游戏接口。连接后可在 [状态接口](http://127.0.0.1:7857/api/state) 查看 `connected`。

Mod 的 `player=0` 和 `player=1` 分别选择 1P、2P。触摸和外键叠加层 URL 的 `player=1` 和 `player=2` 分别显示 1P、2P。管理端口改变后，更新 `endpoint` 并重启游戏。

## 配置 OBS 和网络

在 OBS 添加浏览器源。以下路径接在显示端地址 `http://127.0.0.1:7857` 后。

| 叠加层 | 路径示例 | 浏览器源大小 |
| --- | --- | --- |
| 节奏解析 | `/rhythm?mode=taiko&width=800&height=100` | 800 × 250，上方 150 px 留给上飞特效 |
| 乐谱模式 | `/rhythm?mode=score&width=800&height=100` | 800 × 100 |
| 曲目信息与节拍器 | `/corner?bpm=true&time=true&pos=true&metronome=bar` | 随场景设置 |
| 判定快慢 | `/judgement` | 默认 800 × 90 |
| 触摸感应器 | `/sensor?size=720&light=51E9FFF5&outline=false&player=1` | 720 × 720 |
| 外键与灯光 | `/keys?size=720&lightOpacity=1&frameOpacity=1&player=1` | 720 × 720 |

`size` 是正方形边长。颜色用 `RRGGBB` 或对应选项支持的 `RRGGBBAA`，无需 `#`。外键可用 `ledMode=legacy&light=EC2127` 固定灯色。

管理端口默认是 `7856`，显示端口默认是 `7857`，监听地址默认是 `127.0.0.1`。在管理界面的 `server` 配置中修改地址或端口后需重启 Host。OBS 在另一台电脑时，可将 `server.host` 改为 `0.0.0.0`，在防火墙中只向可信局域网放行显示端口，再用 Host 的局域网 IP 拼接 OBS 地址。Mod 的 `endpoint` 仍指本机回环地址。显示端公开当前输入、曲目、谱面和判定状态，勿转发到互联网或公开桥接 token。

## 随附文件与本地配置

| 已随仓库提供 | 需要本地准备 |
| --- | --- |
| `dependencies/sdez170-melon064/` 的 MelonLoader、Harmony、Mono 框架和游戏 firstpass 编译参考 | 实际运行的 Windows 游戏、匹配的 MelonLoader 安装及其运行时文件 |
| `web/assets/` 的 SD/DX、Rank、判定、曲目信息图标和图片，触摸几何、外键图层、MiSans 和 minimoon 字体 | 源码运行时可选的曲目封面 ZIP。Windows 发布包已随附 `jackets.zip` |
| Host 源码和 OBS 网页 | 本机生成的 `config.json`、`bridge-token.txt`，以及按上文创建的 `bridge.cfg` |

Windows Web ZIP 随附网页素材和曲目封面。封面路径、目录结构及素材替换方法见 [本地素材配置](docs/local-assets.md)。

## 从源码构建

安装 [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)，在仓库根目录执行。Windows 可用 Git Bash。

```sh
sh build.sh               # 构建 Host 和 Mod
sh build.sh --host-only   # 只构建 Host
cd dist/host
dotnet StellarTool.Host.dll
```

Host 和网页素材输出到 `dist/host/`，Mod 输出到 `dist/mod/StellarTool.Mod.dll`。完整构建默认用仓库内的参考文件，可用 `GAME_MANAGED` 指定本机的 `Sinmai_Data/Managed`，用 `MELON_DIR` 指定包含匹配 `MelonLoader.dll` 和 `0Harmony.dll` 的文件夹。

要重新打包包含素材的 Windows x64 自包含版本，请安装 Python 3 并准备获授权的封面源 ZIP。在仓库根目录运行以下命令。

```sh
python3 scripts/package-windows.py /path/to/assets.zip dist/windows-release
```

脚本提取 Host 可读取的封面 PNG，并将启动器、Web、Mod、运行时许可证和第三方声明一起打包。

## 测试

```sh
node --test tests/*.test.mjs
for suite in StellarTool NativeButtonLeds Settlement Judgement; do
  dotnet run --project "tests/${suite}.Tests" -c Release
done
MELON_DIR="$PWD/dependencies/sdez170-melon064" \
  dotnet run --project tests/CalibratedHits.Tests -c Release
```

可另设 `STELLAR_TEST_GAME_ASSEMBLY` 指向本机 `Assembly-CSharp.dll`，以额外检查游戏程序集元数据。

## 许可证

项目源码与文档采用 [MIT](LICENSE)。第三方构建参考和素材各自适用其许可。Web 界面使用 MiSans 字体，随附 [MiSans 许可](web/assets/MiSans-LICENSE.txt)。
