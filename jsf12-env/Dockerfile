# ---------- 构建阶段：用 Maven + JDK8 编译打 war ----------
FROM maven:3.9-eclipse-temurin-8 AS build
WORKDIR /app
# 先拷 pom 预热依赖缓存（源码变更时不必重新下载依赖）
COPY pom.xml .
RUN mvn -B -q dependency:go-offline
COPY src ./src
RUN mvn -B -q package

# ---------- 运行阶段：Tomcat 9 + JRE8（javax 命名空间，适配 JSF 1.2）----------
# 注意：绝不能用 tomcat:10+，其切到 jakarta 命名空间会导致 JSF 1.2 启动失败。
FROM tomcat:9-jre8-temurin
# 清掉自带示例应用，把本应用部署为 ROOT（根路径访问）
RUN rm -rf /usr/local/tomcat/webapps/*
COPY --from=build /app/target/jsf12-demo.war /usr/local/tomcat/webapps/ROOT.war

# JPDA 远程调试：暴露 8000，供 IDE attach
# 注意：JRE 8 只支持纯端口号；"*:8000" 的写法是 JDK 9+ 才有，在 8 上会 gethostbyname 失败。
ENV JPDA_ADDRESS=8000
ENV JPDA_TRANSPORT=dt_socket
EXPOSE 8080 8000

# 以调试模式启动 Tomcat
CMD ["catalina.sh", "jpda", "run"]
