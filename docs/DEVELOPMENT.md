# 开发指南

## 目录

| 目录 / 文件 | 用途 |
|---|---|
| `grapher/CurveEditing/` | 白色曲线工作台、实时标记、吸附、撤回、现代窗口组件 |
| `grapher/Desktop/` | 启动设置、托盘生命周期、单实例恢复 |
| `grapher/Models/` | 继承上游的数据与设备层；扩展驱动事务及独立输入接收器 |
| `packaging/` | EXE 启动器及单文件打包脚本 |
| `scripts/` | 官方依赖准备、构建、测试入口 |
| `tests/` | 曲线、界面、启动设置及输入生命周期检查 |
| `doc/assets/` | 图标、界面图和明确标注的模拟演示 |
| `docs/` | 项目开发、架构、发布及来源说明 |
| `.github/workflows/` | Windows 构建检查；不自动公开发布 |
| `artifacts/` | 忽略版本控制的构建输出 |
| `driver/`、`wrapper/`、`installer/` 等 | 上游原有源码，保留相对路径 |
| `project.json` | 桌面版本、口号、平台与上游依赖版本 |

## 构建

```powershell
$official = & .\scripts\get-official.ps1
& .\scripts\build.ps1 -OfficialDirectory $official
```

也可将已解压的官方目录直接传入。生成文件位于 `artifacts/LightMove/`。底层驱动版本与官方封装兼容检测保持上游 v1.7.1；桌面包版本独立记录在 `project.json`。

## 运行

`app/rawaccel.exe` 默认打开现代工作台。`--legacy` 保留上游中文界面入口；发行 EXE 不需要这个参数。`--background` 仅在启动设置允许时收进托盘。已有实例时再次运行会唤回原窗口；后台重复运行不弹窗。

## 验证边界

默认检查使用驱动默认数据或回调，不改变真实驱动配置。另有 Windows 运行项和托盘行为的替身检查；没有为了测试修改用户真正的开机启动项。真实输入跟随、托盘菜单和登录后启动应在 Windows 桌面验收。当前机器尚未通过一次真实注销 / 重启验证自动启动。

CI 配置已提供；只有推送到 GitHub 后才会执行。不得把尚未执行的远端 CI 写成已经通过。
