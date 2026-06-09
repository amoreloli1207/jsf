// content_script.js
// 注入所有帧（all_frames + document_start），监听页面通过 window.postMessage 发来的消息，
// 命中 __tdsviewer 标记后用 chrome.runtime.sendMessage 转发给 background。
console.log('[content_script] loaded in', location.href);

window.addEventListener('message', (event) => {
  // 只处理本窗口自己 postMessage 出来的消息，避免接收其它来源
  if (event.source !== window) return;

  const data = event.data;
  if (!data || data.__tdsviewer !== true) return;

  console.log('[content_script] 收到页面 postMessage:', data);

  chrome.runtime.sendMessage({ from: 'content_script', data }, (resp) => {
    if (chrome.runtime.lastError) {
      console.warn('[content_script] sendMessage 出错:', chrome.runtime.lastError.message);
      return;
    }
    console.log('[content_script] background 回应:', resp);
  });
});
