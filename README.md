# StellarTool

用于舞萌 DX 的 OBS 浏览器叠加层与本地遥测工具。由 .NET 8 Host、MelonLoader 游戏适配器和原生 Web 前端组成。适配器读取输入、曲目与判定等游戏状态，不写入成绩或账号，不向控制器发送指令。

Independent community project. Not affiliated with or endorsed by SEGA.

## 启动 Host

安装 .NET 8 SDK，从仓库根目录执行：

```sh
dotnet run --project src/StellarTool.Host
```

首次启动会生成 `config.json` 和 `bridge-token.txt`。管理界面在 [7856](http://127.0.0.1:7856/)，显示端在 7857。管理写入和 Mod 数据接入只接受本机请求；显示接口不要求登录，会暴露当前输入、曲目、谱面和判定。默认绑定回环地址。OBS 在另一台机器上时，仅对可信局域网开放显示端，不要把端口转发到互联网，也不要公开 bridge token。

把对应 URL 填入 OBS 浏览器源：

| 叠加层 | 示例 | 浏览器源大小 |
| --- | --- | --- |
| 节奏解析 | `/rhythm?mode=taiko&width=800&height=100` | 800 × 250，顶部 150 px 留给上飞特效 |
| 乐谱模式 | `/rhythm?mode=score&width=800&height=100` | 800 × 100 |
| 曲目信息与节拍器 | `/corner?bpm=true&time=true&pos=true&metronome=bar` | 随场景设置 |
| 判定快慢 | `/judgement?preview=true` | 默认 800 × 90；正式使用去掉预览参数 |
| 触摸感应器 | `/sensor?size=720&light=51E9FFF5&outline=false&player=1` | 720 × 720 |
| 外键与灯光 | `/keys?size=720&lightOpacity=1&frameOpacity=1&player=1` | 720 × 720 |

`size` 是边长。颜色用 `RRGGBB` 或叠加层支持的 `RRGGBBAA`，不带 `#`。外键可在兼容手台上显示实时 RGB，按压单独压暗。若只想用固定颜色显示按键，设置 `ledMode=legacy&light=EC2127`。

## 构建与接入游戏

浏览器叠加层与 Host 无需游戏文件即可构建。完整游戏接入需要你自己的兼容 Windows 游戏安装、MelonLoader、Harmony 和对应 Mono/.NET Framework 引用。以下命令使用 POSIX shell（Windows 可用 Git Bash）：

```sh
sh build.sh --host-only
GAME_MANAGED=/path/to/Sinmai_Data/Managed MELON_DIR=/path/to/loader/files sh build.sh
```

`MELON_DIR` 应包含匹配的 `MelonLoader.dll` 与 `0Harmony.dll`。只将 `dist/mod/StellarTool.Mod.dll` 放入游戏的 `Mods/`，不要复制 Host 或第三方 DLL。将游戏同机的 Host 启动后，在游戏目录创建 `UserData/StellarTool/bridge.cfg`：

```ini
endpoint=http://127.0.0.1:7856/api/ingest
token=REPLACE_WITH_LOCAL_BRIDGE_TOKEN
player=0
```

这里 Mod 的 `player=0` 指 1P，网页的 `player=1` 指 1P。适配器会按 `Assembly-CSharp.dll` 的哈希校验受支持的游戏版本。不匹配时会停用，而不是盲目挂载。SDEZ 的指定版本已验证启动与遥测连接；SDGB 有适配代码，尚未验证实机。按键灯读取依赖兼容的 AquaMai/Maimoller 路径，并非所有手台都支持。

## 公开版本包含什么

本仓库只发布项目自身源码。没有游戏、固件、第三方二进制、封面、字体、游戏贴图、基于官方 CAD 的按键图或私有部署配置。未提供素材时，叠加层使用文字和原创示意图；触摸区示意图不等同于官机的精确几何。你可以在本地配置有权使用的可选图像，不要将这些资源提交到仓库。

## 测试

```sh
node --test tests/*.test.mjs
for suite in StellarTool NativeButtonLeds Settlement Judgement; do
  dotnet run --project "tests/${suite}.Tests" -c Release
done
MELON_DIR=/path/to/loader/files dotnet run --project tests/CalibratedHits.Tests -c Release
```

最后一项需要本机 Harmony。可另设 `STELLAR_TEST_GAME_ASSEMBLY` 测试目标游戏程序集的元数据，无需运行游戏。CI 运行不依赖游戏或第三方资源的构建和测试。

## License

[MIT](LICENSE) covers this project's source and documentation only. Third-party games, names, dependencies, and locally supplied artwork are not included in that grant.
