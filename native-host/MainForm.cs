using System.Text;
using System.Text.Json;

namespace DemoHost;

// Demo native messaging host。
// 由 LocalService 按需拉起（过渡期也兼容 Edge connectNative），stdin/stdout 通信：
// 每条消息 = 4 字节小端长度前缀 + UTF-8 JSON。
// 回包带回请求里的 id 供 LocalService 关联；主动推送发 {type:"event", event, data}（无 id）。
// 注意：stdout 是协议通道，绝不能往里打日志——日志只进窗口里的文本框。
public class MainForm : Form
{
    private readonly TextBox _log;
    private readonly CancellationTokenSource _cts = new();
    private Stream? _stdout;
    private readonly object _stdoutLock = new();
    private int _pushSeq;

    // 不序列化 null：扩展模式下请求无 id，回包里也就不出现 "id":null；
    // 不把中文转义成 \uXXXX，便于日志阅读（stdout 是协议通道，不涉及 HTML）
    private static readonly JsonSerializerOptions SendJsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public MainForm()
    {
        Text = "Demo Native Host (demo.exe)";
        Width = 760;
        Height = 480;
        StartPosition = FormStartPosition.CenterScreen;

        _log = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 9f),
            BackColor = Color.Black,
            ForeColor = Color.Lime
        };

        var clearButton = new Button
        {
            Text = "清空",
            AutoSize = true,
            Margin = new Padding(4)
        };
        clearButton.Click += (_, _) => _log.Clear();

        // 验证 exe → LocalService → 页面（SSE）主动推送链路
        var pushButton = new Button
        {
            Text = "推送测试",
            AutoSize = true,
            Margin = new Padding(4)
        };
        pushButton.Click += (_, _) => PushEvent("testEvent", new
        {
            seq = Interlocked.Increment(ref _pushSeq),
            text = "hello from demo.exe",
            time = DateTime.Now.ToString("HH:mm:ss.fff")
        });

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(2)
        };
        toolbar.Controls.Add(clearButton);
        toolbar.Controls.Add(pushButton);

        // 用 TableLayoutPanel 保证布局确定：第 0 行放工具栏（自适应高度），第 1 行日志区填满
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.Controls.Add(toolbar, 0, 0);
        layout.Controls.Add(_log, 0, 1);

        Controls.Add(layout);

        Load += (_, _) => StartReadLoop();
        FormClosing += (_, _) => _cts.Cancel();
    }

    private void StartReadLoop()
    {
        AppendLog("Demo native host 启动，等待上游（LocalService / 扩展）的消息...");
        Task.Run(() => ReadLoop(_cts.Token));
    }

    private void ReadLoop(CancellationToken token)
    {
        try
        {
            using var stdin = Console.OpenStandardInput();
            var lenBuf = new byte[4];

            while (!token.IsCancellationRequested)
            {
                if (!ReadExact(stdin, lenBuf, 4))
                {
                    AppendLog("[stdin] 到达流末尾（上游断开，或未由 LocalService / 浏览器启动）。");
                    break;
                }

                int len = BitConverter.ToInt32(lenBuf, 0); // Windows 上为小端
                if (len <= 0 || len > 64 * 1024 * 1024)
                {
                    AppendLog($"[stdin] 非法消息长度: {len}");
                    break;
                }

                var payload = new byte[len];
                if (!ReadExact(stdin, payload, len))
                {
                    AppendLog("[stdin] 读取消息体时流中断。");
                    break;
                }

                string json = Encoding.UTF8.GetString(payload);
                AppendLog($"[收到 {len} 字节] {json}");

                string? id = GetRequestId(json);

                if (IsShutdownMessage(json))
                {
                    AppendLog("[shutdown] 收到关闭请求，宿主即将退出。");
                    SendMessage(new { id, ok = true, shutdown = true });
                    BeginInvoke(new Action(Close));
                    break;
                }

                // 回送一个 ack（可选），同样的帧格式
                SendMessage(new { id, ok = true, echo = json });
            }
        }
        catch (Exception ex)
        {
            AppendLog("[error] " + ex.Message);
        }
    }

    private static bool ReadExact(Stream s, byte[] buf, int count)
    {
        int off = 0;
        while (off < count)
        {
            int n = s.Read(buf, off, count - off);
            if (n <= 0) return false; // EOF
            off += n;
        }
        return true;
    }

    // 取请求的关联 id（LocalService 发来的消息带顶层 id；扩展发来的没有，返回 null）
    private static string? GetRequestId(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                return id.GetString();
        }
        catch
        {
            // 解析失败按无 id 处理
        }
        return null;
    }

    // 主动推送事件。复用 SendMessage 的 _stdoutLock，保证不与回包帧交错
    private void PushEvent(string eventName, object data)
    {
        SendMessage(new { type = "event", @event = eventName, data });
    }

    // 判断是否为 shutdown 请求。消息结构（LocalService / background 包装后）：
    // { [id], type:"fromPage", payload:{ from:"webapi"|"content_script", data:{ type:"shutdown", ... } } }
    private static bool IsShutdownMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("payload", out var payload)
                && payload.TryGetProperty("data", out var data)
                && data.TryGetProperty("type", out var type)
                && type.ValueKind == JsonValueKind.String
                && type.GetString() == "shutdown")
            {
                return true;
            }
        }
        catch
        {
            // 解析失败按非 shutdown 处理
        }
        return false;
    }

    private void SendMessage(object obj)
    {
        try
        {
            string json = JsonSerializer.Serialize(obj, SendJsonOptions);
            byte[] payload = Encoding.UTF8.GetBytes(json);
            byte[] len = BitConverter.GetBytes(payload.Length); // 小端

            lock (_stdoutLock)
            {
                _stdout ??= Console.OpenStandardOutput();
                _stdout.Write(len, 0, 4);
                _stdout.Write(payload, 0, payload.Length);
                _stdout.Flush();
            }

            AppendLog($"[回送 {payload.Length} 字节] {json}");
        }
        catch (Exception ex)
        {
            AppendLog("[error/send] " + ex.Message);
        }
    }

    private void AppendLog(string line)
    {
        if (_log.InvokeRequired)
        {
            _log.BeginInvoke(new Action(() => AppendLog(line)));
            return;
        }
        _log.AppendText($"{DateTime.Now:HH:mm:ss.fff}  {line}{Environment.NewLine}");
        _log.SelectionStart = _log.TextLength; // 始终把光标移到末尾并滚动到底
        _log.ScrollToCaret();
    }
}
