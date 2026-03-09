namespace JohnnyLike.Domain.Abstractions;

/// <summary>
/// Optional structured debug sink passed to
/// <see cref="IDomainPack.OrderCandidatesForSelection(ActorId,ActorState,WorldState,long,System.Collections.Generic.IReadOnlyList{ActionCandidate},System.Random,CandidateOrderingDebugSink?)"/>.
/// Domains can write ordering-branch details here without changing the return type.
/// The engine reads this after the call to build structured trace events.
/// </summary>
public class CandidateOrderingDebugSink
{
    /// <summary>"exploit" when the pragmatic best-first branch was taken; "explore" for softmax sampling.</summary>
    public string? OrderingBranch { get; set; }

    /// <summary>The actor's DecisionPragmatism value at decision time.</summary>
    public double? DecisionPragmatism { get; set; }

    /// <summary>1.0 - DecisionPragmatism; only meaningful for the explore branch.</summary>
    public double? Spontaneity { get; set; }

    /// <summary>Softmax temperature used during sampling; only set for the explore branch.</summary>
    public double? Temperature { get; set; }

    /// <summary>ActionId of the top candidate in the deterministic sorted input.</summary>
    public string? OriginalTopActionId { get; set; }

    /// <summary>ActionId of the first candidate in the resulting attempt order.</summary>
    public string? ChosenActionId { get; set; }

    /// <summary>1-based rank of the first attempt candidate in the original deterministic sort.</summary>
    public int? ChosenOriginalRank { get; set; }

    /// <summary>
    /// Per-candidate softmax weights and normalized probabilities in original sorted order.
    /// Only populated by domains that support verbose tracing.
    /// </summary>
    public IReadOnlyList<SoftmaxWeightEntry>? SoftmaxWeightDetails { get; set; }
}

/// <summary>Softmax weight detail for a single candidate, used in verbose ordering traces.</summary>
/// <param name="ActionId">The action ID.</param>
/// <param name="ProviderItemId">The provider item ID (may be null).</param>
/// <param name="Weight">Raw (unnormalized) softmax weight.</param>
/// <param name="Probability">Normalized probability (0..1).</param>
public record SoftmaxWeightEntry(string ActionId, string? ProviderItemId, double Weight, double Probability);
