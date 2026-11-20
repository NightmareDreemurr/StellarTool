# 本地素材配置

未安装游戏也能启动 Host 和查看 [判定演示](http://127.0.0.1:7857/judgement?preview=true)。实时游戏输入、曲目和判定仍需兼容的 Windows 游戏安装及适配器。游戏文件和可选画面素材都不随公开版分发，即使不添加素材，Host 仍能运行并使用内置文字及原创示意图。

## 可选图像

仅使用你有权使用的素材。以下路径相对于 Host 的运行目录（默认是当前目录；设置 `STELLAR_TOOL_HOME` 时用该目录）。使用编译产物时，运行目录一般是 `dist/host`。

| 用途 | 位置 | 无素材时 |
| --- | --- | --- |
| 曲目封面 | 用环境变量 `STELLAR_ASSETS_ZIP` 指向本地 ZIP，或在运行目录创建单行 `resource-path.txt`。ZIP 内路径格式为 `assets/jackets/UI_Jacket_000001.png`，编号按曲目 ID 补足 6 位。 | 不显示封面 |
| DX/SD、Rank 等图标 | `web/assets/`，例如 `Deluxe.png`、`UI_GAM_Rank_SSSp.png` | 文字标识 |
| 曲目、分类、成绩状态素材 | `web/assets/songinfo/`，可在 `web/songinfo.mjs` 查具体文件名 | 文字标识 |
| 判定图 | `web/assets/judgements/`，文件名见 `web/judge-sprites.mjs` | 文字判定 |
| 触摸区几何 | `web/assets/sensor-layout.json`，需符合 `web/input-layout.mjs` 的数据结构 | 原创示意图，不代表官机几何 |
| 外键图层 | `web/assets/cad-keys-v1/`，需准备底图和所有按键遮罩，见 `web/input-overlay.mjs` | 原创示意图 |
| 可选字体 | `web/assets/`，文件名见 `web/style.css`；遵守各字体许可 | 系统字体 |

例如在 Host 的运行目录：

```sh
STELLAR_ASSETS_ZIP="/path/to/my-local-artwork.zip" dotnet StellarTool.Host.dll
```

`web/` 由 Host 提供给浏览器。不要把账号数据、Token、原始游戏包、完整素材 ZIP 或服务端配置放进去。默认只在本机监听；需要 OBS 跨机使用时，仅向可信局域网开放。上面这些路径用于本地自备，不是再分发许可。要分享 Host 构建，请在干净的检出目录中重新构建，不要将已配置过的运行目录直接打包。发布自己的修改时，不要提交 `web/assets/`、`resource-path.txt`、`config.json`、`bridge-token.txt` 或游戏目录。

## 游戏接入依赖

请按 [README](../README.md) 将 `GAME_MANAGED` 指向已安装游戏对应版本的本地 Managed 目录，将 `MELON_DIR` 指向已安装并匹配的 MelonLoader / Harmony。不要从本仓库寻找或分发游戏、固件、私有签名文件或本机桥接配置。只有 `StellarTool.Mod.dll` 放进游戏的 `Mods/`。
