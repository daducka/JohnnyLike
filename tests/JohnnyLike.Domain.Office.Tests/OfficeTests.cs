using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Office;

namespace JohnnyLike.Domain.Office.Tests;

public class ContentValidationTests
{
    [Fact]
    public void ValidateContent_ValidTemplates_ReturnsTrue()
    {
        var domainPack = new OfficeDomainPack();
        var result = domainPack.ValidateContent(out var errors);

        Assert.True(result);
        Assert.Empty(errors);
    }

    [Fact]
    public void GetSceneTemplates_ReturnsHighFiveTemplate()
    {
        var domainPack = new OfficeDomainPack();
        var templates = domainPack.GetSceneTemplates();

        Assert.NotEmpty(templates);
        Assert.Contains(templates, t => t.SceneType == "HIGHFIVE_PRINTER");
    }

    [Fact]
    public void HighFiveTemplate_HasTwoRoles()
    {
        var domainPack = new OfficeDomainPack();
        var template = domainPack.GetSceneTemplates()
            .First(t => t.SceneType == "HIGHFIVE_PRINTER");

        Assert.Equal(2, template.Roles.Count);
        Assert.Contains(template.Roles, r => r.RoleName == "initiator");
        Assert.Contains(template.Roles, r => r.RoleName == "receiver");
    }
}

public class ScoringTests
{
    [Fact]
    public void GenerateCandidates_HighHunger_PrioritizesEatSnack()
    {
        var domainPack = new OfficeDomainPack();
        var worldState = domainPack.CreateInitialWorldState();
        var actorState = domainPack.CreateActorState(new ActorId("Jim"), new Dictionary<string, object>
        {
            ["hunger"] = 80.0,
            ["energy"] = 50.0
        }) as OfficeActorState;

        var candidates = domainPack.GenerateCandidates(
            new ActorId("Jim"),
            actorState!,
            worldState,
            10.0,
            new Random(42)
        );

        var eatSnack = candidates.FirstOrDefault(c => c.Action.Id.Value.Contains("eat"));
        Assert.NotNull(eatSnack);
        Assert.True(eatSnack.Score > 0.8);
    }

    [Fact]
    public void GenerateCandidates_ChatRedeem_CreatesHighPriorityCandidate()
    {
        var domainPack = new OfficeDomainPack();
        var worldState = domainPack.CreateInitialWorldState();
        var actorState = domainPack.CreateActorState(new ActorId("Jim")) as OfficeActorState;
        actorState!.LastChatRedeem = "dance";

        var candidates = domainPack.GenerateCandidates(
            new ActorId("Jim"),
            actorState,
            worldState,
            10.0,
            new Random(42)
        );

        var chatRedeem = candidates.FirstOrDefault(c => c.Action.Id.Value.Contains("chat_redeem"));
        Assert.NotNull(chatRedeem);
        Assert.True(chatRedeem.Score > 1.0);
    }

    [Fact]
    public void ApplyActionEffects_EatSnack_ReducesHunger()
    {
        var domainPack = new OfficeDomainPack();
        var worldState = domainPack.CreateInitialWorldState();
        var actorState = domainPack.CreateActorState(new ActorId("Jim"), new Dictionary<string, object>
        {
            ["hunger"] = 80.0
        }) as OfficeActorState;

        var initialHunger = actorState!.Hunger;

        domainPack.ApplyActionEffects(
            new ActorId("Jim"),
            new ActionOutcome(
                new ActionId("eat_snack"),
                ActionOutcomeType.Success,
                10.0
            ),
            actorState,
            worldState
        );

        Assert.True(actorState.Hunger < initialHunger);
    }

    [Fact]
    public void ApplyActionEffects_ChatRedeem_ClearsRedeemAndIncreasesSocial()
    {
        var domainPack = new OfficeDomainPack();
        var worldState = domainPack.CreateInitialWorldState();
        var actorState = domainPack.CreateActorState(new ActorId("Jim")) as OfficeActorState;
        actorState!.LastChatRedeem = "dance";
        actorState.Social = 50.0;

        domainPack.ApplyActionEffects(
            new ActorId("Jim"),
            new ActionOutcome(
                new ActionId("chat_redeem_dance"),
                ActionOutcomeType.Success,
                2.0
            ),
            actorState,
            worldState
        );

        Assert.Null(actorState.LastChatRedeem);
        Assert.True(actorState.Social > 50.0);
    }
}
