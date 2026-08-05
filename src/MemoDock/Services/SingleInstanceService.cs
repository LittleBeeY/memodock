namespace MemoDock.Services;

/// <summary>确保应用单实例运行，并允许后续启动把已有窗口唤醒到前台。</summary>
public sealed class SingleInstanceService : IDisposable
{
    private const string MutexName = @"Local\MemoDock.SingleInstance";
    private const string ActivationEventName = @"Local\MemoDock.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activationEvent;
    private readonly bool _ownsMutex;
    private RegisteredWaitHandle? _activationWait;

    public SingleInstanceService()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out _ownsMutex);
        _activationEvent = new EventWaitHandle(
            initialState: false,
            EventResetMode.AutoReset,
            ActivationEventName);
    }

    /// <summary>当前进程是否持有单实例互斥体（即主实例）。</summary>
    public bool IsPrimary => _ownsMutex;

    /// <summary>
    /// 监听其他实例的激活请求。仅主实例可调用。
    /// </summary>
    /// <param name="onActivation">收到激活请求时的回调，在工作线程执行。</param>
    public void Listen(Action onActivation)
    {
        ArgumentNullException.ThrowIfNull(onActivation);

        if (!IsPrimary)
        {
            throw new InvalidOperationException("只有主实例可以监听激活请求。");
        }

        _activationWait = ThreadPool.RegisterWaitForSingleObject(
            _activationEvent,
            (_, timedOut) =>
            {
                if (!timedOut)
                {
                    onActivation();
                }
            },
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    /// <summary>向主实例发送激活信号。</summary>
    public void SignalPrimary()
    {
        _activationEvent.Set();
    }

    /// <summary>释放互斥体、事件与等待句柄。</summary>
    public void Dispose()
    {
        _activationWait?.Unregister(null);
        _activationEvent.Dispose();

        if (_ownsMutex)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
    }
}
