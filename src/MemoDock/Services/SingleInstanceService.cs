namespace MemoDock.Services;

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

    public bool IsPrimary => _ownsMutex;

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

    public void SignalPrimary()
    {
        _activationEvent.Set();
    }

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
