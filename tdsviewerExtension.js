// tdsviewerExtension.js
// 页面侧扩展桥。GAMNAPIF.html 的 body onload 调用 onLoadExtension，
// 通过 window.postMessage 把消息发给同帧内的 content_script.js。
function onLoadExtension() {
  console.log('[tdsviewerExtension] onLoadExtension called');

  var message = {
    __tdsviewer: true,            // content_script 用来过滤消息的标记
    type: 'onLoadExtension',
    from: 'GAMNAPIF',
    text: 'hello from GAMNAPIF onload'
  };

  // targetOrigin 用 '*'：file:// 下 origin 为 "null"，PoC 不做严格校验
  window.postMessage(message, '*');
  console.log('[tdsviewerExtension] postMessage 已发送:', message);
}
