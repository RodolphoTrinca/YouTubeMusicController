using System.Threading;

namespace YtMusicController.App;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private const string MutexName = @"Local\YtMusicController.SingleInstance";
    private const string ActivationEventName = @"Local\YtMusicController.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle? _activationEvent;
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _listener;

    public SingleInstanceCoordinator()
    {
        _mutex = new Mutex(true, MutexName, out var createdNew);
        IsPrimary = createdNew;
        if (IsPrimary)
            _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName);
    }

    public bool IsPrimary { get; }

    public void Listen(Action onActivation)
    {
        if (!IsPrimary)
            throw new InvalidOperationException("Only the primary instance can listen for activation.");

        _listener = Task.Run(() =>
        {
            var handles = new WaitHandle[] { _activationEvent!, _cancellation.Token.WaitHandle };
            while (WaitHandle.WaitAny(handles) == 0)
                onActivation();
        });
    }

    public async Task SignalPrimaryAsync()
    {
        if (IsPrimary)
            return;

        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                using var activationEvent = EventWaitHandle.OpenExisting(ActivationEventName);
                activationEvent.Set();
                return;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                await Task.Delay(100);
            }
        }
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        _listener?.GetAwaiter().GetResult();
        _activationEvent?.Dispose();
        _cancellation.Dispose();
        if (IsPrimary)
            _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
