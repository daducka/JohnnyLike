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
        SimulatedDurationSeconds: 7200,    // 2 story-hours; island actions take 8-80 min so actors complete several cycles
        DtSeconds: 10.0,                   // 10-second steps keep iteration count low while preserving tick accuracy
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
        StarvationThresholdSeconds: 120,   // only fires for idle actors (see FuzzRunner); 120 s idle is a real problem
        MaxAllowedReservationConflicts: 0
    );

    public static FuzzConfig Extended => new(
        Seed: 42,
        SimulatedDurationSeconds: 21600,   // 6 story-hours
        DtSeconds: 30.0,                   // 30-second steps
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
        StarvationThresholdSeconds: 120,
        MaxAllowedReservationConflicts: 0
    );

    public static FuzzConfig Nightly => new(
        Seed: 42,
        SimulatedDurationSeconds: 43200,   // 12 story-hours; enough for actors to complete many action cycles
        DtSeconds: 60.0,                   // 60-second steps: 720 iterations/run × 1 000 runs ≈ fast nightly run
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
        StarvationThresholdSeconds: 120,   // idle-only starvation check; 120 s without an action is a real deadlock
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
