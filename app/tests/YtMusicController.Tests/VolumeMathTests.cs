using YtMusicController.Core.Player;

namespace YtMusicController.Tests;

public sealed class VolumeMathTests
{
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(45, 45)]
    [InlineData(101, 100)]
    public void Clamp_is_bounded(int input, int expected) =>
        Assert.Equal(expected, VolumeMath.Clamp(input));

    [Theory]
    [InlineData(0, 0d)]
    [InlineData(45, 0.45d)]
    [InlineData(100, 1d)]
    public void Converts_percentage_to_media_fraction(int input, double expected) =>
        Assert.Equal(expected, VolumeMath.ToMediaFraction(input), 5);

    [Theory]
    [InlineData(0d, 0)]
    [InlineData(0.456d, 46)]
    [InlineData(1d, 100)]
    [InlineData(2d, 100)]
    public void Converts_media_fraction_to_percentage(double input, int expected) =>
        Assert.Equal(expected, VolumeMath.FromMediaFraction(input));

    [Fact]
    public void Rapid_encoder_updates_are_atomic_and_clamped()
    {
        var accumulator = new VolumeTargetAccumulator(50);
        Parallel.For(0, 200, _ => accumulator.ApplyDelta(5));
        Assert.Equal(100, accumulator.Current);
        Parallel.For(0, 200, _ => accumulator.ApplyDelta(-5));
        Assert.Equal(0, accumulator.Current);
    }
}
