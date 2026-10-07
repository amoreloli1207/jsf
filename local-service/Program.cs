using System.ComponentModel;
using System.Text.Json;
using LocalService;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

// LocalService：替代 Edge 扩展的本地桥。页面 fetch / EventSource 直连 127.0.0.1，
// 本服务按需拉起 demo.exe 并经 stdio 帧通信。设计见 docs/webapi-migration.md。

var logDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JsfHandler", "logs");
var fileLogger = new FileLoggerProvider(logDir);
var bootLog = fileLogger.CreateLogger("Program");

// 单实例：手动启动与登录自启同时存在时，后来者直接退出，避免抢端口
using var mutex = new Mutex(initiallyOwned: true, @"Local\JsfHandlerLocalService", out var createdNew);
if (!createdNew)
{
    bootLog.LogWarning("已有 LocalService 实例在运行，本进程退出");
    return 2;
}

// ContentRoot 固定为程序目录：登录自启时当前目录不可预期，否则找不到 appsettings.json
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});
builder.Logging.AddProvider(fileLogger);

// 生产白名单 + 开发期额外白名单（分两个键：配置数组按下标合并，同键会互相覆盖）
string[] allowedOrigins =
[
    .. builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [],
    .. builder.Configuration.GetSection("Cors:DevAllowedOrigins").Get<string[]>() ?? []
];
var allowedOriginSet = new HashSet<string>(allowedOrigins, StringComparer.OrdinalIgnoreCase);

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(allowedOrigins)
    .WithMethods("GET", "POST")
    .WithHeaders("Content-Type")));
builder.Services.AddSingleton<HostEventHub>();
builder.Services.AddSingleton<HostProcessBridge>();

var app = builder.Build();

// 服务端再校验一次 Origin：CORS 只限制浏览器读响应，挡不住「简单请求」被发出
app.Use(async (ctx, next) =>
{
    var origin = ctx.Request.Headers.Origin.ToString();
    if (origin.Length > 0 && !allowedOriginSet.Contains(origin))
    {
        app.Logger.LogWarning("拒绝非白名单 Origin: {Origin} {Method} {Path}", origin, ctx.Request.Method, ctx.Request.Path);
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }

    // 强制 JSON：跨站的简单表单 POST（text/plain、form-urlencoded）打不进来
    if (HttpMethods.IsPost(ctx.Request.Method) && !ctx.Request.HasJsonContentType())
    {
        ctx.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
        return;
    }

    // 兼容 Private Network Access 预检：浏览器询问是否允许访问本地网络时明确应答
    if (HttpMethods.IsOptions(ctx.Request.Method)
        && ctx.Request.Headers["Access-Control-Request-Private-Network"] == "true")
    {
        ctx.Response.Headers["Access-Control-Allow-Private-Network"] = "true";
    }

    await next();
});
app.UseCors();

var api = app.MapGroup("/api");

api.MapGet("/health", (HostProcessBridge bridge, HostEventHub hub) =>
    new { ok = true, hostRunning = bridge.IsRunning, subscribers = hub.SubscriberCount });

api.MapPost("/host/messages", (JsonElement body, HostProcessBridge bridge, CancellationToken ct) =>
    Forward(async () => await bridge.SendAsync(body, ct)));

api.MapPost("/host/shutdown", async (
    [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JsonElement? body,
    HostProcessBridge bridge,
    CancellationToken ct) =>
{
    // 保留页面传来的字段，type 强制为 shutdown（demo.exe 按 payload.data.type 判断）
    var data = body is { ValueKind: JsonValueKind.Object } b
        ? b.Deserialize<Dictionary<string, JsonElement>>()!
        : new Dictionary<string, JsonElement>();
    data["type"] = JsonSerializer.SerializeToElement("shutdown");
    var element = JsonSerializer.SerializeToElement(data);

    if (!bridge.IsRunning)
        return Results.Ok(new { ok = true, hostRunning = false });
    return await Forward(() => bridge.SendIfRunningAsync(element, ct));
});

api.MapGet("/host/events", (HostEventHub hub, CancellationToken ct) =>
    TypedResults.ServerSentEvents(hub.Subscribe(ct)));

try
{
    app.Logger.LogInformation("LocalService 启动，环境 {Env}，允许 Origin: {Origins}",
        app.Environment.EnvironmentName, string.Join(", ", allowedOrigins));
    await app.RunAsync();
    return 0;
}
catch (Exception ex)
{
    // 典型场景：端口被占用。记日志并以非 0 退出，不静默挂起
    bootLog.LogCritical(ex, "LocalService 启动或运行失败");
    return 1;
}

static async Task<IResult> Forward(Func<Task<JsonElement?>> send)
{
    try
    {
        var reply = await send();
        return Results.Ok(new { ok = true, reply });
    }
    catch (TimeoutException)
    {
        return Results.Json(new { ok = false, error = "等待 demo.exe 回包超时" }, statusCode: StatusCodes.Status504GatewayTimeout);
    }
    catch (Exception ex) when (ex is HostExitedException or FileNotFoundException or Win32Exception or InvalidOperationException)
    {
        return Results.Json(new { ok = false, error = ex.Message }, statusCode: StatusCodes.Status502BadGateway);
    }
}
