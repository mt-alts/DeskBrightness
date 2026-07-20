using DeskBrightness.Core.Brightness;
using DeskBrightness.Core.Filtering;
using DeskBrightness.Core.Profiles;
using DeskBrightness.Core.Sensors;

namespace DeskBrightness.Test;

public sealed class BrightnessCoreTests
{
    [Fact]
    public void MovingAverageLuxFilter_UsesOnlyConfiguredWindow()
    {
        var filter = new MovingAverageLuxFilter(windowSize: 3);

        Assert.Equal(10, filter.Add(10));
        Assert.Equal(15, filter.Add(20));
        Assert.Equal(20, filter.Add(30));
        Assert.Equal(30, filter.Add(40));
    }

    [Fact]
    public void ThresholdLuxFilter_AcceptsFirstSampleAndMeaningfulChanges()
    {
        var filter = new ThresholdLuxFilter(threshold: 5);

        Assert.True(filter.ShouldAccept(100));
        Assert.False(filter.ShouldAccept(104.99));
        Assert.True(filter.ShouldAccept(105));
        Assert.False(filter.ShouldAccept(109));
    }

    [Fact]
    public void HysteresisFilter_AppliesFirstLevelAndSkipsSmallBrightnessChanges()
    {
        var filter = new HysteresisFilter(stepThreshold: 5);

        Assert.True(filter.ShouldApply(BrightnessLevel.FromPercent(30)));
        Assert.False(filter.ShouldApply(BrightnessLevel.FromPercent(34)));
        Assert.True(filter.ShouldApply(BrightnessLevel.FromPercent(35)));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10, 3)]
    [InlineData(25, 10)]
    [InlineData(50, 20)]
    [InlineData(100, 35)]
    [InlineData(200, 50)]
    [InlineData(400, 65)]
    [InlineData(700, 78)]
    [InlineData(1200, 88)]
    [InlineData(2000, 95)]
    [InlineData(5000, 100)]
    public void DefaultLuxToBrightnessMapper_LogarithmicCurve(double lux, byte expectedPercent)
    {
        var mapper = new DefaultLuxToBrightnessMapper();

        var level = mapper.Map(lux, FullRangeProfile());

        Assert.Equal(expectedPercent, level.Percent);
    }

    [Fact]
    public void DefaultLuxToBrightnessMapper_ClampsToProfileBounds()
    {
        var mapper = new DefaultLuxToBrightnessMapper();
        var profile = new BrightnessProfile
        {
            MinimumBrightness = 40,
            MaximumBrightness = 70,
        };

        Assert.Equal(40, mapper.Map(0, profile).Percent);
        Assert.Equal(70, mapper.Map(5000, profile).Percent);
    }

    [Fact]
    public void BrightnessProfile_RejectsInvalidBounds()
    {
        var profile = new BrightnessProfile
        {
            MinimumBrightness = 90,
            MaximumBrightness = 15,
        };

        Assert.Throws<InvalidOperationException>(() => profile.Validate());
    }

    [Fact]
    public void BrightnessDecisionEngine_AppliesFirstAcceptedSample()
    {
        var engine = new BrightnessDecisionEngine(
            new DefaultLuxToBrightnessMapper(),
            new BrightnessProfile
            {
                MinimumBrightness = 0,
                MaximumBrightness = 100,
                SmoothingWindowSize = 1,
                MinimumApplyInterval = TimeSpan.Zero,
            }
        );

        var decision = engine.Evaluate(new LuxSample(100, DateTimeOffset.UtcNow));

        Assert.True(decision.ShouldApply);
        Assert.Equal((byte?)35, decision.TargetBrightness?.Percent);
        Assert.Equal(100, decision.SmoothedLux);
    }

    [Fact]
    public void BrightnessDecisionEngine_SkipsWhenLuxChangeIsBelowThreshold()
    {
        var engine = new BrightnessDecisionEngine(
            new DefaultLuxToBrightnessMapper(),
            new BrightnessProfile
            {
                MinimumBrightness = 0,
                MaximumBrightness = 100,
                LuxThreshold = 5,
                SmoothingWindowSize = 1,
                MinimumApplyInterval = TimeSpan.Zero,
            }
        );

        var now = DateTimeOffset.UtcNow;

        Assert.True(engine.Evaluate(new LuxSample(100, now)).ShouldApply);

        var decision = engine.Evaluate(new LuxSample(104, now.AddSeconds(10)));

        Assert.False(decision.ShouldApply);
        Assert.Equal("DecisionLuxBelowThreshold", decision.Reason);
    }

    [Fact]
    public void BrightnessDecisionEngine_SkipsWhenMinimumApplyIntervalHasNotElapsed()
    {
        var engine = new BrightnessDecisionEngine(
            new DefaultLuxToBrightnessMapper(),
            new BrightnessProfile
            {
                MinimumBrightness = 0,
                MaximumBrightness = 100,
                LuxThreshold = 0,
                BrightnessStepThreshold = 0,
                SmoothingWindowSize = 1,
                MinimumApplyInterval = TimeSpan.FromSeconds(10),
            }
        );

        var now = DateTimeOffset.UtcNow;

        Assert.True(engine.Evaluate(new LuxSample(10, now)).ShouldApply);

        var decision = engine.Evaluate(new LuxSample(100, now.AddSeconds(1)));

        Assert.False(decision.ShouldApply);
        Assert.Equal("DecisionMinIntervalNotElapsed", decision.Reason);
    }

    private static BrightnessProfile FullRangeProfile()
    {
        return new BrightnessProfile
        {
            MinimumBrightness = 0,
            MaximumBrightness = 100,
        };
    }
}
