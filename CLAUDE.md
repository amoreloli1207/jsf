# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> 人工操作步骤（编译 / 安装 / 加载扩展 / 卸载）见 [README.md](README.md)。本文件聚焦架构与本项目特有的坑。

## 项目性质

PoC（概念验证），运行于 **Windows + Microsoft Edge**。目标：打通**网页（web）与本地 C# 程序之间的双向通信**。

关于名字「jsf handler」：本项目**不含 Java 代码**。其中的 HTML 是「模拟由后端 JSF（JavaServer Faces / Java）生成的静态页面」的产物——即把 JSF 服务端渲染出的页面当作既定输入，PoC 只关注页面加载后如何与本地程序通信。「handler」指负责处理来自该页面请求的本地 C# 端。

技术栈：**HTML + JavaScript + Edge Extension（MV3）+ C#（.NET 10 WinForms）**。

## 整体架构

三层 + 一条贯穿的双向数据流。理解本项目必须把这三者作为一个整体来看，单看任一层都不完整：

```
┌─────────────────────┐   postMessage /        ┌────────────────────┐   Native Messaging      ┌─────────────────────┐
│  Web 页面            │   chrome.runtime        │  Edge Extension     │   (stdin/stdout,        │  C# Native Host     │
│  (静态 HTML + JS，   │ ◄────────────────────► │  content script +   │ ◄────────────────────► │  (本地 "handler"，   │
│   模拟 JSF 输出)     │   .sendMessage          │  background/SW)     │   4字节长度前缀 + JSON) │   demo.exe 显示日志) │
└─────────────────────┘                         └────────────────────┘                         └─────────────────────┘
```

1. **Web 层（HTML + JavaScript）**
   - 静态 HTML，代表 JSF 后端原本会生成的页面；用嵌套 frameset 组织。
   - 发起方是 `GAMNAPIF.html`：其 `<body onload>` 调用 `tdsviewerExtension.js` 的 `onLoadExtension()`，后者用 `window.postMessage` 把消息（带 `__tdsviewer` 标记）发给同帧的 content script。

2. **Edge Extension（桥梁，[extension/](extension/)）**
   - `content_script.js`：`window.addEventListener('message')` 收页面消息，经 `chrome.runtime.sendMessage` 转发给 background。
   - `background.js`（service worker）：`chrome.runtime.onMessage.addListener` 收到后，用 `chrome.runtime.connectNative('com.demo.host')` 建立长连接并 `port.postMessage` 发给本地宿主。
   - `manifest.json`：MV3；`content_scripts.matches` 直接锁定 `GAMNAPIF.html`，但因目标是子帧，**必须保留 `all_frames: true`**；`run_at: document_start` 保证监听器先于页面 onload 注册；`permissions: ["nativeMessaging"]`。

3. **C# Native Host（本地处理端，[native-host/](native-host/)）**
   - .NET 10 WinForms，`AssemblyName=demo` → 输出 `demo.exe`；窗口内一个只读多行 TextBox 显示日志（自动滚到底，带「清空」按钮）。
   - 由 Edge 通过 `connectNative` 按需拉起；通信经 **stdin/stdout**，每条消息 = **4 字节小端长度前缀 + UTF-8 JSON**。
   - 注册：`com.demo.host.json`（含 `name` / `path: ".\\demo.exe"` / `type: "stdio"` / `allowed_origins`）+ 注册表项 `HKCU\Software\Microsoft\Edge\NativeMessagingHosts\com.demo.host` 指向该清单。

### 数据流（一次往返）
`GAMNAPIF.html` onload → `onLoadExtension()` → `window.postMessage` → `content_script.js` → `chrome.runtime.sendMessage` → `background.js` onMessage → `connectNative` 的 port → `demo.exe` 经 stdin 收消息并显示（可选回送 ack）。

## 仓库结构

- **仓库根** — 模拟 JSF 输出的静态页面与页面侧 JS：
  - [javaroot.html](javaroot.html) — 顶层 frameset（上 `business`、下 `viewerltdframe`）。
  - [business.html](business.html) — 含按钮，点击 → [javaframe.js](javaframe.js) 的 `openLtdWindow()` → `openViewWindow()`（把 `viewerltdframe.html` 载入下方帧）。
  - [viewerltdframe.html](viewerltdframe.html) — 嵌套 frameset：[GAMNLTDF.html](GAMNLTDF.html) / [GAMNAPIF.html](GAMNAPIF.html) / [extF.html](extF.html)。
  - [tdsviewerExtension.js](tdsviewerExtension.js)（`onLoadExtension`）、[tdsviewerApplet.js](tdsviewerApplet.js)（占位）。
- **[extension/](extension/)** — MV3 扩展「demo extension」：`manifest.json` / `content_script.js` / `background.js`。
- **[native-host/](native-host/)** — C# 宿主：`DemoHost.csproj` / `Program.cs` / `MainForm.cs` / `com.demo.host.json` / `register-host.ps1` / `unregister-host.ps1`。
- `demo.exe` 由 `register-host.ps1` 发布生成（已被 `.gitignore` 忽略）。

## 开发命令

- **仅编译（开发）**：`dotnet build .\native-host\DemoHost.csproj -c Release`
- **发布 + 注册（一键安装）**：`powershell -ExecutionPolicy Bypass -File .\native-host\register-host.ps1`
  - 框架依赖单文件发布 `demo.exe`（约 175 KB，依赖本机已装的 .NET 10），复制到 native-host，并写注册表。
- **卸载 native host**：`powershell -ExecutionPolicy Bypass -File .\native-host\unregister-host.ps1`
- 加载扩展 / 回填扩展 ID / 允许文件 URL / 联调等浏览器侧人工步骤见 [README.md](README.md)。

## 该项目特有的注意事项

- **发布不要 `-o` 到项目源码目录**：`dotnet publish -o native-host`（输出目录 = 项目目录）会触发 `CSC error CS5001（找不到 Main）`。脚本已改为发布到默认 `bin\...\publish\` 再复制单文件出来。
- **含中文的 `.ps1` / `.cs` 必须存为 UTF-8 with BOM**：简体中文 Windows + PowerShell 5.1 读无 BOM 文件会按 GBK 解码 → 乱码并报「字符串缺少终止符」；csc 同理会让中文字符串字面量变乱码。用 Edit 改动这些文件后需重新补 BOM。
- **不要往 native host 的 stdout 打印日志**：stdout 是协议通道，任何额外字节都会破坏「长度前缀 + JSON」帧。所有日志只进窗口的 TextBox（`MainForm.AppendLog`）。
- **字节序与编码**：长度前缀是 **4 字节小端**，负载是 **UTF-8 JSON**；C# 端 `BitConverter` + `Encoding.UTF8` 显式处理。
- **扩展 ID 与 `allowed_origins` 必须一致**：每次重新加载解压扩展可能改变 ID；ID 变了要同步更新 `com.demo.host.json` 的 `allowed_origins`，否则 Edge 拒绝启动 host。
- **`com.demo.host` 三处一致**：`com.demo.host.json` 的 `name`、注册表项名、`background.js` 里 `connectNative('com.demo.host')`。
- **HTML 是「输入」不是「产物」**：根目录 HTML 模拟 JSF 服务端输出，PoC 不负责生成它；改通信逻辑时不要误以为要去「渲染」页面。
- **平台限定 Windows**：依赖 Edge + 注册表注册，C# 端面向 Windows。

## 约定

代码标识符（类名 / 方法名 / 字段名 / manifest key）一律用英文原文；面向用户的输出与 commit message 用简体中文（见用户全局 CLAUDE.md，不在此重复）。
