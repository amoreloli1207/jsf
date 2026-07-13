// javaframe.js
// business.html 中按钮点击后调用 openLtdWindow，openLtdWindow 再调用 openViewWindow。

function openLtdWindow() {
  console.log('[javaframe] openLtdWindow called');
  openViewWindow();
}

function openViewWindow() {
  console.log('[javaframe] openViewWindow called');
  // business.html 运行在 frameset 的 business 帧内，parent 即 javaroot 的 frameset 窗口；
  // 通过名字定位第二个帧 viewerltdframe，把内容加载进去。
  parent.frames['viewerltdframe'].location.href = 'viewerltdframe.html';
}

function shutdownLtd() {
  console.log('[javaframe] shutdownLtd called');
  // 不能直接读取跨源帧里的 shutdown 函数：file:// 下各帧 origin 为 "null" 互为跨源，
  // 读取自定义属性会抛 SecurityError。改用 postMessage 触发——postMessage 允许跨源调用，
  // 由 viewerMiddleware.js 在 viewerltdframe 帧内监听并执行其 shutdown()。
  var viewer = parent.frames['viewerltdframe'];
  if (viewer) {
    viewer.postMessage({ __viewerCommand: 'shutdown' }, '*');
  } else {
    console.warn('[javaframe] viewerltdframe 未找到，请先点「打开 Ltd 窗口」');
  }
}

function CurrentPage(viewername) {
  var objwin = getWindowObject (viewername +"_Master");
  if (objwin == null) {
    objwin = window.open("", viewername + "_Master", "left=50000, top=50000");
  }else{
    parent.javaframe.setWindowObject(viewername + "_Master", objwin);
  }
  if(objwin.ldtF != null) {
    objwin.imgF.CurrentPageLeadToolExtension(viewername);
  }else if (objwin.imgF != null){
    //通常Web版アプレツ卜版起動時の場合
  }else{
    //ビュ一ア起動前の場合
    objwin.close();
  }
}

function disableButton(obj){
  setTimeout(function(){
    var subimitId = obj.getAttribute("submitBtnId");
    var submitObj = document.getElementById(subimitId);
    var comfirm = obj.getAttribute("confirmflg");
    if (comfirm == "true") {
    }
    else{
      submitObj.click();
    }
  },10);
}

