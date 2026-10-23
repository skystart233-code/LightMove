# 发布流程

轻移的正式仓库是 [skystart233-code/LightMove](https://github.com/skystart233-code/LightMove)，默认分支为 `main`。迁移完成后，历史仓库 [skystart233-code/rawaccel](https://github.com/skystart233-code/rawaccel) 保留为公开存档，不再作为发布入口。

公开同步前先审阅 README、功能、许可归属与发布资产。任何推送、创建仓库、Release 或归档操作都必须由维护者明确确认；本文件不构成自动发布授权。

## 发布前检查

1. 在干净的独立 checkout 中工作，确认 `git status` 无未提交改动。扫描 tracked files 与历史路径，确认没有个人配置、账户数据、密钥、令牌、日志或真实鼠标输入记录。
2. 检查产品文档、issue 模板、workflow 和下载链接指向 `skystart233-code/LightMove`；保留 `RawAccelOfficial/rawaccel`、`0cch/ShakeToFindCursor` 与 `JamesNK/Newtonsoft.Json` 等上游来源链接。README 必须说明这是基于上游的独立仓库，不声称全部原创。
3. 核对 `LICENSE`、`third-party/ShakeToFindCursor/LICENSE`、`third-party/Newtonsoft.Json/LICENSE.md` 三份完整 MIT 文本，以及 [THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md)。确认 `packaging/build-lightmove.ps1` 会把三份许可放入发行包。
4. 功能发布执行完整构建、离线检查与实机验证；仓库迁移发布**不得重编译已验证二进制**，应复用原 Release 的 EXE，核对字节与 SHA-256 后原样上传。
5. 从准备发布的 commit/tag 生成新的源代码 ZIP；不得复用旧 source ZIP。为已冻结的二进制另行准备包含三份完整许可的 companion ZIP。
6. 更新 `project.json`、`CHANGELOG.md` 与版本说明。功能发布才提升桌面版本；迁移发布保留原版本号。原始驱动兼容版本不要随桌面版本修改。
7. 发布说明明确：本次是功能发布还是仓库迁移、是否重编译、EXE 的 SHA-256、源码 ZIP 的来源 commit/tag、许可 ZIP 的作用，以及尚未完成的验证范围。

## 发布步骤

1. 审核通过后提交到 `main`，创建或更新版本 tag。
2. 创建公开仓库 `skystart233-code/LightMove`（非 fork），推送 `main` 与 tag，并将默认分支设为 `main`。
3. 确认仓库为 public、`fork=false`、默认分支 `main`；确认 tag 指向准备好的迁移 commit。
4. 创建 prerelease Release，附上：原始 EXE、EXE 的 `.sha256`、从该 tag 生成的新 source ZIP、companion 许可 ZIP。
5. 核对 Release 资产名称、大小与 SHA-256；确认旧仓库 `v0.5.0-preview` 仍可访问。
6. 在旧仓库 README 顶部加入迁移提示，说明新仓库地址、旧仓库保留为公开存档；随后归档旧仓库并确认仍为 public。
7. 归档后再次核对新仓库 default `main`、public、`isFork=false`，以及 Release 与许可资产完整。

## 边界

Windows 界面包目前无额外代码签名；随附 Raw Accel 驱动保持官方签名。首次驱动安装在干净系统上的完整验证、其他 DPI / 多屏和真实登录启动需持续补充。不能据本机通过宣称已完成所有设备兼容验证，也不能把尚未执行的远端 CI 写成已经通过。
