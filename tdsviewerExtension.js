// tdsviewerExtension.js
// 页面侧本地桥。GAMNAPIF.html 的 body onload 调用 onLoadExtension。
// 原先经 window.postMessage → Edge 扩展 → native host；现改为直连本地 LocalService（ASP.NET Core）：
//   页面 → 本地：fetch POST；本地 → 页面：SSE（EventSource）。设计见 docs/webapi-migration.md。

// 传输封装：调用点只认 tdsHost，不关心底层是 fetch 还是别的
var tdsHost = (function () {
  var BASE_URL = 'http://127.0.0.1:17320'; // 与 LocalService appsettings.json 的 Urls 一致

  var eventSource = null;
  var handlers = {}; // eventName -> [handler]

  function request(method, path, body, options) {
    var init = { method: method };
    if (body !== undefined) {
      init.headers = { 'Content-Type': 'application/json' }; // LocalService 强制 JSON
      init.body = JSON.stringify(body);
    }
    // keepalive：页面卸载/跳转后请求仍会发完（shutdown 后紧跟着导航到 about:blank）
    if (options && options.keepalive) init.keepalive = true;

    return fetch(BASE_URL + path, init).then(function (resp) {
      return resp.json().catch(function () { return {}; }).then(function (json) {
        if (!resp.ok) throw new Error(json.error || ('HTTP ' + resp.status));
        return json;
      });
    });
  }

  // 每个页面只建一条 EventSource：浏览器到 127.0.0.1 走 HTTP/1.1，同主机仅 6 条并发连接
  function ensureEventSource() {
    if (eventSource) return eventSource;
    eventSource = new EventSource(BASE_URL + '/api/host/events');
    eventSource.onerror = function () {
      // EventSource 会自动重连，这里只记录
      console.warn('[tdsHost] SSE 连接中断，浏览器将自动重连');
    };
    for (var name in handlers) bindEvent(name);
    return eventSource;
  }

  function bindEvent(name) {
    eventSource.addEventListener(name, function (e) {
      var data;
      try { data = JSON.parse(e.data); } catch (err) { data = e.data; }
      var list = handlers[name] || [];
      for (var i = 0; i < list.length; i++) list[i](data, name);
    });
  }

  return {
    health: function () { return request('GET', '/api/health'); },
    send: function (message) { return request('POST', '/api/host/messages', message); },
    shutdown: function (message) {
      return request('POST', '/api/host/shutdown', message || {}, { keepalive: true });
    },
    subscribe: function (eventName, handler) {
      var isNew = !handlers[eventName];
      (handlers[eventName] = handlers[eventName] || []).push(handler);
      if (eventSource && isNew) bindEvent(eventName);
      ensureEventSource();
    }
  };
})();

function onLoadExtension() {
  console.log('[tdsviewerExtension] onLoadExtension called');

  // 先订阅推送：demo.exe 的「推送测试」按钮发 testEvent；demo.exe 退出时 LocalService 推 hostExited
  tdsHost.subscribe('ready', function () { console.log('[tdsviewerExtension] SSE 已连接'); });
  tdsHost.subscribe('testEvent', function (data) { console.log('[tdsviewerExtension] 收到推送 testEvent:', data); });
  tdsHost.subscribe('hostExited', function (data) { console.log('[tdsviewerExtension] demo.exe 已退出:', data); });

  var message = {
    type: 'onLoadExtension',
    from: 'GAMNAPIF',
    text: 'hello from GAMNAPIF onload'
  };

  tdsHost.send(message).then(function (result) {
    console.log('[tdsviewerExtension] demo.exe 回包:', result.reply);
  }).catch(function (err) {
    // 典型原因：LocalService 未启动 / Origin 不在白名单 / demo.exe 拉起失败
    console.error('[tdsviewerExtension] 发送失败（LocalService 是否已启动？）:', err);
  });
}

var isAddEvent = false;
function CurrentPageLeadToolExtension(viewername){
  var sendForm = document.getElementById("sendFormCurrentPage");
  if(sendForm == null){
    sendForm = document.createElement("form");
    sendForm.setAttribute("id","sendFormCurrentPage");
    sendForm.setAttribute("method","POST");
    document.body.appendChild(sendForm);
  }
  var viewerId = getParameterByName("viewerId");
  var targetname = viewerId + "_ExtensionCurrentPage";
  sendForm.target = targetname;
  sendForm.action = "GAMNDUMMYEDGE.html";
  window.open('', targetname, 'left=50000,top=50000,width=1,height=1,status=yes,location=no,resizable=yes');
  sendForm.submit();
  if(!isAddEvent){
    isAddEvent = true;
    window.addEventListener("message", function (event){
      parent.ldtF.document.getElementById("currentPage").value = event.data;
      parent.ldtF.document.getElementById("keyCode").value = viewername;
      parent.ldtF.document.getElementById("updCurrentPage").click();
    });
  }
}