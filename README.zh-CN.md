# Raw Accel 简体中文界面

基于 [RawAccelOfficial/rawaccel v1.7.1](https://github.com/RawAccelOfficial/rawaccel/releases/tag/v1.7.1) 的非官方界面汉化，维护于 `zh-CN` 分支。保留原项目 MIT 许可证和作者署名。

汉化范围：主窗口、菜单、八种曲线名称、参数标签、图表标题/图例、设备窗口、关于窗口、查找表校验提示、常见的未安装驱动提示。曲线保留英文名称便于对照官方文档。底层库返回的部分诊断文字、官方安装器及卸载器保留原文。

仅编译 C# 图形界面。驱动 `rawaccel.sys`、`wrapper.dll`、安装器、卸载器、`writer.exe` 和 `Newtonsoft.Json.dll` 均使用官方发布包原文件。配置字段、枚举值及鼠标加速算法保持上游格式；数字解析继续使用固定格式，避免中文区域设置影响小数点。

## 使用

1. 将整个软件文件夹放在固定位置，不要只移动 `rawaccel.exe`。
2. 首次使用，右键同目录 `installer.exe`，选择“以管理员身份运行”，按提示完成安装。
3. 安装后重启电脑。
4. 打开 `rawaccel.exe`，即为中文界面。`rawaccel-original.exe` 是保留的官方英文界面。
5. 安装过官方驱动且已经重启的电脑，通常可直接打开界面。

适用官方 v1.7.1 发布包支持的 Windows 10/11 x64。依赖 .NET Framework 4.7.2+ 和 Visual C++ 2019 x64 运行库，具体要求以[官方指南](https://github.com/RawAccelOfficial/rawaccel/blob/v1.7.1/doc/Guide.md)为准。

## “少挪鼠标，慢速精调”的调节方向

- 固定鼠标硬件 DPI，先用“灵敏度倍率”确定舒服的慢速操作手感。
- 可从“自然 Natural”曲线开始，温和提高快速移动时的灵敏度，并限制高速上限。
- “输入阈值”控制从什么手速开始加速；“上限”控制高速端的倍率。
- 使用自定义加速时，建议先关闭 Windows 的“提高指针精确度”，避免叠加加速。
- 曲线中的“线性 Linear”仍然是加速模式；关闭加速应选择“关闭 Off”。
- 右侧“当前值”显示已生效参数。修改输入框后，点击“应用”才写入驱动；“重置”按钮会重置驱动。

本包未代替你安装驱动、修改 DPI 或应用加速设置。由于尚未知晓鼠标 DPI 与屏幕配置，没有预先写入个人加速方案。

## 从源码编译中文界面

需要 Windows x64、Windows PowerShell 5.1 和 .NET SDK（包含 Roslyn 编译器）。优先使用本机 .NET Framework 4.7.2 引用程序集；缺少时使用系统 Framework 程序集。

先下载并解压官方 `RawAccel_v1.7.1.zip`。其官方 SHA-256：

```text
770fe3ae0919ca3c4d412f58c985eb27f5434decad809f7e8206de4e8852eec4
```

在仓库根目录执行，参数填解压后的 `RawAccel` 文件夹路径：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-zh-CN.ps1 -OfficialDirectory C:\path\to\RawAccel
```

默认输出到 `artifacts\RawAccel-zh-CN`，包含官方依赖和英文原版备份。该脚本不会安装驱动或修改系统鼠标设置。中文界面属于本分支自行编译的非官方程序，不应冒充官方签名版本。

## 验证范围

已完成本机编译、八种曲线的界面渲染、中文名称与底层模式对应、配置序列化往返、设备及关于窗口检查。界面验证使用隔离的测试副本和默认配置，禁用驱动写入/重置，不要求安装驱动。

尚未进行安装后的实际鼠标手感测试；需要安装官方驱动并重启后在目标设备上调节。上游内部版本号可能与 GitHub 发布标签不完全一致，关于窗口保留真实程序集版本号。
