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
      parent.ldtF.document.getElementByld("currentPage").value = event.data;
      parent.ldtF.document.getElementByld("keyCode").value = viewername;
      parent.ldtF.document.getElementByld("updCurrentPage").click();
    });
  }
}