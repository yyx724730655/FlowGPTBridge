# FlowGPT Bridge

FlowGPT Bridge 是一个 Windows 平台的 Flow Launcher 插件。它通过 `gpt` 关键字打开或激活 ChatGPT 桌面端，并使用用户可配置的快捷键切换 Chat、Work、Codex 或新建聊天。

插件不调用 OpenAI API，不使用 UI Automation，不读取聊天内容，也不会自动粘贴或发送 Prompt。

## 当前版本

第一版源码实现了：

- `gpt` 打开或激活 ChatGPT，并保持上次页面；
- 普通 Prompt 与 `/chat`、`/work`、`/codex`、`/current`、`/new` 命令；
- `--` 转义，避免 Prompt 与保留命令混淆；
- 按当前配置生成 Flow 结果列表，查询阶段没有启动应用等副作用；
- 从 Windows 开始菜单注册信息、手动路径或 AppUserModelID 启动 ChatGPT；
- 恢复最小化窗口、置前并按进程身份再次验证焦点；
- 使用串行 `SendInput` 发送模式/新聊天快捷键；
- 写入 Unicode 剪贴板并短间隔重试；
- WPF 设置页：录制、清除、测试和恢复四个快捷键；
- 解析、结果列表和执行计划单元测试。

## 构建

要求：

- Windows 10/11；
- .NET 9 SDK；
- Visual Studio 2022（可选）。

```powershell
dotnet restore .\FlowGPTBridge.sln
dotnet test .\FlowGPTBridge.sln
dotnet publish .\FlowGPTBridge\FlowGPTBridge.csproj -c Release -o .\artifacts\FlowGPTBridge
```

## 本地安装

1. 在 Flow Launcher 中执行 `Flow Launcher UserData Folder`。
2. 打开其中的 `Plugins` 目录。
3. 将 `artifacts\FlowGPTBridge` 整个目录复制到 `Plugins\FlowGPTBridge`。
4. 重启 Flow Launcher。
5. 输入 `gpt` 验证列表；首次使用前打开插件设置，确认快捷键与 ChatGPT 中的配置一致。

开发期间也可以用目录联接避免反复复制：

```cmd
mklink /J "%APPDATA%\FlowLauncher\Plugins\FlowGPTBridge" "D:\path\to\artifacts\FlowGPTBridge"
```

## 查询示例

| 输入 | 行为 |
|---|---|
| `gpt` | 打开 ChatGPT，保持当前页面 |
| `gpt 帮我整理需求` | 在默认模式新建聊天并复制 Prompt |
| `gpt /work` | 仅切换到 Work |
| `gpt /codex fix tests` | 切换 Codex、新建聊天并复制 Prompt |
| `gpt /new` | 当前模式新建聊天 |
| `gpt -- /work 的含义` | 将 `/work 的含义` 作为普通 Prompt |

## 安全边界

- 发送每个快捷键前都会用前台窗口 PID 再次验证 ChatGPT 进程身份；验证失败即停止。
- 新聊天快捷键失败时不会复制 Prompt，避免用户误以为已经进入新会话。
- Prompt 不会写入日志，结果列表只显示单行截断预览。
- 全部代码中没有 `Ctrl+V`、`Enter`、屏幕坐标点击或 UI Automation。

## Windows 集成测试清单

单元测试之外，发布前还需要在真实 Windows 环境手动验证：

- ChatGPT 未运行、运行中、最小化和启动缓慢；
- 无法取得焦点时其他应用不会收到快捷键；
- 按住 Alt 呼出 Flow 后不会发生修饰键粘连；
- 修改四个快捷键后发送的是新配置；
- Prompt 进入剪贴板，但 ChatGPT 输入框保持为空；
- 全流程没有自动粘贴或自动发送。
