namespace JohnnyLike.SimRunner;

public enum ResourceScarcityProfile
{
    Normal,
    Scarce,
    VeryScarce
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
}
