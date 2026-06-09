# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> ⚠️ **占位版（脚手架）**：本仓库目前为空，尚无实际代码。本文件依据项目意图预先记录**计划中的架构**。带 `TODO` 的部分需在对应代码落地后补全；请勿把这里的命令/路径当作已存在的事实——先确认文件存在再引用。

## 项目性质

PoC（概念验证），运行于 **Windows + Microsoft Edge**。目标：打通**网页（web）与本地 C# 程序之间的双向通信**。

关于名字「jsf handler」：本项目**不含 Java 代码**。其中的 HTML 是「模拟由后端 JSF（JavaServer Faces / Java）生成的静态页面」的产物——即把 JSF 服务端渲染出的页面当作既定输入，PoC 只关注页面加载后如何与本地程序通信。「handler」指负责处理来自该页面请求的本地 C# 端。

技术栈：**HTML + JavaScript + Edge Extension + C#**。

## 整体架构（计划中）

三层 + 一条贯穿的双向数据流。理解本项目必须把这三者作为一个整体来看，单看任一层都不完整：

```
┌─────────────────────┐   postMessage /        ┌────────────────────┐   Native Messaging      ┌─────────────────────┐
│  Web 页面            │   chrome.runtime        │  Edge Extension     │   (stdin/stdout,        │  C# Native Host     │
│  (静态 HTML + JS，   │ ◄────────────────────► │  content script +   │ ◄────────────────────► │  (本地 "handler"，   │
│   模拟 JSF 输出)     │   .sendMessage          │  background/SW)     │   4字节长度前缀 + JSON) │   执行本地操作)      │
└─────────────────────┘                         └────────────────────┘                         └─────────────────────┘
```

1. **Web 层（HTML + JavaScript）**
   - 静态 HTML，代表 JSF 后端原本会生成的页面。
   - 页面内 JS 是发起方：把请求发给扩展，并接收本地 C# 返回的结果。
   - 页面与扩展之间通常走 `window.postMessage`（页面 ↔ content script）或扩展注入的桥接 API。

2. **Edge Extension（桥梁）**
   - **content script**：注入目标页面，在「页面 DOM 世界」与「扩展世界」之间转发消息。
   - **background / service worker**：持有与本地程序的 Native Messaging 连接，转发两侧消息。
   - 关键 API：`chrome.runtime.connectNative` / `chrome.runtime.sendNativeMessage`（Edge 兼容 Chromium 扩展 API）。
   - `manifest.json` 中需声明 `nativeMessaging` 权限，并匹配目标页面的 host。

3. **C# Native Host（本地处理端，即 "handler"）**
   - 作为 **Native Messaging Host** 注册到系统，被 Edge 按需拉起。
   - 通信协议（Chromium 原生消息规范）：经 **stdin/stdout**，每条消息 = **4 字节小端长度前缀 + UTF-8 JSON 负载**。读写两端都必须遵守该帧格式，这是最容易出错的地方。
   - 注册方式：一个 **native host manifest（JSON）**（含 host 名称、可执行文件路径、`allowed_origins` = 扩展 ID）+ **注册表项**指向该 manifest：
     - `HKCU\Software\Microsoft\Edge\NativeMessagingHosts\<host-name>`（或 `HKLM` 全局）。

### 数据流（一次典型往返）
页面 JS → `postMessage` → content script → `runtime.sendMessage` → background → `connectNative` 写入（长度前缀+JSON） → C# 进程 stdin → C# 处理本地操作 → 写回 stdout（长度前缀+JSON） → background → content script → `postMessage` → 页面 JS 收到结果。

## 仓库结构（计划中）

代码尚未创建。建议（落地后据实更新本节）：

- `extension/` — Edge 扩展：`manifest.json`、content script、background/service worker。
- `native-host/` — C# 工程：native messaging host 实现 + 安装/注册脚本。
- `web/` — 模拟 JSF 输出的静态 HTML + JS 演示页。
- `*.json`（native host manifest）、注册脚本（`.reg` / PowerShell）。

> TODO：代码落地后，用实际目录名替换以上推测，并删除本提示。

## 开发命令

> TODO：以下均为**待补全**占位。请在真实工程文件（`.csproj` / `manifest.json` / 安装脚本）出现后，用经过验证的实际命令替换，并删除本警告。

- **构建 C# native host**：`TODO`（如 `dotnet build native-host/` 或 `.sln` 对应命令）。
- **加载扩展**：Edge → `edge://extensions` → 开启「开发人员模式」→「加载解压缩的扩展」→ 选择 `extension/` 目录 → 记录生成的**扩展 ID**。
- **注册 native host**：`TODO`（运行注册脚本，把 manifest 路径写入 `HKCU\Software\Microsoft\Edge\NativeMessagingHosts\<host-name>`；manifest 的 `allowed_origins` 必须填上一步的扩展 ID）。
- **运行 / 调试**：`TODO`（打开模拟页面；用扩展的 service worker DevTools 看消息；native host 的 stdout 不能用于日志——会破坏帧协议，需单独写日志文件）。
- **测试**：`TODO`（PoC 阶段可能以手动验证为主）。

## 该项目特有的注意事项

- **不要往 native host 的 stdout 打印日志**：stdout 是协议通道，任何额外字节都会破坏「长度前缀 + JSON」帧，导致扩展解析失败。日志请写文件或 stderr。
- **字节序与编码**：长度前缀是 **4 字节小端**，负载是 **UTF-8 JSON**；C# 端读写须显式按此处理，不要依赖默认编码。
- **扩展 ID 与 `allowed_origins` 必须一致**：每次重新加载解压扩展可能改变 ID；ID 变了要同步更新 native host manifest 的 `allowed_origins`，否则 Edge 拒绝启动 host。
- **平台限定 Windows**：依赖 Edge + 注册表注册，C# 端默认面向 Windows。
- **HTML 是「输入」不是「产物」**：`web/` 下的 HTML 模拟 JSF 服务端输出，PoC 不负责生成它；改动通信逻辑时不要误以为要去「渲染」页面。

## 约定

代码标识符（类名 / 方法名 / 字段名 / manifest key）一律用英文原文；面向用户的输出与 commit message 用简体中文（见用户全局 CLAUDE.md，不在此重复）。
