using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Engine;

namespace JohnnyLike.Engine.Tests;

/// <summary>
/// Tests for action-level resource reservations
/// </summary>
public class ActionReservationTests
{
    [Fact]
    public void ActionWithResourceRequirements_ReservesResourcesDuringExecution()
    {
        // Arrange
        var domainPack = new TestActionReservationDomain();
        var traceSink = new InMemoryTraceSink();
        var engine = new JohnnyLike.Engine.Engine(domainPack, 42, traceSink);
        
        engine.AddActor(new ActorId("TestActor"));
        
        // Act - Get action that requires a resource
        var success = engine.TryGetNextAction(new ActorId("TestActor"), out var action);
        
        // Assert - Action was assigned
        Assert.True(success);
        Assert.NotNull(action);
        Assert.Equal("action_with_resource", action!.Id.Value);
        
        // Resource should be reserved during action execution
        var reservationTable = GetReservationTable(engine);
        Assert.True(reservationTable.IsReserved(new ResourceId("test:resource:1")));
    }
    
    [Fact]
    public void ActionCompletion_ReleasesReservedResources()
    {
        // Arrange
        var domainPack = new TestActionReservationDomain();
        var traceSink = new InMemoryTraceSink();
        var engine = new JohnnyLike.Engine.Engine(domainPack, 42, traceSink);
        
        engine.AddActor(new ActorId("TestActor"));
        
        // Act - Get and complete action
        var success = engine.TryGetNextAction(new ActorId("TestActor"), out var action);
        Assert.True(success);
        Assert.NotNull(action);
        
        var reservationTable = GetReservationTable(engine);
        Assert.True(reservationTable.IsReserved(new ResourceId("test:resource:1")));
        
        // Complete the action
        engine.ReportActionComplete(
            new ActorId("TestActor"),
            new ActionOutcome(action!.Id, ActionOutcomeType.Success, 10.0, null)
        );
        
        // Assert - Resource should be released
        Assert.False(reservationTable.IsReserved(new ResourceId("test:resource:1")));
    }
    
    [Fact]
    public void MultipleActors_CannotReserveSameResource()
    {
        // Arrange
        var domainPack = new TestActionReservationDomain();
        var traceSink = new InMemoryTraceSink();
        var engine = new JohnnyLike.Engine.Engine(domainPack, 42, traceSink);
        
        engine.AddActor(new ActorId("Actor1"));
        engine.AddActor(new ActorId("Actor2"));
        
        // Act - First actor gets the action with resource
        var success1 = engine.TryGetNextAction(new ActorId("Actor1"), out var action1);
        Assert.True(success1);
        Assert.NotNull(action1);
        Assert.Equal("action_with_resource", action1!.Id.Value);
        
        // Second actor tries to get same action - should fall back to action without resource
        var success2 = engine.TryGetNextAction(new ActorId("Actor2"), out var action2);
        Assert.True(success2);
        Assert.NotNull(action2);
        Assert.Equal("action_without_resource", action2!.Id.Value); // Fallback action
    }
    
    [Fact]
    public void ActionWithMultipleResources_ReservesAllOrNone()
    {
        // Arrange
        var domainPack = new TestMultiResourceDomain();
        var traceSink = new InMemoryTraceSink();
        var engine = new JohnnyLike.Engine.Engine(domainPack, 42, traceSink);
        
        engine.AddActor(new ActorId("TestActor"));
        
        // Act - Get action that requires multiple resources
        var success = engine.TryGetNextAction(new ActorId("TestActor"), out var action);
        
        // Assert
        Assert.True(success);
        Assert.NotNull(action);
        
        var reservationTable = GetReservationTable(engine);
        Assert.True(reservationTable.IsReserved(new ResourceId("test:resource:1")));
        Assert.True(reservationTable.IsReserved(new ResourceId("test:resource:2")));
    }
    
    [Fact]
    public void PartialReservationFailure_RollsBackAllReservations()
    {
        // Arrange
        var domainPack = new TestMultiResourceDomain();
        var traceSink = new InMemoryTraceSink();
        var engine = new JohnnyLike.Engine.Engine(domainPack, 42, traceSink);
        
        var reservationTable = GetReservationTable(engine);
        
        // Pre-reserve one of the resources externally
        reservationTable.TryReserve(
            new ResourceId("test:resource:2"),
            new SceneId("external"),
            new ActorId("External"),
            100.0
        );
        
        engine.AddActor(new ActorId("TestActor"));
        
        // Act - Try to get action that requires both resources
        var success = engine.TryGetNextAction(new ActorId("TestActor"), out var action);
        
        // Assert - Should fall back to action without resource requirements
        Assert.True(success);
        Assert.NotNull(action);
        Assert.Equal("action_without_resource", action!.Id.Value);
        
        // Resource 1 should not be reserved (rollback happened)
        Assert.False(reservationTable.IsReserved(new ResourceId("test:resource:1")));
        // Resource 2 is still reserved by external
        Assert.True(reservationTable.IsReserved(new ResourceId("test:resource:2")));
    }
    
    // Helper to access private ReservationTable field via reflection for testing
    private static ReservationTable GetReservationTable(JohnnyLike.Engine.Engine engine)
    {
        var directorField = typeof(JohnnyLike.Engine.Engine).GetField("_director", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var director = directorField!.GetValue(engine);
        
        var reservationsField = typeof(Director).GetField("_reservations", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (ReservationTable)reservationsField!.GetValue(director)!;
    }
    
    // Test domain pack that provides actions with resource requirements
    private class TestActionReservationDomain : IDomainPack
    {
        public string DomainName => "TestActionReservation";
        
        public WorldState CreateInitialWorldState() => new TestWorldState();
        
        public ActorState CreateActorState(ActorId actorId, Dictionary<string, object>? initialData = null)
        {
            return new TestActorState { Id = actorId };
        }
        
        public List<ActionCandidate> GenerateCandidates(
            ActorId actorId,
            ActorState actorState,
            WorldState worldState,
            double currentTime,
            Random rng,
            IResourceAvailability resourceAvailability)
        {
            var candidates = new List<ActionCandidate>();
            
            // Action that requires a resource
            if (!resourceAvailability.IsReserved(new ResourceId("test:resource:1")))
            {
                candidates.Add(new ActionCandidate(
                    new ActionSpec(
                        new ActionId("action_with_resource"),
                        ActionKind.Interact,
                        EmptyActionParameters.Instance,
                        10.0,
                        null,
                        new List<ResourceRequirement>
                        {
                            new ResourceRequirement(new ResourceId("test:resource:1"))
                        }
                    ),
                    1.0,
                    "Action with resource"
                ));
            }
            
            // Fallback action without resource
            candidates.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("action_without_resource"),
                    ActionKind.Wait,
                    EmptyActionParameters.Instance,
                    5.0
                ),
                0.5,
                "Action without resource"
            ));
            
            return candidates;
        }
        
        public void ApplyActionEffects(ActorId actorId, ActionOutcome outcome, ActorState actorState, WorldState worldState, IRngStream rng) { }
        public void OnSignal(Signal signal, ActorState? targetActor, WorldState worldState, double currentTime) { }
        public List<SceneTemplate> GetSceneTemplates() => new List<SceneTemplate>();
        public bool ValidateContent(out List<string> errors) { errors = new List<string>(); return true; }
        public Dictionary<string, object> GetActorStateSnapshot(ActorState actorState) => new Dictionary<string, object>();
    }
    
    // Test domain pack that provides actions requiring multiple resources
    private class TestMultiResourceDomain : IDomainPack
    {
        public string DomainName => "TestMultiResource";
        
        public WorldState CreateInitialWorldState() => new TestWorldState();
        
        public ActorState CreateActorState(ActorId actorId, Dictionary<string, object>? initialData = null)
        {
            return new TestActorState { Id = actorId };
        }
        
        public List<ActionCandidate> GenerateCandidates(
            ActorId actorId,
            ActorState actorState,
            WorldState worldState,
            double currentTime,
            Random rng,
            IResourceAvailability resourceAvailability)
        {
            var candidates = new List<ActionCandidate>();
            
            // Action that requires multiple resources
            if (!resourceAvailability.IsReserved(new ResourceId("test:resource:1")) &&
                !resourceAvailability.IsReserved(new ResourceId("test:resource:2")))
            {
                candidates.Add(new ActionCandidate(
                    new ActionSpec(
                        new ActionId("action_with_multiple_resources"),
                        ActionKind.Interact,
                        EmptyActionParameters.Instance,
                        10.0,
                        null,
                        new List<ResourceRequirement>
                        {
                            new ResourceRequirement(new ResourceId("test:resource:1")),
                            new ResourceRequirement(new ResourceId("test:resource:2"))
                        }
                    ),
                    1.0,
                    "Action with multiple resources"
                ));
            }
            
            // Fallback action without resource
            candidates.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("action_without_resource"),
                    ActionKind.Wait,
                    EmptyActionParameters.Instance,
                    5.0
                ),
                0.5,
                "Action without resource"
            ));
            
            return candidates;
        }
        
        public void ApplyActionEffects(ActorId actorId, ActionOutcome outcome, ActorState actorState, WorldState worldState, IRngStream rng) { }
        public void OnSignal(Signal signal, ActorState? targetActor, WorldState worldState, double currentTime) { }
        public List<SceneTemplate> GetSceneTemplates() => new List<SceneTemplate>();
        public bool ValidateContent(out List<string> errors) { errors = new List<string>(); return true; }
        public Dictionary<string, object> GetActorStateSnapshot(ActorState actorState) => new Dictionary<string, object>();
    }
    
    private class TestActorState : ActorState
    {
        public override string Serialize() => "{}";
        public override void Deserialize(string json) { }
    }
    
    private class TestWorldState : WorldState
    {
        public override string Serialize() => "{}";
        public override void Deserialize(string json) { }
    }
}
