using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Kit.Dice.Tests;

public class DiceTests
{
    private class FixedRngStream : IRngStream
    {
        private readonly Queue<int> _rolls;

        public FixedRngStream(params int[] rolls)
        {
            _rolls = new Queue<int>(rolls);
        }

        public int Next(int minValue, int maxValue)
        {
            return _rolls.Dequeue();
        }

        public double NextDouble()
        {
            return 0.5;
        }
    }

    [Fact]
    public void RollD20_ReturnsValueBetween1And20()
    {
        var rng = new RandomRngStream(new Random(42));
        var roll = Dice.RollD20(rng);
        Assert.InRange(roll, 1, 20);
    }

    [Fact]
    public void RollD20WithAdvantage_ReturnsBetterRoll()
    {
        var rng = new FixedRngStream(5, 15);
        var roll = Dice.RollD20WithAdvantage(rng);
        Assert.Equal(15, roll);
    }

    [Fact]
    public void RollD20WithDisadvantage_ReturnsWorseRoll()
    {
        var rng = new FixedRngStream(5, 15);
        var roll = Dice.RollD20WithDisadvantage(rng);
        Assert.Equal(5, roll);
    }

    [Fact]
    public void RollD20_IsDeterministicWithFixedSeed()
    {
        var rng1 = new RandomRngStream(new Random(42));
        var rng2 = new RandomRngStream(new Random(42));
        
        var roll1 = Dice.RollD20(rng1);
        var roll2 = Dice.RollD20(rng2);
        
        Assert.Equal(roll1, roll2);
    }
}

public class DndMathTests
{
    [Theory]
    [InlineData(10, 0)]
    [InlineData(11, 0)]
    [InlineData(12, 1)]
    [InlineData(14, 2)]
    [InlineData(16, 3)]
    [InlineData(18, 4)]
    [InlineData(20, 5)]
    [InlineData(9, -1)]
    [InlineData(8, -1)]
    [InlineData(7, -2)]
    [InlineData(6, -2)]
    public void AbilityModifier_CalculatesCorrectly(int stat, int expectedModifier)
    {
        var modifier = DndMath.AbilityModifier(stat);
        Assert.Equal(expectedModifier, modifier);
    }

    [Fact]
    public void EstimateSuccessChanceD20_AutoSuccessWhenDCTooLow()
    {
        var chance = DndMath.EstimateSuccessChanceD20(1, 5, AdvantageType.Normal);
        Assert.Equal(0.95, chance);
    }

    [Fact]
    public void EstimateSuccessChanceD20_RespectsCriticalFailureNat1()
    {
        // With a very low DC, you should still have a 5% chance of failing (nat 1)
        var chance = DndMath.EstimateSuccessChanceD20(1, 10, AdvantageType.Normal);
        Assert.Equal(0.95, chance);
    }

    [Fact]
    public void EstimateSuccessChanceD20_RespectsCriticalSuccessNat20()
    {
        // With a very high DC, you should still have a 5% chance of succeeding (nat 20)
        var chance = DndMath.EstimateSuccessChanceD20(25, 0, AdvantageType.Normal);
        Assert.Equal(0.05, chance);
    }

    [Fact]
    public void EstimateSuccessChanceD20_RespectsCriticalFailureWithAdvantage()
    {
        // Even with advantage, nat 1 on both rolls should be possible (0.05 * 0.05 = 0.0025 or 0.25%)
        var chance = DndMath.EstimateSuccessChanceD20(1, 10, AdvantageType.Advantage);
        Assert.True(chance <= 0.95, $"Expected chance <= 0.95, got {chance}");
    }

    [Fact]
    public void EstimateSuccessChanceD20_RespectsCriticalSuccessWithDisadvantage()
    {
        // Even with disadvantage, nat 20 on both rolls should be possible (0.05 * 0.05 = 0.0025 or 0.25%)
        var chance = DndMath.EstimateSuccessChanceD20(25, 0, AdvantageType.Disadvantage);
        Assert.True(chance >= 0.05, $"Expected chance >= 0.05, got {chance}");
    }

    [Fact]
    public void EstimateSuccessChanceD20_NormalRoll()
    {
        var chance = DndMath.EstimateSuccessChanceD20(10, 0, AdvantageType.Normal);
        Assert.Equal(0.55, chance, 2);
    }

    [Fact]
    public void EstimateSuccessChanceD20_WithAdvantage_IncreasesProbability()
    {
        var normalChance = DndMath.EstimateSuccessChanceD20(15, 0, AdvantageType.Normal);
        var advantageChance = DndMath.EstimateSuccessChanceD20(15, 0, AdvantageType.Advantage);
        
        Assert.True(advantageChance > normalChance);
    }

    [Fact]
    public void EstimateSuccessChanceD20_WithDisadvantage_DecreasesProbability()
    {
        var normalChance = DndMath.EstimateSuccessChanceD20(10, 0, AdvantageType.Normal);
        var disadvantageChance = DndMath.EstimateSuccessChanceD20(10, 0, AdvantageType.Disadvantage);
        
        Assert.True(disadvantageChance < normalChance);
    }
}

public class SkillCheckResolverTests
{
    private class FixedRngStream : IRngStream
    {
        private readonly int _roll;

        public FixedRngStream(int roll)
        {
            _roll = roll;
        }

        public int Next(int minValue, int maxValue)
        {
            return _roll;
        }

        public double NextDouble()
        {
            return 0.5;
        }
    }

    [Fact]
    public void Resolve_CriticalSuccessOnNat20()
    {
        var rng = new FixedRngStream(20);
        var request = new SkillCheckRequest(15, 0, AdvantageType.Normal, "TestSkill");
        
        var result = SkillCheckResolver.Resolve(rng, request);
        
        Assert.Equal(20, result.Roll);
        Assert.Equal(20, result.Total);
        Assert.Equal(RollOutcomeTier.CriticalSuccess, result.OutcomeTier);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Resolve_CriticalFailureOnNat1()
    {
        var rng = new FixedRngStream(1);
        var request = new SkillCheckRequest(5, 10, AdvantageType.Normal, "TestSkill");
        
        var result = SkillCheckResolver.Resolve(rng, request);
        
        Assert.Equal(1, result.Roll);
        Assert.Equal(11, result.Total);
        Assert.Equal(RollOutcomeTier.CriticalFailure, result.OutcomeTier);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Resolve_SuccessWhenTotalMeetsDC()
    {
        var rng = new FixedRngStream(10);
        var request = new SkillCheckRequest(15, 5, AdvantageType.Normal, "TestSkill");
        
        var result = SkillCheckResolver.Resolve(rng, request);
        
        Assert.Equal(10, result.Roll);
        Assert.Equal(15, result.Total);
        Assert.Equal(RollOutcomeTier.Success, result.OutcomeTier);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Resolve_CriticalSuccessWhenMarginIs5Plus()
    {
        var rng = new FixedRngStream(15);
        var request = new SkillCheckRequest(15, 5, AdvantageType.Normal, "TestSkill");
        
        var result = SkillCheckResolver.Resolve(rng, request);
        
        Assert.Equal(15, result.Roll);
        Assert.Equal(20, result.Total);
        Assert.Equal(RollOutcomeTier.CriticalSuccess, result.OutcomeTier);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Resolve_PartialSuccessWhenShortfallIsSmall()
    {
        var rng = new FixedRngStream(8);
        var request = new SkillCheckRequest(15, 5, AdvantageType.Normal, "TestSkill");
        
        var result = SkillCheckResolver.Resolve(rng, request);
        
        Assert.Equal(8, result.Roll);
        Assert.Equal(13, result.Total);
        Assert.Equal(RollOutcomeTier.PartialSuccess, result.OutcomeTier);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Resolve_FailureWhenShortfallIsLarge()
    {
        var rng = new FixedRngStream(5);
        var request = new SkillCheckRequest(15, 5, AdvantageType.Normal, "TestSkill");
        
        var result = SkillCheckResolver.Resolve(rng, request);
        
        Assert.Equal(5, result.Roll);
        Assert.Equal(10, result.Total);
        Assert.Equal(RollOutcomeTier.Failure, result.OutcomeTier);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Resolve_IsDeterministicWithSameSeed()
    {
        var rng1 = new RandomRngStream(new Random(42));
        var rng2 = new RandomRngStream(new Random(42));
        var request = new SkillCheckRequest(15, 3, AdvantageType.Normal, "TestSkill");
        
        var result1 = SkillCheckResolver.Resolve(rng1, request);
        var result2 = SkillCheckResolver.Resolve(rng2, request);
        
        Assert.Equal(result1.Roll, result2.Roll);
        Assert.Equal(result1.Total, result2.Total);
        Assert.Equal(result1.OutcomeTier, result2.OutcomeTier);
    }

    [Fact]
    public void Resolve_EstimatedSuccessChanceIsCalculated()
    {
        var rng = new FixedRngStream(10);
        var request = new SkillCheckRequest(15, 3, AdvantageType.Normal, "TestSkill");
        
        var result = SkillCheckResolver.Resolve(rng, request);
        
        Assert.True(result.EstimatedSuccessChance > 0.0);
        Assert.True(result.EstimatedSuccessChance <= 1.0);
    }
}
