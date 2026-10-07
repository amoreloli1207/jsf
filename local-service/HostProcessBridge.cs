using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace LocalService;

// 接替原先 Edge 的角色：按需拉起 demo.exe，经重定向的 stdin/stdout 收发
// 「4 字节小端长度前缀 + UTF-8 JSON」帧（与 Native Messaging 帧格式相同，demo.exe 无需改协议）。
// stdout 上混有两类帧：带 id 的回包（交给等待中的请求）、type=="event" 的事件（交给 HostEventHub 广播）。
public sealed class HostProcessBridge : IDisposable
{
    private const int MaxFrameBytes = 64 * 1024 * 1024;

    // 不把中文转义成 \uXXXX，demo.exe 日志窗口可直接阅读（stdio 通道，不涉及 HTML 注入）
    private static readonly JsonSerializerOptions FrameJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly HostEventHub _hub;
    private readonly ILogger<HostProcessBridge> _logger;
    private readonly string _exePath;
    private readonly TimeSpan _replyTimeout;

    private readonly SemaphoreSlim _startLock = new(1, 1);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _pending = new();

    private readonly ChildProcessJob _job = new();
    private Process? _process;

    public HostProcessBridge(IConfiguration config, HostEventHub hub, ILogger<HostProcessBridge> logger)
    {
        _hub = hub;
        _logger = logger;

        // 相对路径以程序所在目录为基准（登录自启时当前目录不可预期）
        var configured = config["Host:ExePath"] ?? "demo.exe";
        _exePath = Path.GetFullPath(configured, AppContext.BaseDirectory);
        _replyTimeout = TimeSpan.FromMilliseconds(config.GetValue("Host:ReplyTimeoutMs", 5000));
    }

    public bool IsRunning => _process is { HasExited: false };

    // 发消息给 demo.exe 并等待回包；未运行则先拉起
    public async Task<JsonElement> SendAsync(JsonElement data, CancellationToken ct)
    {
        var process = await EnsureStartedAsync(ct);
        return await SendCoreAsync(process, data, ct);
    }

    // 仅在 demo.exe 运行时发送（用于 shutdown：没在运行就不必为了关闭而拉起它）
    public async Task<JsonElement?> SendIfRunningAsync(JsonElement data, CancellationToken ct)
    {
        var process = _process;
        if (process is null || process.HasExited) return null;
        return await SendCoreAsync(process, data, ct);
    }

    private async Task<JsonElement> SendCoreAsync(Process process, JsonElement data, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        try
        {
            // 沿用扩展方案的信封结构，demo.exe 的 IsShutdownMessage（payload.data.type）无需改动
            var envelope = new { id, type = "fromPage", payload = new { from = "webapi", data } };
            var payload = JsonSerializer.SerializeToUtf8Bytes(envelope, FrameJsonOptions);
            var len = BitConverter.GetBytes(payload.Length); // 小端

            await _writeLock.WaitAsync(ct);
            try
            {
                var stdin = process.StandardInput.BaseStream;
                await stdin.WriteAsync(len, ct);
                await stdin.WriteAsync(payload, ct);
                await stdin.FlushAsync(ct);
            }
            finally
            {
                _writeLock.Release();
            }
            _logger.LogInformation("→ demo.exe [{Len} 字节] {Json}", payload.Length, Encoding.UTF8.GetString(payload));

            return await tcs.Task.WaitAsync(_replyTimeout, ct);
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    private async Task<Process> EnsureStartedAsync(CancellationToken ct)
    {
        await _startLock.WaitAsync(ct);
        try
        {
            if (_process is { HasExited: false } running) return running;

            if (!File.Exists(_exePath))
                throw new FileNotFoundException($"找不到 demo.exe: {_exePath}");

            var psi = new ProcessStartInfo(_exePath)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                WorkingDirectory = Path.GetDirectoryName(_exePath)!
            };
            var process = Process.Start(psi) ?? throw new InvalidOperationException("Process.Start 返回 null");
            try
            {
                _job.Add(process);
            }
            catch (Exception ex)
            {
                // 加入 job 失败不影响通信，只是失去「强制结束时连带结束」的保障
                _logger.LogWarning(ex, "demo.exe 加入 Job Object 失败");
            }
            _process = process;
            _logger.LogInformation("已拉起 demo.exe (PID {Pid}): {Path}", process.Id, _exePath);

            _ = Task.Run(() => ReadLoop(process));
            return process;
        }
        finally
        {
            _startLock.Release();
        }
    }

    private void ReadLoop(Process process)
    {
        var stdout = process.StandardOutput.BaseStream;
        var lenBuf = new byte[4];
        try
        {
            while (true)
            {
                if (!ReadExact(stdout, lenBuf)) break; // EOF：进程退出

                int len = BitConverter.ToInt32(lenBuf, 0);
                if (len <= 0 || len > MaxFrameBytes)
                {
                    _logger.LogError("demo.exe 帧长度非法: {Len}，停止读取", len);
                    break;
                }

                var payload = new byte[len];
                if (!ReadExact(stdout, payload)) break;

                HandleFrame(payload);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取 demo.exe stdout 出错");
        }

        try { process.WaitForExit(2000); } catch { }
        int? exitCode = process.HasExited ? process.ExitCode : null;
        _logger.LogInformation("demo.exe (PID {Pid}) 已退出，退出码 {Code}", process.Id, exitCode);

        // 失败掉所有还在等回包的请求
        foreach (var (key, tcs) in _pending)
        {
            if (_pending.TryRemove(key, out _))
                tcs.TrySetException(new HostExitedException());
        }

        Interlocked.CompareExchange(ref _process, null, process);
        process.Dispose();

        _hub.Publish("hostExited", JsonSerializer.Serialize(new { exitCode }));
    }

    private void HandleFrame(byte[] payload)
    {
        string json = Encoding.UTF8.GetString(payload);
        _logger.LogInformation("← demo.exe [{Len} 字节] {Json}", payload.Length, json);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
            {
                if (_pending.TryRemove(idProp.GetString()!, out var tcs))
                    tcs.TrySetResult(root.Clone());
                else
                    _logger.LogWarning("收到无人等待的回包（可能已超时）: {Id}", idProp.GetString());
                return;
            }

            if (root.TryGetProperty("type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String
                && typeProp.GetString() == "event")
            {
                var eventName = root.TryGetProperty("event", out var e) && e.ValueKind == JsonValueKind.String
                    ? e.GetString()!
                    : "message";
                var data = root.TryGetProperty("data", out var d) ? d.GetRawText() : "null";
                _hub.Publish(eventName, data);
                return;
            }

            _logger.LogWarning("无法识别的帧，已丢弃: {Json}", json);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "demo.exe 帧不是合法 JSON，已丢弃");
        }
    }

    private static bool ReadExact(Stream s, byte[] buf)
    {
        int off = 0;
        while (off < buf.Length)
        {
            int n = s.Read(buf, off, buf.Length - off);
            if (n <= 0) return false; // EOF
            off += n;
        }
        return true;
    }

    // LocalService 正常停止时结束自己拉起的 demo.exe；强制结束的情况由 ChildProcessJob 兜底
    public void Dispose()
    {
        var process = _process;
        try
        {
            if (process is { HasExited: false })
            {
                _logger.LogInformation("LocalService 停止，结束 demo.exe (PID {Pid})", process.Id);
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "结束 demo.exe 失败");
        }
        _job.Dispose();
    }
}

public sealed class HostExitedException() : Exception("demo.exe 已退出");
