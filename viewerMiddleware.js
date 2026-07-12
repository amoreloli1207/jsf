// viewerMiddleware.js
// 被 viewerltdframe.html 引用。business.html 的「关闭 Ltd 窗口」按钮经 javaframe.js 用 postMessage 触发本文件的 shutdown()。
// shutdown 走与「打开」按钮大致相同的路线：window.postMessage → content_script → background → 通知 C# 关闭；
// 同时把 javaroot 中的 viewerltdframe 帧恢复为空白（相当于关闭查看器）。
function shutdown() {
  console.log('[viewerMiddleware] shutdown called');

  // 1) 通知 C# 关闭。本函数运行在 viewerltdframe 帧内，postMessage 发到本帧 window，
  //    由注入到本帧的 content_script.js 接收并转发给扩展 background，再发给 native host。
  window.postMessage({
    __tdsviewer: true,
    type: 'shutdown',
    from: 'viewerltdframe',
    text: 'shutdown request'
  }, '*');

  // 2) 关闭 javaroot 中的 viewerltdframe 帧（恢复为空白）。
  //    稍作延迟，确保上面的消息先被 content_script 转发出去——本帧导航到 about:blank 会销毁当前文档。
  setTimeout(function () {
    parent.frames['viewerltdframe'].location.href = 'about:blank';
  }, 200);
}

// 监听来自 business 帧的关闭指令。business 与本帧在 file:// 下互为跨源，无法直接调用本帧函数，
// 故 business 改用 postMessage 触发；本监听器收到指令后执行 shutdown()。
window.addEventListener('message', function (event) {
  if (event.data && event.data.__viewerCommand === 'shutdown') {
    console.log('[viewerMiddleware] 收到 shutdown 指令');
    shutdown();
  }
});
