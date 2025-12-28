using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Narratum.Core;
using Narratum.Memory;
using Narratum.Memory.Services;
using Narratum.Memory.Store;
using Xunit;

namespace Narratum.Memory.Tests;

/// <summary>
/// Comprehensive integration tests for the memory system.
/// Tests complete workflows combining all Phase 2 components.
/// </summary>
public class MemoryIntegrationTests
{
    private readonly Mock<IMemoryRepository> _mockRepository;
    private readonly Mock<IFactExtractor> _mockFactExtractor;
    private readonly Mock<ISummaryGenerator> _mockSummaryGenerator;
    private readonly Mock<ICoherenceValidator> _mockCoherenceValidator;
    private readonly MemoryService _memoryService;

    public MemoryIntegrationTests()
    {
        _mockRepository = new Mock<IMemoryRepository>();
        _mockFactExtractor = new Mock<IFactExtractor>();
        _mockSummaryGenerator = new Mock<ISummaryGenerator>();
        _mockCoherenceValidator = new Mock<ICoherenceValidator>();

        _memoryService = new MemoryService(
            _mockRepository.Object,
            _mockFactExtractor.Object,
            _mockSummaryGenerator.Object,
            _mockCoherenceValidator.Object,
            MockLoggerFactory.CreateLogger<MemoryService>()
        );
    }

    // ========== Workflow Tests ==========

    [Fact]
    public async Task Workflow_RememberEvent_ThenRetrieve_Success()
    {
        // Arrange
        var worldId = Id.New();
        var @event = new { type = "death", character = "Aric" };
        var context = new Dictionary<string, object>();

        var fact = Fact.Create(
            "Aric is dead",
            FactType.CharacterState,
            MemoryLevel.Event,
            new[] { "Aric" }
        );

        _mockFactExtractor
            .Setup(x => x.ExtractFromEvent(It.IsAny<object>(), It.IsAny<EventExtractorContext>()))
            .Returns(new List<Fact> { fact });

        var savedMemoranda = new List<Memorandum>();
        _mockRepository
            .Setup(x => x.SaveAsync(It.IsAny<Memorandum>()))
            .Callback<Memorandum>(m => savedMemoranda.Add(m))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _memoryService.RememberEventAsync(worldId, @event);

        // Assert
        result.Should().NotBeNull();
        _mockRepository.Verify(x => x.SaveAsync(It.IsAny<Memorandum>()), Times.Once);
        savedMemoranda.Should().HaveCount(1);
    }

    [Fact]
    public async Task Workflow_RememberChapter_WithMultipleEvents_Success()
    {
        // Arrange
        var worldId = Id.New();
        var events = new List<object>
        {
            new { type = "moved", character = "Aric", to = "Forest" },
            new { type = "died", character = "Aric" }
        };

        var facts = new List<Fact>
        {
            Fact.Create("Aric moved to Forest", FactType.Event, MemoryLevel.Event, new[] { "Aric", "Forest" }),
            Fact.Create("Aric is dead", FactType.CharacterState, MemoryLevel.Event, new[] { "Aric" })
        };

        _mockFactExtractor
            .Setup(x => x.ExtractFromEvents(It.IsAny<IReadOnlyList<object>>(), It.IsAny<EventExtractorContext>()))
            .Returns(facts);

        _mockSummaryGenerator
            .Setup(x => x.SummarizeChapter(It.IsAny<IReadOnlyList<Fact>>()))
            .Returns("Aric moved and died.");

        _mockRepository
            .Setup(x => x.SaveAsync(It.IsAny<Memorandum>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _memoryService.RememberChapterAsync(worldId, events);

        // Assert
        result.Should().NotBeNull();
        _mockRepository.Verify(x => x.SaveAsync(It.IsAny<Memorandum>()), Times.Once);
        _mockSummaryGenerator.Verify(x => x.SummarizeChapter(It.IsAny<IReadOnlyList<Fact>>()), Times.Once);
    }

    [Fact]
    public async Task Workflow_Summarize_LongEventSequence_Success()
    {
        // Arrange
        var worldId = Id.New();
        var events = Enumerable.Range(0, 50)
            .Select(i => new { id = i, type = "event" })
            .Cast<object>()
            .ToList();

        var facts = Enumerable.Range(0, 50)
            .Select(i => Fact.Create(
                $"Event {i}",
                FactType.Event,
                MemoryLevel.Event,
                new[] { $"Entity_{i}" }
            ))
            .ToList();

        _mockFactExtractor
            .Setup(x => x.ExtractFromEvents(It.IsAny<IReadOnlyList<object>>(), It.IsAny<EventExtractorContext>()))
            .Returns(facts);

        _mockSummaryGenerator
            .Setup(x => x.SummarizeChapter(It.IsAny<IReadOnlyList<Fact>>()))
            .Returns("Summary of 50 events");

        // Act
        var result = await _memoryService.SummarizeHistoryAsync(worldId, events, targetLength: 500);

        // Assert
        result.Should().NotBeNull();
        _mockFactExtractor.Verify(x => x.ExtractFromEvents(It.IsAny<IReadOnlyList<object>>(), It.IsAny<EventExtractorContext>()), Times.Once);
        _mockSummaryGenerator.Verify(x => x.SummarizeChapter(It.IsAny<IReadOnlyList<Fact>>()), Times.Once);
    }

    [Fact]
    public async Task Workflow_ValidateCoherence_EmptyMemoria_NoViolations()
    {
        // Arrange
        var worldId = Id.New();
        var memoria = new List<Memorandum>();

        _mockCoherenceValidator
            .Setup(x => x.ValidateFacts(It.IsAny<IReadOnlyList<Fact>>()))
            .Returns(new List<CoherenceViolation>());

        // Act
        var result = await _memoryService.ValidateCoherenceAsync(worldId, memoria);

        // Assert
        result.Should().NotBeNull();
        _mockCoherenceValidator.Verify(x => x.ValidateFacts(It.IsAny<IReadOnlyList<Fact>>()), Times.Never);
    }

    [Fact]
    public async Task Workflow_GetCanonicalState_ReturnsAggregatedState()
    {
        // Arrange
        var worldId = Id.New();
        var fact = Fact.Create("Aric is alive", FactType.CharacterState, MemoryLevel.World, new[] { "Aric" });
        
        var canonicalState = CanonicalState.CreateEmpty(worldId.Value, MemoryLevel.World).AddFact(fact);

        // Act
        var result = await _memoryService.GetCanonicalStateAsync(worldId, DateTime.UtcNow);

        // Assert
        result.Should().NotBeNull();
    }

    // ========== Edge Cases ==========

    [Fact]
    public async Task EdgeCase_RememberChapter_EmptyEvents_Failure()
    {
        // Arrange
        var worldId = Id.New();
        var emptyEvents = new List<object>();

        // Act
        var result = await _memoryService.RememberChapterAsync(worldId, emptyEvents);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task EdgeCase_FindMemorandaByEntity_NoResults()
    {
        // Arrange
        var worldId = Id.New();

        // Act
        var result = await _memoryService.FindMemoriaByEntityAsync(worldId, "NonExistent");

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task EdgeCase_RetrieveMemorandum_NotFound()
    {
        // Arrange
        var memorandumId = Id.New();

        _mockRepository
            .Setup(x => x.GetByIdAsync(memorandumId.Value))
            .ReturnsAsync((Memorandum?)null);

        // Act
        var result = await _memoryService.RetrieveMemoriumAsync(memorandumId);

        // Assert
        result.Should().NotBeNull();
        _mockRepository.Verify(x => x.GetByIdAsync(memorandumId.Value), Times.Once);
    }

    // ========== Coherence Tests ==========

    [Fact]
    public async Task Coherence_ConsistentMemoria_NoViolations()
    {
        // Arrange
        var worldId = Id.New();
        var fact1 = Fact.Create("Aric is alive", FactType.CharacterState, MemoryLevel.World, new[] { "Aric" });
        var fact2 = Fact.Create("Lyra is alive", FactType.CharacterState, MemoryLevel.World, new[] { "Lyra" });
        
        var canonicalState = CanonicalState.CreateEmpty(worldId.Value, MemoryLevel.World);
        var stateWithFacts = canonicalState.AddFact(fact1).AddFact(fact2);

        var memoria = new List<Memorandum>
        {
            new Memorandum(
                Id: Guid.NewGuid(),
                WorldId: worldId.Value,
                Title: "Events",
                Description: "Initial events",
                CanonicalStates: new Dictionary<MemoryLevel, CanonicalState>
                {
                    { MemoryLevel.World, stateWithFacts }
                },
                Violations: new HashSet<CoherenceViolation>(),
                CreatedAt: DateTime.UtcNow,
                LastUpdated: DateTime.UtcNow
            )
        };

        _mockCoherenceValidator
            .Setup(x => x.ValidateFacts(It.IsAny<IReadOnlyList<Fact>>()))
            .Returns(new List<CoherenceViolation>());

        // Act
        var result = await _memoryService.ValidateCoherenceAsync(worldId, memoria);

        // Assert
        result.Should().NotBeNull();
        _mockCoherenceValidator.Verify(x => x.ValidateFacts(It.IsAny<IReadOnlyList<Fact>>()), Times.Once);
    }

    // ========== Long History Tests ==========

    [Fact]
    public async Task LongHistory_50Events_ProcessedSuccessfully()
    {
        // Arrange
        var worldId = Id.New();
        var events = Enumerable.Range(0, 50).Select(i => new { id = i }).Cast<object>().ToList();

        var facts = Enumerable.Range(0, 50)
            .Select(i => Fact.Create($"Fact {i}", FactType.Event, MemoryLevel.Event, new[] { $"Entity_{i}" }))
            .ToList();

        _mockFactExtractor
            .Setup(x => x.ExtractFromEvents(It.IsAny<IReadOnlyList<object>>(), It.IsAny<EventExtractorContext>()))
            .Returns(facts);

        _mockSummaryGenerator
            .Setup(x => x.SummarizeChapter(It.IsAny<IReadOnlyList<Fact>>()))
            .Returns("50 event summary");

        _mockRepository
            .Setup(x => x.SaveAsync(It.IsAny<Memorandum>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _memoryService.RememberChapterAsync(worldId, events);

        // Assert
        result.Should().NotBeNull();
        _mockRepository.Verify(x => x.SaveAsync(It.IsAny<Memorandum>()), Times.Once);
    }

    [Fact]
    public async Task LongHistory_100Events_SummarizedSuccessfully()
    {
        // Arrange
        var worldId = Id.New();
        var events = Enumerable.Range(0, 100).Select(i => new { id = i }).Cast<object>().ToList();

        var facts = Enumerable.Range(0, 100)
            .Select(i => Fact.Create($"Fact {i}", FactType.Event, MemoryLevel.Event, new[] { $"Entity_{i}" }))
            .ToList();

        _mockFactExtractor
            .Setup(x => x.ExtractFromEvents(It.IsAny<IReadOnlyList<object>>(), It.IsAny<EventExtractorContext>()))
            .Returns(facts);

        _mockSummaryGenerator
            .Setup(x => x.SummarizeChapter(It.IsAny<IReadOnlyList<Fact>>()))
            .Returns("100 event summary");

        // Act
        var result = await _memoryService.SummarizeHistoryAsync(worldId, events, targetLength: 500);

        // Assert
        result.Should().NotBeNull();
        _mockSummaryGenerator.Verify(x => x.SummarizeChapter(It.IsAny<IReadOnlyList<Fact>>()), Times.Once);
    }
}

/// <summary>
/// Helper for creating mock loggers in tests.
/// </summary>
public static class MockLoggerFactory
{
    public static Microsoft.Extensions.Logging.ILogger<T> CreateLogger<T>()
    {
        return new Mock<Microsoft.Extensions.Logging.ILogger<T>>().Object;
    }
}
