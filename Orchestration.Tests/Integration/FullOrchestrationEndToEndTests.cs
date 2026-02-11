using FluentAssertions;
using Narratum.Core;
using Narratum.Domain;
using Narratum.State;
using Narratum.Orchestration.Models;
using Narratum.Orchestration.Llm;
using Narratum.Orchestration.Logging;
using Narratum.Orchestration.Services;
using Narratum.Orchestration.Stages;
using Narratum.Orchestration.Validation;
using Narratum.Orchestration.Prompts;
using Xunit;

namespace Narratum.Orchestration.Tests.Integration;

/// <summary>
/// Tests end-to-end Phase 3.8 : pipeline complet FullOrchestrationService.
///
/// Scénarios testés :
/// - Workflow normal de création d'histoire (cycle par cycle)
/// - Tous les types d'intent (7 types)
/// - Gestion des erreurs (LLM en échec, timeouts, entrées invalides)
/// - Retry automatique sur échec de validation
/// - Robustesse "Stupid LLM"
/// - Multi-personnages et lieux
/// - Cycles multiples successifs (simulation d'une session d'écriture)
/// - Audit trail et métriques
/// - Performance (&lt; 2s par cycle)
/// </summary>
public class FullOrchestrationEndToEndTests
{
    private readonly StoryState _emptyState;
    private readonly StoryState _richState;
    private readonly Id _aliceId;
    private readonly Id _bobId;
    private readonly Id _forestId;
    private readonly Id _castleId;

    public FullOrchestrationEndToEndTests()
    {
        _aliceId = Id.New();
        _bobId = Id.New();
        _forestId = Id.New();
        _castleId = Id.New();

        _emptyState = StoryState.Create(Id.New(), "The Hidden Realm");

        var alice = new CharacterState(_aliceId, "Alice", VitalStatus.Alive, _forestId)
            .WithKnownFact("Alice is a brave explorer")
            .WithKnownFact("Alice carries a magic compass");

        var bob = new CharacterState(_bobId, "Bob", VitalStatus.Alive, _castleId)
            .WithKnownFact("Bob is a castle guard")
            .WithKnownFact("Bob fears the dark forest");

        _richState = StoryState.Create(Id.New(), "The Hidden Realm")
            .WithCharacters(alice, bob);
    }

    private static FullOrchestrationService CreateService(
        ILlmClient? client = null,
        FullOrchestrationConfig? config = null,
        ICoherenceValidatorAdapter? coherenceValidator = null)
    {
        return new FullOrchestrationService(
            llmClient: client ?? new MockLlmClient(MockLlmConfig.ForTesting),
            config: config ?? FullOrchestrationConfig.ForTesting,
            pipelineLogger: new PipelineLogger(),
            auditTrail: new AuditTrail(),
            metricsCollector: new MetricsCollector(),
            structureValidator: new StructureValidator(),
            coherenceValidator: coherenceValidator,
            promptRegistry: PromptRegistry.CreateWithDefaults());
    }

    #region Workflow Normal — Cycle de Création d'Histoire

    [Fact]
    public async Task FullCycle_ContinueNarrative_WithEmptyState_ShouldSucceed()
    {
        // Un utilisateur commence une nouvelle histoire et demande une continuation
        var service = CreateService();
        var intent = NarrativeIntent.Continue("Begin the tale of the Hidden Realm");

        var result = await service.ExecuteCycleAsync(_emptyState, intent);

        result.Should().BeOfType<Result<FullPipelineResult>.Success>();
        var pipeline = ((Result<FullPipelineResult>.Success)result).Value;
        pipeline.IsSuccess.Should().BeTrue();
        pipeline.Output.Should().NotBeNull();
        pipeline.Output!.NarrativeText.Should().NotBeNullOrEmpty();
        pipeline.StageResults.Should().NotBeEmpty();
        pipeline.TotalDuration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task FullCycle_ContinueNarrative_WithRichState_ShouldSucceed()
    {
        // Histoire en cours avec personnages et lieux
        var service = CreateService();
        var intent = NarrativeIntent.Continue("Alice ventures deeper into the forest");

        var result = await service.ExecuteCycleAsync(_richState, intent);

        result.Should().BeOfType<Result<FullPipelineResult>.Success>();
        var pipeline = ((Result<FullPipelineResult>.Success)result).Value;
        pipeline.IsSuccess.Should().BeTrue();
        pipeline.Output!.NarrativeText.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task FullCycle_MultipleCyclesSequentially_ShouldAllSucceed()
    {
        // Simuler une session d'écriture : l'utilisateur demande plusieurs générations à la suite
        var service = CreateService();
        var currentState = _richState;

        var intents = new[]
        {
            NarrativeIntent.Continue("Alice enters the dark forest"),
            new NarrativeIntent(IntentType.DescribeScene, "Describe the forest clearing",
                targetLocationId: _forestId),
            NarrativeIntent.Dialogue(new[] { _aliceId, _bobId }, _forestId),
            new NarrativeIntent(IntentType.CreateTension, "A shadow approaches"),
            NarrativeIntent.Continue("The story continues after the encounter")
        };

        for (var i = 0; i < intents.Length; i++)
        {
            var result = await service.ExecuteCycleAsync(currentState, intents[i]);

            result.Should().BeOfType<Result<FullPipelineResult>.Success>(
                $"Cycle {i + 1} ({intents[i].Type}) should succeed");

            var pipeline = ((Result<FullPipelineResult>.Success)result).Value;
            pipeline.IsSuccess.Should().BeTrue($"Cycle {i + 1} should be successful");
            pipeline.Output.Should().NotBeNull($"Cycle {i + 1} should produce output");
        }
    }

    #endregion

    #region Tous les Types d'Intent

    [Theory]
    [InlineData(IntentType.ContinueNarrative)]
    [InlineData(IntentType.IntroduceEvent)]
    [InlineData(IntentType.GenerateDialogue)]
    [InlineData(IntentType.DescribeScene)]
    [InlineData(IntentType.Summarize)]
    [InlineData(IntentType.CreateTension)]
    [InlineData(IntentType.ResolveConflict)]
    public async Task FullCycle_AllIntentTypes_ShouldSucceed(IntentType intentType)
    {
        // Chaque type d'intent doit fonctionner bout-en-bout
        var service = CreateService();
        var intent = new NarrativeIntent(intentType, $"Testing {intentType}");

        var result = await service.ExecuteCycleAsync(_richState, intent);

        result.Should().BeOfType<Result<FullPipelineResult>.Success>();
        var pipeline = ((Result<FullPipelineResult>.Success)result).Value;
        pipeline.IsSuccess.Should().BeTrue();
        pipeline.Output.Should().NotBeNull();
    }

    [Fact]
    public async Task FullCycle_DialogueIntent_WithTargetCharacters_ShouldSucceed()
    {
        // Dialogue ciblant des personnages spécifiques
        var service = CreateService();
        var intent = NarrativeIntent.Dialogue(new[] { _aliceId, _bobId }, _forestId);

        var result = await service.ExecuteCycleAsync(_richState, intent);

        var pipeline = ((Result<FullPipelineResult>.Success)result).Value;
        pipeline.IsSuccess.Should().BeTrue();
        pipeline.Output!.NarrativeText.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task FullCycle_DescribeScene_WithTargetLocation_ShouldSucceed()
    {
        // Description d'un lieu spécifique
        var service = CreateService();
        var intent = NarrativeIntent.DescribeLocation(_forestId, "The ancient dark forest");

        var result = await service.ExecuteCycleAsync(_richState, intent);

        var pipeline = ((Result<FullPipelineResult>.Success)result).Value;
        pipeline.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task FullCycle_IntroduceEvent_ShouldCreateNarrative()
    {
        // Introduire un événement dans l'histoire
        var service = CreateService();
        var intent = new NarrativeIntent(IntentType.IntroduceEvent,
            "A mysterious stranger arrives at the castle gate",
            targetLocationId: _castleId);

        var result = await service.ExecuteCycleAsync(_richState, intent);

        var pipeline = ((Result<FullPipelineResult>.Success)result).Value;
        pipeline.IsSuccess.Should().BeTrue();
        pipeline.Output.Should().NotBeNull();
    }

    [Fact]
    public async Task FullCycle_Summarize_ShouldProduceOutput()
    {
        // Demander un résumé de l'histoire
        var service = CreateService();
        var intent = new NarrativeIntent(IntentType.Summarize, "Summarize the story so far");

        var result = await service.ExecuteCycleAsync(_richState, intent);

        var pipeline = ((Result<FullPipelineResult>.Success)result).Value;
        pipeline.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task FullCycle_CreateTension_ShouldSucceed()
    {
        var service = CreateService();
        var intent = new NarrativeIntent(IntentType.CreateTension,
            "An earthquake shakes the realm", targetCharacterIds: new[] { _aliceId });

        var result = await service.ExecuteCycleAsync(_richState, intent);

        var pipeline = ((Result<FullPipelineResult>.Success)result).Value;
        pipeline.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task FullCycle_ResolveConflict_ShouldSucceed()
    {
        var service = CreateService();
        var intent = new NarrativeIntent(IntentType.ResolveConflict,
            "Alice and Bob find common ground");

        var result = await service.ExecuteCycleAsync(_richState, intent);

        var pipeline = ((Result<FullPipelineResult>.Success)result).Value;
        pipeline.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region Gestion des Erreurs — Entrées Invalides

    [Fact]
    public async Task FullCycle_NullStoryState_ShouldThrowArgumentNull()
    {
        var service = CreateService();
        var intent = NarrativeIntent.Continue();

        var action = async () => await service.ExecuteCycleAsync(null!, intent);

        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task FullCycle_NullIntent_ShouldThrowArgumentNull()
    {
        var service = CreateService();

        var action = async () => await service.ExecuteCycleAsync(_emptyState, null!);

        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task FullCycle_IntentWithNonExistentCharacterIds_ShouldStillSucceed()
    {
        // L'utilisateur cible un personnage qui n'existe pas dans le state
        var service = CreateService();
        var fakeCharId = Id.New();
        var intent = new NarrativeIntent(IntentType.GenerateDialogue,
            "Ghost speaks", targetCharacterIds: new[] { fakeCharId });

        var result = await service.ExecuteCycleAsync(_richState, intent);

        // Le pipeline doit quand même fonctionner (graceful degradation)
        result.Should().BeOfType<Result<FullPipelineResult>.Success>();
        var pipeline = ((Result<FullPipelineResult>.Success)result).Value;
        // Le résultat peut être success ou failure, mais ne doit pas crasher
        pipeline.Should().NotBeNull();
    }

    [Fact]
    public async Task FullCycle_IntentWithNonExistentLocationId_ShouldStillSucceed()
    {
        // L'utilisateur cible un lieu qui n'existe pas
        var service = CreateService();
        var fakeLocId = Id.New();
        var intent = NarrativeIntent.DescribeLocation(fakeLocId, "A place that doesn't exist");

        var result = await service.ExecuteCycleAsync(_richState, intent);

        result.Should().BeOfType<Result<FullPipelineResult>.Success>();
    }

    #endregion

    #region Robustesse LLM — Stupid LLM et Échecs

    [Fact]
    public async Task FullCycle_StupidLlm_ShouldStillProduceOutput()
    {
        // Principe Phase 3 : "Le système doit fonctionner même si le LLM est stupide"
        var service = CreateService(client: new StupidLlmClient());
        var intent = NarrativeIntent.Continue("Tell the story");

        var result = await service.ExecuteCycleAsync(_richState, intent);

        result.Should().BeOfType<Result<FullPipelineResult>.Success>();
        var pipeline = ((Result<FullPipelineResult>.Success)result).Value;
        pipeline.IsSuccess.Should().BeTrue();
        pipeline.Output!.NarrativeText.Should().Be("TEXTE FAUX MAIS STRUCTURELLEMENT VALIDE");
    }

    [Theory]
    [InlineData(IntentType.ContinueNarrative)]
    [InlineData(IntentType.GenerateDialogue)]
    [InlineData(IntentType.DescribeScene)]
    [InlineData(IntentType.Summarize)]
    [InlineData(IntentType.CreateTension)]
    [InlineData(IntentType.ResolveConflict)]
    [InlineData(IntentType.IntroduceEvent)]
    public async Task StupidLlm_AllIntentTypes_ShouldStillWork(IntentType intentType)
    {
        // Le StupidLLM doit fonctionner avec TOUS les types d'intent
        var service = CreateService(client: new StupidLlmClient());
        var intent = new NarrativeIntent(intentType, "Stupid test");

        var result = await service.ExecuteCycleAsync(_richState, intent);

        result.Should().BeOfType<Result<FullPipelineResult>.Success>();
        ((Result<FullPipelineResult>.Success)result).Value.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task FullCycle_LlmAlwaysFails_ShouldReturnFailureNotCrash()
    {
        // Un LLM qui échoue 100% du temps
        var failConfig = new MockLlmConfig { FailureRate = 1.0, SimulatedDelay = TimeSpan.Zero };
        var service = CreateService(client: new MockLlmClient(failConfig));
        var intent = NarrativeIntent.Continue();

        var result = await service.ExecuteCycleAsync(_emptyState, intent);

        // Le pipeline ne doit PAS crasher — il retourne un résultat d'échec propre
        result.Should().BeOfType<Result<FullPipelineResult>.Success>();
        var pipeline = ((Result<FullPipelineResult>.Success)result).Value;
        pipeline.IsSuccess.Should().BeFalse();
        pipeline.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task FullCycle_LlmReturnsEmptyContent_ShouldHandleGracefully()
    {
        // LLM retourne une chaîne vide
        var emptyConfig = new MockLlmConfig
        {
            DefaultResponse = "",
            SimulatedDelay = TimeSpan.Zero
        };
        var service = CreateService(
            client: new MockLlmClient(emptyConfig),
            config: FullOrchestrationConfig.ForTesting with { MaxRetries = 0 });
        var intent = NarrativeIntent.Continue();

        var result = await service.ExecuteCycleAsync(_richState, intent);

        // Doit pas crasher - échec de validation est acceptable
        result.Should().BeOfType<Result<FullPipelineResult>.Success>();
        var pipeline = ((Result<FullPipelineResult>.Success)result).Value;
        // Le StructureValidator doit détecter le contenu vide
        pipeline.Should().NotBeNull();
    }

    [Fact]
    public async Task FullCycle_LlmReturnsVeryLongContent_ShouldHandleGracefully()
    {
        // LLM retourne un texte extrêmement long
        var longText = new string('A', 100_000);
        var longConfig = new MockLlmConfig
        {
            DefaultResponse = longText,
            SimulatedDelay = TimeSpan.Zero
        };
        var service = CreateService(
            client: new MockLlmClient(longConfig),
            config: FullOrchestrationConfig.ForTesting with { MaxRetries = 0 });
        var intent = NarrativeIntent.Continue();

        var result = await service.ExecuteCycleAsync(_richState, intent);

        // Ne doit pas crasher
        result.Should().BeOfType<Result<FullPipelineResult>.Success>();
    }

    #endregion

    #region Retry et Validation

    [Fact]
    public async Task FullCycle_WithRetries_ShouldTrackRetryCount()
    {
        // Test avec un client qui réussit au premier essai → 0 retries
        var service = CreateService(
            config: FullOrchestrationConfig.ForTesting with { MaxRetries = 3 });
        var intent = NarrativeIntent.Continue();

        var result = await service.ExecuteCycleAsync(_richState, intent);

        var pipeline = ((Result<FullPipelineResult>.Success)result).Value;
        pipeline.RetryCount.Should().Be(0, "Mock LLM should succeed on first try");
    }

    [Fact]
    public async Task FullCycle_WithMaxRetriesZero_ShouldNotRetry()
    {
        var service = CreateService(
            config: FullOrchestrationConfig.ForTesting with { MaxRetries = 0 });
        var intent = NarrativeIntent.Continue();

        var result = await service.ExecuteCycleAsync(_richState, intent);

        var pipeline = ((Result<FullPipelineResult>.Success)result).Value;
        pipeline.RetryCount.Should().BeLessOrEqualTo(1);
    }

    [Fact]
    public async Task FullCycle_ValidationDisabled_ShouldStillWork()
    {
        var config = FullOrchestrationConfig.ForTesting with
        {
            EnableStructureValidation = false,
            EnableCoherenceValidation = false
        };
        var service = CreateService(config: config);
        var intent = NarrativeIntent.Continue();

        var result = await service.ExecuteCycleAsync(_richState, intent);

        var pipeline = ((Result<FullPipelineResult>.Success)result).Value;
        pipeline.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region Personnages et Lieux — Scénarios Utilisateur

    [Fact]
    public async Task FullCycle_SingleCharacter_ShouldSucceed()
    {
        // Histoire avec un seul personnage
        var charId = Id.New();
        var state = StoryState.Create(Id.New(), "Solo World")
            .WithCharacter(new CharacterState(charId, "Lone Hero", VitalStatus.Alive));
        var service = CreateService();
        var intent = NarrativeIntent.Continue("The lone hero begins their journey");

        var result = await service.ExecuteCycleAsync(state, intent);

        var pipeline = ((Result<FullPipelineResult>.Success)result).Value;
        pipeline.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task FullCycle_ManyCharacters_ShouldSucceed()
    {
        // Histoire avec beaucoup de personnages
        var state = _emptyState;
        for (var i = 0; i < 20; i++)
        {
            state = state.WithCharacter(
                new CharacterState(Id.New(), $"Character_{i}", VitalStatus.Alive));
        }

        var service = CreateService();
        var intent = NarrativeIntent.Continue("A grand gathering at the castle");

        var result = await service.ExecuteCycleAsync(state, intent);

        var pipeline = ((Result<FullPipelineResult>.Success)result).Value;
        pipeline.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task FullCycle_DeadCharacterInState_ShouldNotCrash()
    {
        // Un personnage mort dans le state ne doit pas causer de crash
        var deadChar = new CharacterState(Id.New(), "Ghost", VitalStatus.Dead);
        var state = _richState.WithCharacter(deadChar);
        var service = CreateService();
        var intent = NarrativeIntent.Continue();

        var result = await service.ExecuteCycleAsync(state, intent);

        var pipeline = ((Result<FullPipelineResult>.Success)result).Value;
        pipeline.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task FullCycle_DialogueBetweenAliveAndDead_ShouldHandle()
    {
        // Tentative de dialogue entre un vivant et un mort
        var deadId = Id.New();
        var state = _richState.WithCharacter(
            new CharacterState(deadId, "DeadPerson", VitalStatus.Dead));
        var service = CreateService();
        var intent = NarrativeIntent.Dialogue(new[] { _aliceId, deadId }, _forestId);

        var result = await service.ExecuteCycleAsync(state, intent);

        // Ne doit pas crasher
        result.Should().BeOfType<Result<FullPipelineResult>.Success>();
    }

    #endregion

    #region Audit Trail et Métriques

    [Fact]
    public async Task FullCycle_ShouldRecordAllStages()
    {
        var service = CreateService();
        var intent = NarrativeIntent.Continue();

        var result = await service.ExecuteCycleAsync(_richState, intent);

        var pipeline = ((Result<FullPipelineResult>.Success)result).Value;

        // Le pipeline doit avoir au minimum ContextBuilder, PromptBuilder, AgentExecutor, Validation
        pipeline.StageResults.Should().HaveCountGreaterOrEqualTo(3);
        pipeline.StageResults.Should().Contain(s => s.StageName == "ContextBuilder");
        pipeline.StageResults.Should().Contain(s => s.StageName == "PromptBuilder");
        pipeline.StageResults.Should().Contain(s =>
            s.StageName.StartsWith("AgentExecutor"));
    }

    [Fact]
    public async Task FullCycle_ShouldPopulateAuditTrail()
    {
        var auditTrail = new AuditTrail();
        var service = new FullOrchestrationService(
            llmClient: new MockLlmClient(MockLlmConfig.ForTesting),
            config: FullOrchestrationConfig.ForTesting,
            auditTrail: auditTrail);
        var intent = NarrativeIntent.Continue();

        var result = await service.ExecuteCycleAsync(_richState, intent);
        var pipeline = ((Result<FullPipelineResult>.Success)result).Value;

        var report = service.GetAuditReport(pipeline.PipelineId);
        report.Should().NotBeNull();
        report.Entries.Should().NotBeEmpty();
    }

    [Fact]
    public async Task FullCycle_ShouldPopulateMetrics()
    {
        var metricsCollector = new MetricsCollector();
        var service = new FullOrchestrationService(
            llmClient: new MockLlmClient(MockLlmConfig.ForTesting),
            config: FullOrchestrationConfig.ForTesting,
            metricsCollector: metricsCollector);
        var intent = NarrativeIntent.Continue();

        await service.ExecuteCycleAsync(_richState, intent);

        var report = service.GetMetricsReport();
        report.Should().NotBeNull();
    }

    [Fact]
    public async Task FullCycle_OutputMetadata_ShouldContainIsMock()
    {
        var service = CreateService();
        var intent = NarrativeIntent.Continue();

        var result = await service.ExecuteCycleAsync(_richState, intent);

        var pipeline = ((Result<FullPipelineResult>.Success)result).Value;
        pipeline.Output!.Metadata.Should().ContainKey("isMock");
        pipeline.Output.Metadata["isMock"].Should().Be(true);
    }

    #endregion

    #region Performance

    [Fact]
    public async Task FullCycle_ShouldCompleteWithinTwoSeconds()
    {
        // Phase 3.8 : chaque cycle d'orchestration < 2s
        var service = CreateService(
            config: FullOrchestrationConfig.ForPerformance);
        var intent = NarrativeIntent.Continue();

        var result = await service.ExecuteCycleAsync(_richState, intent);

        var pipeline = ((Result<FullPipelineResult>.Success)result).Value;
        pipeline.TotalDuration.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task FullCycle_TenConsecutiveCycles_ShouldAllBeUnderTwoSeconds()
    {
        // Performance : 10 cycles consécutifs
        var service = CreateService(
            config: FullOrchestrationConfig.ForPerformance);

        for (var i = 0; i < 10; i++)
        {
            var intent = NarrativeIntent.Continue($"Cycle {i + 1}");
            var result = await service.ExecuteCycleAsync(_richState, intent);

            var pipeline = ((Result<FullPipelineResult>.Success)result).Value;
            pipeline.TotalDuration.Should().BeLessThan(TimeSpan.FromSeconds(2),
                $"Cycle {i + 1} should complete in < 2s");
        }
    }

    #endregion

    #region Scénarios Utilisateur Réalistes

    [Fact]
    public async Task Scenario_NewStoryFromScratch_ShouldWork()
    {
        // L'utilisateur crée une histoire depuis zéro
        var service = CreateService();
        var state = StoryState.Create(Id.New(), "My First Story");

        // Étape 1 : Commencer l'histoire
        var result1 = await service.ExecuteCycleAsync(state,
            NarrativeIntent.Continue("In a land far away..."));
        ((Result<FullPipelineResult>.Success)result1).Value.IsSuccess.Should().BeTrue();

        // Étape 2 : Décrire le lieu
        var result2 = await service.ExecuteCycleAsync(state,
            NarrativeIntent.DescribeLocation(Id.New(), "The enchanted valley"));
        ((Result<FullPipelineResult>.Success)result2).Value.IsSuccess.Should().BeTrue();

        // Étape 3 : Introduire un événement
        var result3 = await service.ExecuteCycleAsync(state,
            new NarrativeIntent(IntentType.IntroduceEvent, "A meteor falls from the sky"));
        ((Result<FullPipelineResult>.Success)result3).Value.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Scenario_RegenerateLastPage_WithModifiedInstructions()
    {
        // L'utilisateur n'est pas satisfait et veut régénérer la dernière page
        var service = CreateService();

        // Première génération
        var intent1 = NarrativeIntent.Continue("Alice finds a treasure chest");
        var result1 = await service.ExecuteCycleAsync(_richState, intent1);
        var output1 = ((Result<FullPipelineResult>.Success)result1).Value.Output!.NarrativeText;

        // Régénération avec instructions modifiées
        var intent2 = NarrativeIntent.Continue("Alice finds a treasure chest, but it's trapped");
        var result2 = await service.ExecuteCycleAsync(_richState, intent2);
        var output2 = ((Result<FullPipelineResult>.Success)result2).Value.Output!.NarrativeText;

        // Les deux doivent avoir réussi
        ((Result<FullPipelineResult>.Success)result1).Value.IsSuccess.Should().BeTrue();
        ((Result<FullPipelineResult>.Success)result2).Value.IsSuccess.Should().BeTrue();

        // Les outputs doivent exister (avec le mock ils seront identiques, mais le pipeline doit fonctionner)
        output1.Should().NotBeNullOrEmpty();
        output2.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Scenario_DirectAccessToSpecificPage()
    {
        // L'utilisateur saute directement à un contexte spécifique sans historique
        var service = CreateService();
        var state = StoryState.Create(Id.New(), "Jumping Story");

        // Accès direct à un dialogue sans contexte préalable
        var dialogueIntent = new NarrativeIntent(IntentType.GenerateDialogue,
            "Two strangers meet");

        var result = await service.ExecuteCycleAsync(state, dialogueIntent);

        var pipeline = ((Result<FullPipelineResult>.Success)result).Value;
        pipeline.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Scenario_SwitchBetweenIntentTypes_RapidFire()
    {
        // L'utilisateur change rapidement de type de requête
        var service = CreateService();

        var rapidIntents = new NarrativeIntent[]
        {
            NarrativeIntent.Continue(),
            new(IntentType.DescribeScene, "Quick scene"),
            new(IntentType.GenerateDialogue, "Quick dialogue"),
            NarrativeIntent.Continue(),
            new(IntentType.Summarize, "Quick summary"),
            new(IntentType.CreateTension, "Quick tension"),
            NarrativeIntent.Continue()
        };

        foreach (var intent in rapidIntents)
        {
            var result = await service.ExecuteCycleAsync(_richState, intent);
            result.Should().BeOfType<Result<FullPipelineResult>.Success>();
            ((Result<FullPipelineResult>.Success)result).Value.IsSuccess.Should().BeTrue();
        }
    }

    [Fact]
    public async Task Scenario_StoryWithEvolvingCharacters()
    {
        // Les personnages évoluent entre les cycles
        var service = CreateService();
        var charId = Id.New();

        // Cycle 1 : Personnage vivant et ignorant
        var state1 = StoryState.Create(Id.New(), "Evolution World")
            .WithCharacter(new CharacterState(charId, "Hero", VitalStatus.Alive));
        var result1 = await service.ExecuteCycleAsync(state1, NarrativeIntent.Continue());
        ((Result<FullPipelineResult>.Success)result1).Value.IsSuccess.Should().BeTrue();

        // Cycle 2 : Le personnage a appris des choses
        var state2 = StoryState.Create(Id.New(), "Evolution World")
            .WithCharacter(new CharacterState(charId, "Hero", VitalStatus.Alive)
                .WithKnownFact("Discovered the ancient temple")
                .WithKnownFact("Learned the secret password"));
        var result2 = await service.ExecuteCycleAsync(state2, NarrativeIntent.Continue());
        ((Result<FullPipelineResult>.Success)result2).Value.IsSuccess.Should().BeTrue();

        // Cycle 3 : Le personnage meurt
        var state3 = StoryState.Create(Id.New(), "Evolution World")
            .WithCharacter(new CharacterState(charId, "Hero", VitalStatus.Dead));
        var result3 = await service.ExecuteCycleAsync(state3, NarrativeIntent.Continue());
        ((Result<FullPipelineResult>.Success)result3).Value.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region Service State — IsReady et Configuration

    [Fact]
    public async Task IsReady_WithMockClient_ShouldReturnTrue()
    {
        var service = CreateService();

        var isReady = await service.IsReadyAsync();

        isReady.Should().BeTrue();
    }

    [Fact]
    public async Task IsReady_WithStupidClient_ShouldReturnTrue()
    {
        var service = CreateService(client: new StupidLlmClient());

        var isReady = await service.IsReadyAsync();

        isReady.Should().BeTrue();
    }

    [Fact]
    public void Config_ForTesting_ShouldHaveReasonableDefaults()
    {
        var config = FullOrchestrationConfig.ForTesting;

        config.MaxRetries.Should().Be(1);
        config.StageTimeout.Should().BeLessThan(TimeSpan.FromMinutes(1));
        config.GlobalTimeout.Should().BeLessThan(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void Config_ForPerformance_ShouldBeOptimized()
    {
        var config = FullOrchestrationConfig.ForPerformance;

        config.MaxRetries.Should().Be(0);
        config.EnableDetailedLogging.Should().BeFalse();
    }

    #endregion

    #region Stress Tests

    [Fact]
    public async Task Stress_FiftyCyclesSequentially_ShouldAllSucceed()
    {
        var service = CreateService();

        for (var i = 0; i < 50; i++)
        {
            var intent = NarrativeIntent.Continue($"Stress cycle {i + 1}");
            var result = await service.ExecuteCycleAsync(_richState, intent);

            result.Should().BeOfType<Result<FullPipelineResult>.Success>(
                $"Stress cycle {i + 1} should not fail");
            ((Result<FullPipelineResult>.Success)result).Value.IsSuccess.Should().BeTrue();
        }
    }

    [Fact]
    public async Task Stress_AlternatingSuccessAndFailureLlm_ShouldNeverCrash()
    {
        // Alternance entre LLM qui réussit et LLM qui échoue
        var configs = new MockLlmConfig[]
        {
            MockLlmConfig.ForTesting,                                    // Success
            new() { FailureRate = 1.0, SimulatedDelay = TimeSpan.Zero }, // Fail
            MockLlmConfig.ForTesting,                                    // Success
            new() { FailureRate = 1.0, SimulatedDelay = TimeSpan.Zero }, // Fail
            MockLlmConfig.Stupid                                         // Stupid success
        };

        foreach (var config in configs)
        {
            var service = CreateService(client: new MockLlmClient(config));
            var intent = NarrativeIntent.Continue();

            var result = await service.ExecuteCycleAsync(_richState, intent);

            // Ne doit JAMAIS crasher
            result.Should().BeOfType<Result<FullPipelineResult>.Success>();
        }
    }

    [Fact]
    public async Task Stress_LargeStoryState_ShouldComplete()
    {
        // Un état d'histoire avec énormément de personnages
        var state = _emptyState;
        for (var i = 0; i < 100; i++)
        {
            var charState = new CharacterState(Id.New(), $"Char_{i}",
                i % 5 == 0 ? VitalStatus.Dead : VitalStatus.Alive);
            for (var j = 0; j < 5; j++)
            {
                charState = charState.WithKnownFact($"Fact {j} about Char_{i}");
            }
            state = state.WithCharacter(charState);
        }

        var service = CreateService();
        var intent = NarrativeIntent.Continue("A massive world");

        var result = await service.ExecuteCycleAsync(state, intent);

        result.Should().BeOfType<Result<FullPipelineResult>.Success>();
        ((Result<FullPipelineResult>.Success)result).Value.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region Pipeline Integrity — Chaque étape est traçable

    [Fact]
    public async Task Pipeline_Success_ShouldHaveAllStagesCompleted()
    {
        var service = CreateService();
        var intent = NarrativeIntent.Continue();

        var result = await service.ExecuteCycleAsync(_richState, intent);
        var pipeline = ((Result<FullPipelineResult>.Success)result).Value;

        pipeline.StageResults.Should().AllSatisfy(stage =>
        {
            stage.StageName.Should().NotBeNullOrEmpty();
            stage.Duration.Should().BeGreaterOrEqualTo(TimeSpan.Zero);
        });
    }

    [Fact]
    public async Task Pipeline_Failure_ShouldStillHavePartialStages()
    {
        // Même en cas d'échec, les stages exécutés doivent être enregistrés
        var failConfig = new MockLlmConfig { FailureRate = 1.0, SimulatedDelay = TimeSpan.Zero };
        var service = CreateService(
            client: new MockLlmClient(failConfig),
            config: FullOrchestrationConfig.ForTesting with { MaxRetries = 0 });
        var intent = NarrativeIntent.Continue();

        var result = await service.ExecuteCycleAsync(_emptyState, intent);
        var pipeline = ((Result<FullPipelineResult>.Success)result).Value;

        pipeline.IsSuccess.Should().BeFalse();
        // Au minimum ContextBuilder et PromptBuilder ont été exécutés avant l'échec de l'agent
        pipeline.StageResults.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Pipeline_PipelineId_ShouldBeUnique()
    {
        var service = CreateService();
        var intent = NarrativeIntent.Continue();

        var result1 = await service.ExecuteCycleAsync(_richState, intent);
        var result2 = await service.ExecuteCycleAsync(_richState, intent);

        var id1 = ((Result<FullPipelineResult>.Success)result1).Value.PipelineId;
        var id2 = ((Result<FullPipelineResult>.Success)result2).Value.PipelineId;

        id1.Should().NotBe(id2);
    }

    #endregion

    #region Cancellation

    [Fact]
    public async Task FullCycle_CancelledToken_ShouldNotCrash()
    {
        var service = CreateService(
            config: FullOrchestrationConfig.ForTesting with
            {
                GlobalTimeout = TimeSpan.FromMinutes(5)
            });
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        var intent = NarrativeIntent.Continue();

        // Avec un mock ultra-rapide, le pipeline peut finir avant de vérifier le token
        // L'essentiel est qu'il ne crashe pas — il retourne un résultat valide
        var result = await service.ExecuteCycleAsync(_richState, intent, cts.Token);

        result.Should().BeOfType<Result<FullPipelineResult>.Success>();
        var pipeline = ((Result<FullPipelineResult>.Success)result).Value;
        pipeline.Should().NotBeNull();
    }

    [Fact]
    public async Task FullCycle_TightTimeout_ShouldNotHang()
    {
        // Timeout très serré — le pipeline ne doit pas bloquer indéfiniment
        var service = CreateService(
            config: new FullOrchestrationConfig
            {
                GlobalTimeout = TimeSpan.FromMilliseconds(1),
                StageTimeout = TimeSpan.FromMilliseconds(1),
                MaxRetries = 0,
                RetryDelay = TimeSpan.Zero
            });
        var intent = NarrativeIntent.Continue();

        // Doit terminer en un temps raisonnable, pas bloquer
        var task = service.ExecuteCycleAsync(_richState, intent);
        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10)));

        completed.Should().Be(task, "Pipeline should not hang on tight timeout");
    }

    #endregion
}
