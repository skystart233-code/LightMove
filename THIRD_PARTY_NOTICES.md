# 第三方组件与许可

轻移 LightMove 基于 Raw Accel v1.7.1，并再分发以下第三方组件。各组件的版权归原作者；LightMove 的修改按 MIT 许可发布。

| 组件 | 版本 / 来源 | 许可 | 完整文本 |
|---|---|---|---|
| Raw Accel | v1.7.1 · https://github.com/RawAccelOfficial/rawaccel | MIT · Copyright (c) 2020 a1xd | [LICENSE](LICENSE) |
| ShakeToFindCursor | commit `972287b11c35646645c7c31716c8e591f5f51eea` · https://github.com/0cch/ShakeToFindCursor | MIT · Copyright (c) 2024 0CCh | [LICENSE](third-party/ShakeToFindCursor/LICENSE) |
| Newtonsoft.Json | 13.0.1 · commit `ae9fe44e1323e91bcbd185ca1a14099fba7c021f` · https://github.com/JamesNK/Newtonsoft.Json | MIT · Copyright (c) 2007 James Newton-King | [LICENSE.md](third-party/Newtonsoft.Json/LICENSE.md) |

## 分发要求

- 分发 LightMove 二进制或源码时，必须保留以上三份完整许可文本及本文件。
- 根目录 [LICENSE](LICENSE) 是 Raw Accel 的 MIT 文本，也是 LightMove 继承的 MIT 许可文本。
- ShakeToFindCursor 的来源、改动与许可见 [NOTICE](third-party/ShakeToFindCursor/NOTICE.md)；Newtonsoft.Json 的版本、来源与使用方式见 [NOTICE](third-party/Newtonsoft.Json/NOTICE.md)。
- 已发布的 0.5.0-preview EXE 内置 Raw Accel 与 ShakeToFindCursor 许可文本，但未内嵌 Newtonsoft.Json 13.0.1 的完整 MIT 文本。发布附件中的配套许可 ZIP 补齐该文件；不得表述为该 EXE 已包含 Newtonsoft.Json 完整许可。
- 未来构建的发行包由 `packaging/build-lightmove.ps1` 按仓库路径放入三份完整许可（根目录 `LICENSE`、`third-party/ShakeToFindCursor/LICENSE`、`third-party/Newtonsoft.Json/LICENSE.md`），并在 `开源组件许可.txt` 中汇总。

## 来源与修改

- **Raw Accel**：使用官方 v1.7.1 的驱动、封装库与算法，保留原文件、格式、签名与 MIT 许可。LightMove 的中文界面、曲线工作台、托盘和摇晃查找指针等桌面交互为社区扩展，不代表 Raw Accel 官方发行版，也不声称全部原创。
- **ShakeToFindCursor**：复用其摇晃识别与原生指针替换 / 恢复思路，并以 C# 适配；新增时间窗口、过滤、偏好与托盘集成等改动，详见其 NOTICE。
- **Newtonsoft.Json**：通过 NuGet 使用官方 13.0.1 程序集，未修改；仅作为 JSON 序列化依赖。现有 0.5.0-preview 二进制中的 `Newtonsoft.Json.dll` 文件版本为 13.0.1.25517。
