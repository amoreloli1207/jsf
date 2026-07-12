<%@ page contentType="text/html; charset=UTF-8" pageEncoding="UTF-8" %>
<%@ taglib prefix="f" uri="http://java.sun.com/jsf/core" %>
<%@ taglib prefix="h" uri="http://java.sun.com/jsf/html" %>
<f:view>
    <html>
    <head>
        <title>JSF 1.2 Demo</title>
        <meta http-equiv="Content-Type" content="text/html; charset=UTF-8"/>
        <style>
            body { font-family: sans-serif; margin: 40px; }
            .msg { margin-top: 16px; color: #0a7; font-weight: bold; }
        </style>
    </head>
    <body>
        <h2>JSF 1.2 最小示例</h2>
        <h:form>
            <h:outputLabel value="姓名：" for="name"/>
            <h:inputText id="name" value="#{helloBean.name}"/>
            <h:commandButton value="打招呼" action="#{helloBean.sayHello}"/>
            <div class="msg">
                <h:outputText value="#{helloBean.message}"/>
            </div>
        </h:form>
    </body>
    </html>
</f:view>
