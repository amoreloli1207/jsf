# JSF Handler PoC

一个概念验证（PoC）：通过 **Microsoft Edge 扩展**打通「网页 ↔ 本地 C# 程序」的双向通信。
页面里的 HTML 模拟由 JSF（后端 Java）生成的静态页面，Edge 扩展作为桥梁，把页面消息转发给本地 C# native host（`demo.exe`），后者在窗口里显示收到的日志。

消息链路：

```
GAMNAPIF.html (body onload)
  → onLoadExtension()            [tdsviewerExtension.js]
    → window.postMessage(...)
      → window.addEventListener  [extension/content_script.js]
        → chrome.runtime.sendMessage(...)
          → chrome.runtime.onMessage.addListener   [extension/background.js]
            → port.postMessage(...)  →  demo.exe（C# WinForm，显示日志）
```

页面层级（frameset）：

```
javaroot.html          上下两帧
├─ business.html       含按钮，点击触发链路
└─ viewerltdframe.html 初始空白，点击后加载；内部又是三帧 frameset
   ├─ GAMNLTDF.html
   ├─ GAMNAPIF.html    ← 引用 tdsviewer*.js，onload 发起 postMessage
   └─ extF.html
```

---

## 前置条件

- Windows 10 / 11 + Microsoft Edge
- **.NET 10 SDK**（`demo.exe` 为框架依赖发布，运行时依赖本机已安装的 .NET 10）
- Windows PowerShell（脚本含中文，已存为 UTF-8 with BOM）

> 下列所有命令均在仓库根目录 `jsf handler\` 下执行。

---

## 一、编译并安装 native host

`register-host.ps1` 会一步完成：发布 `demo.exe` → 复制到 [native-host/](native-host/) → 写入 Edge 的注册表项。

```powershell
powershell -ExecutionPolicy Bypass -File .\native-host\register-host.ps1
```

它做了三件事：

1. `dotnet publish`（框架依赖、单文件）生成约 175 KB 的 `demo.exe`；
2. 把 `demo.exe` 复制到 `native-host\demo.exe`（与 `com.demo.host.json` 同级，对应清单里的 `path: ".\\demo.exe"`）；
3. 在注册表写入 `HKCU\Software\Microsoft\Edge\NativeMessagingHosts\com.demo.host`，默认值指向 [native-host/com.demo.host.json](native-host/com.demo.host.json) 的绝对路径。

仅想在开发时编译（不安装），用普通 build 即可：

```powershell
dotnet build .\native-host\DemoHost.csproj -c Release
```

---

## 二、加载扩展（Edge）

1. 打开 `edge://extensions`；
2. 打开左下角「**开发人员模式**」；
3. 点「**加载解压缩的扩展**」，选择 [extension/](extension/) 文件夹；
4. **记下卡片上显示的扩展 ID**（32 位字母，下一步要用）。

---

## 三、回填扩展 ID

把 [native-host/com.demo.host.json](native-host/com.demo.host.json) 里 `allowed_origins` 的占位 `abcd` 换成上一步的真实扩展 ID：

```json
"allowed_origins": [ "chrome-extension://<这里填真实扩展ID>/" ]
```

> 改 json 后无需重跑注册脚本，存盘即可。注意结尾的 `/` 不能少。

---

## 四、允许访问文件 URL（用本地文件测试时必做）

本 PoC 用 `file://` 直接打开页面测试时，需要给扩展开放文件访问权限：

`edge://extensions` → 该扩展的「**详细信息**」→ 打开「**允许访问文件 URL**」。

---

## 五、运行 / 联调

1. 用 Edge 打开 [javaroot.html](javaroot.html)；
2. 点上方 business 页里的「打开 Ltd 窗口」按钮；
3. 下方帧加载出 viewerltdframe → 其内 `GAMNAPIF.html` 的 `onload` 触发整条链路；
4. **`demo.exe` 窗口弹出**，日志区显示 `[收到 N 字节] {...}`，并自动滚动到底；点「清空」可清空日志。

各层的 `console.log` 可在以下位置查看：

- 页面侧：页面的 DevTools（F12）Console；
- 扩展侧：`edge://extensions` → 该扩展 → 「Service Worker」→ 打开其 DevTools。

---

## 卸载

1. **移除扩展**：`edge://extensions` 里点该扩展卡片的「移除」（仅临时停用则关掉开关即可）；
2. **注销 native host**（删注册表项）：

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\native-host\unregister-host.ps1
   ```

3. 如需彻底清理，可手动删除 `native-host\demo.exe`、`native-host\bin`、`native-host\obj`。

---

## 注意事项

- **扩展 ID 必须与 `allowed_origins` 一致**：每次「移除后重新加载解压扩展」ID 可能变化，变了要回到第三步重新回填，否则 Edge 会以 `Access to the specified native messaging host is forbidden` 拒绝启动 host。
- **`com.demo.host` 三处必须一致**：[com.demo.host.json](native-host/com.demo.host.json) 的 `name`、注册表项名、[background.js](extension/background.js) 里 `connectNative('com.demo.host')`。
- **`demo.exe` 不会自动退出**：用 `connectNative` 长连接保持窗口常开，便于持续看日志；扩展断开后窗口仍在，需手动关闭。
- **stdout 是协议通道**：native host 的标准输出只用于发送「长度前缀 + JSON」消息，任何额外打印都会破坏帧；所有日志只进窗口文本框。
- **PowerShell 脚本编码**：`.ps1` 与含中文的 `.cs` 已存为 UTF-8 with BOM；若另存为无 BOM，简体中文 Windows 下会乱码并报解析错误。
