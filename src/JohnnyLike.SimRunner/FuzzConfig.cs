namespace JohnnyLike.SimRunner;

public enum ResourceScarcityProfile
{
    Normal,
    Scarce,
    VeryScarce
}

public enum FuzzProfile
{
    Smoke,
    Extended,
    Nightly
}

public record FuzzConfig(
    int Seed,
    int SimulatedDurationSeconds,
    double DtSeconds,
    int NumActors,
    double EventRatePerMinute,
    double BurstProbability,
    double BurstMultiplier,
    double ActionDurationJitterPct,
    double TravelTimeJitterPct,
    double TaskFailureRate,
    double NoShowProbability,
    double BusyLockProbability,
    int JoinWindowMinSeconds,
    int JoinWindowMaxSeconds,
    ResourceScarcityProfile ResourceScarcityProfile,
    int MaxActorQueueLength,
    int MaxSceneLifetimeSeconds,
    int StarvationThresholdSeconds,
    int MaxAllowedReservationConflicts
)
{
    public static FuzzConfig Default => new(
        Seed: 42,
        SimulatedDurationSeconds: 300,
        DtSeconds: 0.1,
        NumActors: 4,
        EventRatePerMinute: 10.0,
        BurstProbability: 0.1,
        BurstMultiplier: 3.0,
        ActionDurationJitterPct: 20.0,
        TravelTimeJitterPct: 30.0,
        TaskFailureRate: 0.05,
        NoShowProbability: 0.02,
        BusyLockProbability: 0.01,
        JoinWindowMinSeconds: 5,
        JoinWindowMaxSeconds: 15,
        ResourceScarcityProfile: ResourceScarcityProfile.Normal,
        MaxActorQueueLength: 10,
        MaxSceneLifetimeSeconds: 60,
        StarvationThresholdSeconds: 120,
        MaxAllowedReservationConflicts: 0
    );

    public static FuzzConfig Smoke => new(
        Seed: 42,
        SimulatedDurationSeconds: 60,
        DtSeconds: 0.1,
        NumActors: 2,
        EventRatePerMinute: 8.0,
        BurstProbability: 0.05,
        BurstMultiplier: 2.0,
        ActionDurationJitterPct: 15.0,
        TravelTimeJitterPct: 20.0,
        TaskFailureRate: 0.02,
        NoShowProbability: 0.01,
        BusyLockProbability: 0.005,
        JoinWindowMinSeconds: 5,
        JoinWindowMaxSeconds: 12,
        ResourceScarcityProfile: ResourceScarcityProfile.Normal,
        MaxActorQueueLength: 8,
        MaxSceneLifetimeSeconds: 45,
        StarvationThresholdSeconds: 90,
        MaxAllowedReservationConflicts: 0
    );

    public static FuzzConfig Extended => new(
        Seed: 42,
        SimulatedDurationSeconds: 180,
        DtSeconds: 0.1,
        NumActors: 6,
        EventRatePerMinute: 12.0,
        BurstProbability: 0.12,
        BurstMultiplier: 3.5,
        ActionDurationJitterPct: 25.0,
        TravelTimeJitterPct: 35.0,
        TaskFailureRate: 0.07,
        NoShowProbability: 0.025,
        BusyLockProbability: 0.015,
        JoinWindowMinSeconds: 4,
        JoinWindowMaxSeconds: 14,
        ResourceScarcityProfile: ResourceScarcityProfile.Scarce,
        MaxActorQueueLength: 12,
        MaxSceneLifetimeSeconds: 70,
        StarvationThresholdSeconds: 100,
        MaxAllowedReservationConflicts: 0
    );

    public static FuzzConfig Nightly => new(
        Seed: 42,
        SimulatedDurationSeconds: 240,
        DtSeconds: 0.1,
        NumActors: 8,
        EventRatePerMinute: 15.0,
        BurstProbability: 0.15,
        BurstMultiplier: 4.0,
        ActionDurationJitterPct: 30.0,
        TravelTimeJitterPct: 40.0,
        TaskFailureRate: 0.10,
        NoShowProbability: 0.03,
        BusyLockProbability: 0.02,
        JoinWindowMinSeconds: 3,
        JoinWindowMaxSeconds: 15,
        ResourceScarcityProfile: ResourceScarcityProfile.VeryScarce,
        MaxActorQueueLength: 15,
        MaxSceneLifetimeSeconds: 80,
        StarvationThresholdSeconds: 120,
        MaxAllowedReservationConflicts: 0
    );

    public static FuzzConfig FromProfile(FuzzProfile profile, int seed)
    {
        return profile switch
        {
            FuzzProfile.Smoke => Smoke with { Seed = seed },
            FuzzProfile.Extended => Extended with { Seed = seed },
            FuzzProfile.Nightly => Nightly with { Seed = seed },
            _ => Default with { Seed = seed }
        };
    }
}
