using System.Collections.Concurrent;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace LocalService;

// SSE 订阅者管理：每条 /api/host/events 连接一个 Channel，事件广播到所有 Channel。
// 页面侧约定每个页面只建一条 EventSource（HTTP/1.1 同主机 6 连接上限，见 docs/webapi-migration.md §3.4）。
public sealed class HostEventHub : IDisposable
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    private readonly ConcurrentDictionary<Guid, Channel<SseItem<string>>> _subscribers = new();
    private readonly ILogger<HostEventHub> _logger;
    private readonly Timer _heartbeat;

    public HostEventHub(ILogger<HostEventHub> logger)
    {
        _logger = logger;
        // 心跳：防止连接被闲置断开，也让失效连接尽早写失败并被移除
        _heartbeat = new Timer(_ => Publish("ping", "{}"), null, HeartbeatInterval, HeartbeatInterval);
    }

    public int SubscriberCount => _subscribers.Count;

    // eventName 对应 SSE 的 event: 字段，dataJson 原样作为 data: 字段
    public void Publish(string eventName, string dataJson)
    {
        var item = new SseItem<string>(dataJson, eventName);
        foreach (var channel in _subscribers.Values)
        {
            // 有界 + DropOldest：慢订阅者不会拖住广播方
            channel.Writer.TryWrite(item);
        }
        if (eventName != "ping")
        {
            _logger.LogInformation("广播事件 {Event} 给 {Count} 个订阅者: {Data}", eventName, _subscribers.Count, dataJson);
        }
    }

    public async IAsyncEnumerable<SseItem<string>> Subscribe([EnumeratorCancellation] CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<SseItem<string>>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });
        _subscribers[id] = channel;
        _logger.LogInformation("SSE 订阅者接入 {Id}，当前 {Count} 个", id, _subscribers.Count);

        try
        {
            // 连上即发一条 ready，页面可据此确认订阅成功
            yield return new SseItem<string>("{}", "ready");

            while (true)
            {
                SseItem<string> item;
                try
                {
                    item = await channel.Reader.ReadAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    yield break;
                }
                yield return item;
            }
        }
        finally
        {
            _subscribers.TryRemove(id, out _);
            _logger.LogInformation("SSE 订阅者断开 {Id}，剩余 {Count} 个", id, _subscribers.Count);
        }
    }

    public void Dispose() => _heartbeat.Dispose();
}
