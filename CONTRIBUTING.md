# 参与开发

轻移保留 Raw Accel 上游工程路径，以便继续合并底层更新。桌面扩展集中在 `grapher/CurveEditing` 和 `grapher/Desktop`。

1. 准备 Windows x64、Windows PowerShell 5.1 与 .NET SDK。
2. 从官方 v1.7.1 发行版准备依赖，或执行 `scripts/get-official.ps1`。
3. 执行 `scripts/build.ps1 -OfficialDirectory C:\path\to\RawAccel`。默认编译、运行离线检查、生成单文件 EXE 与 SHA-256 文件。
4. 以小而明确的变更提交 PR，说明行为变化和验证结果。

不要提交个人驱动设置、备份、真实输入日志、账户信息或构建产物。官方驱动与封装库保持原始文件；修改底层驱动不属于当前桌面版本构建流程。

桌面截图工具不可用时，应明确区分离屏渲染与用户实机确认。默认测试不向真实驱动写入；实际写入测试需要明确授权。

详见 [开发指南](docs/DEVELOPMENT.md)、[架构说明](docs/ARCHITECTURE.md) 和 [测试说明](tests/README.md)。
