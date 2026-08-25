namespace YtMusicController.Core.Player;

public sealed class VolumeTargetAccumulator(int initialVolume = 100)
{
    private int _target = VolumeMath.Clamp(initialVolume);
    public int Current => Volatile.Read(ref _target);

    public int Set(int percentage)
    {
        var value = VolumeMath.Clamp(percentage);
        Interlocked.Exchange(ref _target, value);
        return value;
    }

    public int ApplyDelta(int delta)
    {
        while (true)
        {
            var current = Current;
            var next = VolumeMath.ApplyDelta(current, delta);
            if (Interlocked.CompareExchange(ref _target, next, current) == current)
                return next;
        }
    }
}
