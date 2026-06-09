// background.js (service worker)
// 接收 content_script 转发来的消息，连到本地 native host (demo.exe) 并把消息发过去。
// 用 connectNative 建立长连接：让 demo.exe 持续运行、日志窗口保持打开。

const HOST_NAME = 'com.demo.host'; // 必须与 native host 清单的 name、注册表项名一致

let port = null;

function ensurePort() {
  if (port) return port;

  console.log('[background] connectNative ->', HOST_NAME);
  port = chrome.runtime.connectNative(HOST_NAME);

  port.onMessage.addListener((msg) => {
    console.log('[background] 收到 native host 回送:', msg);
  });

  port.onDisconnect.addListener(() => {
    const err = chrome.runtime.lastError;
    console.warn('[background] native host 断开:', err && err.message);
    port = null;
  });

  return port;
}

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  console.log('[background] onMessage from content_script:', message);
  try {
    const p = ensurePort();
    // 给 demo.exe 发送一条信息
    p.postMessage({ type: 'fromPage', payload: message });
    sendResponse({ ok: true });
  } catch (e) {
    console.error('[background] 发送 native 消息失败:', e);
    sendResponse({ ok: false, error: String(e) });
  }
  return true; // 允许异步 sendResponse
});
