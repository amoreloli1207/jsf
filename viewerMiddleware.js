// viewerMiddleware.js
// 被 viewerltdframe.html 引用。business.html 的「关闭 Ltd 窗口」按钮经 javaframe.js 用 postMessage 触发本文件的 shutdown()。
// shutdown 经本地 LocalService 通知 C# 关闭（tdsHost 定义在 tdsviewerExtension.js，引用本文件的页面须先引入它）；
// 同时把 javaroot 中的 viewerltdframe 帧恢复为空白（相当于关闭查看器）。
function shutdown() {
  console.log('[viewerMiddleware] shutdown called');

  // 1) 通知 C# 关闭：POST /api/host/shutdown，带 keepalive，下面的导航不会打断请求。
  tdsHost.shutdown({
    from: 'viewerltdframe',
    text: 'shutdown request'
  }).then(function (result) {
    console.log('[viewerMiddleware] shutdown 回应:', result);
  }).catch(function (err) {
    console.error('[viewerMiddleware] shutdown 失败:', err);
  });

  // 2) 关闭 javaroot 中的 viewerltdframe 帧（恢复为空白）。
  //    keepalive 已保证请求发完；保留短延迟只是让上面的日志有机会输出。
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
