using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Supply;

namespace JohnnyLike.Domain.Island.Tests;

/// <summary>
/// Tests that verify the affordance migration: environment-derived action candidates
/// now come from world items rather than directly from the actor class.
/// </summary>
public class AffordanceMigrationTests
{
    private static readonly IslandDomainPack Domain = new();

    // ── Shared helpers ────────────────────────────────────────────────────────

    /// <summary>Creates an actor via the domain pack (has AlivenessBuff) in a healthy, playful state.</summary>
    private static HumanActorState MakeHealthyActor(string id = "TestActor")
    {
        var state = (HumanActorState)Domain.CreateActorState(new ActorId(id));
        state.Satiety = 80.0;
        state.Energy  = 80.0;
        state.Morale  = 60.0;
        state.Health  = 100.0;
        return state;
    }

    /// <summary>Creates an actor that is alive but fails PlayfulOnly (starving, low morale).</summary>
    private static HumanActorState MakeSurvivingActor(string id = "TestActor")
    {
        var state = (HumanActorState)Domain.CreateActorState(new ActorId(id));
        state.Satiety = 10.0;
        state.Energy  = 10.0;
        state.Morale  = 10.0;
        state.Health  = 100.0;
        return state;
    }

    private static List<ActionCandidate> GenerateCandidates(HumanActorState actor)
    {
        var world = (IslandWorldState)Domain.CreateInitialWorldState();
        return Domain.GenerateCandidates(actor.Id, actor, world, 0L, new Random(42), new EmptyResourceAvailability());
    }

    // ── build_sand_castle comes from BeachItem ────────────────────────────────

    [Fact]
    public void BuildSandCastle_ComeFromBeachItem_NotActor()
    {
        var actor = MakeHealthyActor();
        var candidates = GenerateCandidates(actor);

        var candidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "build_sand_castle");
        Assert.NotNull(candidate);

        // ProviderItemId must be the beach item's id, not the actor id
        Assert.Equal("beach", candidate.ProviderItemId);
    }

    [Fact]
    public void BuildSandCastle_IsAbsent_WhenActorIsNotPlayful()
    {
        // Starving actor does not meet PlayfulOnly requirement
        var actor = MakeSurvivingActor();
        var candidates = GenerateCandidates(actor);

        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "build_sand_castle");
    }

    // ── swim comes from OceanItem ─────────────────────────────────────────────

    [Fact]
    public void Swim_ComesFromOceanItem_NotActor()
    {
        var actor = MakeHealthyActor();
        var candidates = GenerateCandidates(actor);

        var candidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "swim");
        Assert.NotNull(candidate);

        // ProviderItemId must be the ocean item's id, not the actor id
        Assert.Equal("ocean", candidate.ProviderItemId);
    }

    /// <summary>
    /// Demonstrates actor-state-filtered affordances: the OceanItem only offers
    /// swim to actors that satisfy the PlayfulOnly requirement.
    /// </summary>
    [Fact]
    public void Swim_IsAbsent_WhenActorIsNotPlayful()
    {
        var actor = MakeSurvivingActor();
        var candidates = GenerateCandidates(actor);

        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "swim");
    }

    [Fact]
    public void Swim_IsPresent_WhenActorIsPlayful()
    {
        var actor = MakeHealthyActor();
        var candidates = GenerateCandidates(actor);

        Assert.Contains(candidates, c => c.Action.Id.Value == "swim");
    }

    // ── sleep_under_tree comes from CoconutTreeItem ───────────────────────────

    [Fact]
    public void SleepUnderTree_ComesFromCoconutTreeItem_NotActor()
    {
        var actor = MakeHealthyActor();
        var candidates = GenerateCandidates(actor);

        var candidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "sleep_under_tree");
        Assert.NotNull(candidate);

        // ProviderItemId must be the palm tree item's id, not the actor id
        Assert.Equal("palm_tree", candidate.ProviderItemId);
    }

    [Fact]
    public void SleepUnderTree_IsAvailable_ForSurvivingActor()
    {
        // sleep_under_tree uses AliveOnly — should be offered regardless of playfulness
        var actor = MakeSurvivingActor();
        var candidates = GenerateCandidates(actor);

        Assert.Contains(candidates, c => c.Action.Id.Value == "sleep_under_tree");
    }

    // ── think_about_supplies comes from SupplyPile ────────────────────────────

    [Fact]
    public void ThinkAboutSupplies_ComesFromSupplyPile_NotActor()
    {
        var actor = MakeHealthyActor();
        var candidates = GenerateCandidates(actor);

        var candidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "think_about_supplies");
        Assert.NotNull(candidate);

        // ProviderItemId must be the supply pile's id, not the actor id
        Assert.Equal("shared_supplies", candidate.ProviderItemId);
    }

    // ── ChatCandidateProvider ─────────────────────────────────────────────────

    [Fact]
    public void ClapEmote_ComesFromActorProvider_WithPendingIntent()
    {
        var actor = MakeHealthyActor("TestActor");
        actor.PendingChatActions.Enqueue(new PendingIntent
        {
            ActionId = "clap_emote",
            Type     = "sub",
            Data     = new Dictionary<string, object>(),
            EnqueuedAtTick = 0L
        });

        var candidates = GenerateCandidates(actor);

        var candidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "clap_emote");
        Assert.NotNull(candidate);
        // Chat candidates use the actor's id as ProviderItemId
        Assert.Equal("TestActor", candidate.ProviderItemId);
    }

    [Fact]
    public void ClapEmote_IsAbsent_WhenSurvivalCritical()
    {
        var actor = MakeSurvivingActor(); // Satiety=10, Energy=10 → survival critical
        actor.PendingChatActions.Enqueue(new PendingIntent
        {
            ActionId = "clap_emote",
            Type     = "sub",
            Data     = new Dictionary<string, object>(),
            EnqueuedAtTick = 0L
        });

        var candidates = GenerateCandidates(actor);

        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "clap_emote");
    }

    // ── Actor no longer owns environment candidates ───────────────────────────

    [Fact]
    public void Actor_DoesNotDirectlyProvide_EnvironmentCandidates()
    {
        // Verify that none of the migrated actions use the actor's own ID as provider.
        var actor = MakeHealthyActor("TestActor");
        var candidates = GenerateCandidates(actor);

        var actorId = "TestActor";
        var environmentIds = new[] { "build_sand_castle", "swim", "sleep_under_tree", "think_about_supplies" };

        foreach (var actionId in environmentIds)
        {
            var match = candidates.FirstOrDefault(c => c.Action.Id.Value == actionId && c.ProviderItemId == actorId);
            Assert.Null(match);
        }
    }
}

