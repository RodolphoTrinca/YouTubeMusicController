namespace YtMusicController.Core.Player;

public static class VolumeMath
{
    public static int Clamp(int percentage) => Math.Clamp(percentage, 0, 100);
    public static double ToMediaFraction(int percentage) => Clamp(percentage) / 100d;
    public static int FromMediaFraction(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return 0;
        return Clamp((int)Math.Round(value * 100, MidpointRounding.AwayFromZero));
    }

    public static int ApplyDelta(int current, int delta) => Clamp(current + delta);
}
