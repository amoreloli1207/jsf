# JSF 1.2 可运行环境（Docker）

一个能真正跑起来的最小 JSF 1.2 应用，用于验证 JSF 1.x（JSP 视图）技术栈。
本机不装任何 Java/Tomcat，全部在容器里完成。

## 技术栈

| 层 | 选型 | 说明 |
|---|---|---|
| JSF | Mojarra **1.2_15**（`javax.faces`）| JSF 1.2，JSP 作为视图技术；与 1.0/1.1 同族、行为几乎一致 |
| 视图 | **JSP**（`home.jsp`）| 用 `h:`/`f:` 标签库，正是 JSF 1.x 的典型形态 |
| 容器 | **Tomcat 9** + JRE 8 | `javax.*` 命名空间；**不可用 Tomcat 10+**（jakarta 命名空间会崩） |
| 构建 | Maven（在构建镜像内）| 本机无需装 Maven/JDK |

## 目录

```
jsf12-env/
├── Dockerfile               # 多阶段：Maven 编译 → Tomcat 运行
├── docker-compose.yml       # 映射 8080(应用) + 8000(调试)
├── pom.xml                  # JSF 1.2 依赖坐标
├── src/main/java/demo/HelloBean.java   # managed bean（断点打这里）
└── src/main/webapp/
    ├── index.jsp            # 欢迎页 → 重定向到 home.jsf
    ├── home.jsp             # JSF 视图（h:form / h:inputText / h:commandButton）
    └── WEB-INF/
        ├── web.xml          # FacesServlet 映射 *.jsf + ConfigureListener
        └── faces-config.xml # 注册 helloBean（session 作用域）
```

## 运行

在 `jsf12-env/` 目录下：

```bash
docker compose up --build
```

首次会拉镜像 + 下依赖，需几分钟。启动后浏览器打开：

```
http://localhost:8080/
```

会跳到 `home.jsf`，输入姓名点「打招呼」，页面显示由 managed bean 返回的问候语——即证明 JSF 1.2 完整跑通（组件渲染 + 表单回发 + EL + 托管 Bean）。

停止：`docker compose down`。

## 调试（Docker 完全支持）

容器以 `catalina.sh jpda run` 启动，暴露 **8000** 端口做 Java 远程调试（JPDA/JDWP）。

### VS Code
1. 安装扩展 **Extension Pack for Java**（含 Debugger for Java）。
2. 用 VS Code 打开 `jsf12-env/` 目录（已备好 `.vscode/launch.json`）。
3. `docker compose up --build` 先把容器跑起来。
4. 在 `HelloBean.sayHello()` 里点行号打断点。
5. 运行与调试面板选 **Attach to Tomcat (JSF12 Docker)** → 启动 attach。
6. 浏览器点「打招呼」→ 断点命中，可单步、看变量。

### IntelliJ IDEA
Run → Edit Configurations → 加 **Remote JVM Debug**，Host `localhost`、Port `8000`，Debugger mode `Attach`，先 `up` 再点调试即可。

## 备注

- 想改成**严格 JSF 1.0/1.1**：把 `pom.xml` 的两个 `javax.faces` 依赖换成对应老版本（1.1 有 `1.1_02` 等；1.0 中央仓基本缺失，需自备 jar）。API 与本示例高度一致，`home.jsp` / `HelloBean` 基本不用改。
- 想热改 Java 代码免重启：可挂载卷 + 用支持热替换的方式，或直接 `docker compose up --build` 重建（最省心）。
- 本目录与仓库根的「模拟 JSF 输出的静态 HTML」是两回事：那些是 PoC 的既定输入产物，这里才是真正由 JSF 渲染的环境。
```
