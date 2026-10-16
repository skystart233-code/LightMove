# 架构与状态

`Program` 创建上游设备 / 驱动后端，再创建现代编辑器，由 `TraySession` 管理窗口和进程生命周期。输入接收使用独立消息窗口，收起编辑器不会销毁它。

`CurveEditorForm` 管理两轴草稿、撤回历史与应用回调。蓝线来自待应用曲线；灰线和灰菱形来自已生效配置采样。`SettingsManager` 在应用时备份完整配置、写入驱动、严格回读并保存；成功后才更新灰色参考点。

`DesktopSettingsService` 将启动与托盘偏好保存在 `%LocalAppData%\LightMove\preferences.json`。开机启动使用当前用户 `Software\Microsoft\Windows\CurrentVersion\Run` 的 `LightMove` 项；路径带引号并附加 `--background`。只在用户开启后注册；更新启动目标不会自行开启自启，亦不绕过 Windows 的启动应用禁用状态。保存偏好失败时恢复原运行项。

单文件启动器解包到 `%LocalAppData%\Programs\LightMove\preview-<内容哈希>`。新版本会继承设置和曲线缓存；自启已开启时，新主程序将运行项更新至当前解包路径。入口显式设置工作目录，确保 Windows 登录启动时仍读取自己的配置。

默认关闭 / 最小化隐藏窗口，草稿保持在同一进程内；显式退出才释放托盘与后端资源。正在应用曲线时不允许退出。关闭、退出进程均不会自行关闭内核驱动的已生效曲线。
