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
