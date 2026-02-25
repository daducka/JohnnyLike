using JohnnyLike.Narration;

namespace JohnnyLike.Narration.Tests;

public class TimeDilationControllerTests
{
    [Fact]
    public void Decide_AboveHighWatermark_Returns1x()
    {
        var ctrl = new TimeDilationController(lowWatermark: 5, highWatermark: 15, slowdownFactor: 0.7);
        Assert.Equal(1.0, ctrl.Decide(20.0));
    }

    [Fact]
    public void Decide_BelowLowWatermark_ReturnsSlowdown()
    {
        var ctrl = new TimeDilationController(lowWatermark: 5, highWatermark: 15, slowdownFactor: 0.7);
        Assert.Equal(0.7, ctrl.Decide(2.0));
    }

    [Fact]
    public void Decide_InHysteresisBand_RetainsPreviousFactor()
    {
        var ctrl = new TimeDilationController(lowWatermark: 5, highWatermark: 15, slowdownFactor: 0.7);

        // First push into slowdown territory
        ctrl.Decide(2.0);
        Assert.Equal(0.7, ctrl.CurrentFactor);

        // Now buffer is in band (5-15) — should stay at 0.7
        Assert.Equal(0.7, ctrl.Decide(8.0));
    }

    [Fact]
    public void Decide_RecoverToFull_AfterBufferFills()
    {
        var ctrl = new TimeDilationController(lowWatermark: 5, highWatermark: 15, slowdownFactor: 0.7);

        ctrl.Decide(1.0); // goes to 0.7x
        ctrl.Decide(20.0); // buffer healthy → back to 1.0x

        Assert.Equal(1.0, ctrl.CurrentFactor);
    }

    [Fact]
    public void Decide_InitialFactor_Is1x()
    {
        var ctrl = new TimeDilationController();
        Assert.Equal(1.0, ctrl.CurrentFactor);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(4.9)]
    public void Decide_VeryLowBuffer_ReturnsSlowdown(double buf)
    {
        var ctrl = new TimeDilationController(5, 15, 0.5);
        Assert.Equal(0.5, ctrl.Decide(buf));
    }
}
