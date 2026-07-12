<%--
  欢迎页：直接跳到经 FacesServlet 处理的 home.jsf。
  （若把 home.jsp 设为欢迎页会绕过 FacesServlet，JSF 标签不会被解析。）
--%>
<% response.sendRedirect(request.getContextPath() + "/home.jsf"); %>
