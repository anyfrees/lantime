# LanTime 1.0.4 发布校验

- 当前 Windows 11 / .NET Framework 4.x：74 项功能检查、47 项界面检查通过。
- 1.0.3 MSI 的错误 2228 已复现，日志定位到 AppSearch 查询 Signature 表失败。1.0.4 补全标准定位器表。
- 新增 21 项 MSI 预检：使用安全会话实际调用原生 AppSearch、LaunchConditions、CostInitialize、FileCost、CostFinalize。旧版被准确拒绝，修正版通过；此预检不改变系统。
- 当前电脑已通过正常 `/i` 安装、`/fomus` 修复、`/x` 卸载和重新安装流程，退出码均为 0；实际安装目录、桌面和开始菜单快捷方式目标正确，卸载后快捷方式和程序文件移除。
- 安装后的程序重新通过 74 项功能检查，Windows Installer 详细日志保存在本地 `dist/verification`。日志包含本机路径，不上传到源码仓库。
- 真实回环 UDP：NTPv3 / NTPv4、来源多数一致、异常来源排除、CIDR 限制、过期状态等检查通过。
- 自动校准 Windows 时间通过注入模拟系统时钟验收，包括多源分歧、权限失败、写入后回读、反向校准、休眠恢复；没有实际改变测试电脑的系统时间。
- 尚未完成 Win7 实机、高 DPI 多屏、SCM 服务升级维护、开机和长时间运行验收；这些不应从当前电脑安装成功推断。
- 安装包未做代码签名。使用前可对照 `SHA256SUMS.txt`。

运行 `build.ps1` 编译应用；运行 `package.ps1` 执行功能 / 界面 / MSI 原生预检并构建便携 ZIP 和 MSI。生成物、用户数据和本地工具不会被提交到 Git。
