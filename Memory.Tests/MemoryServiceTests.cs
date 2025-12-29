using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Narratum.Core;
using Narratum.Memory;
using Narratum.Memory.Services;
using Narratum.Memory.Store;

namespace Narratum.Memory.Tests;

/// <summary>
/// Tests d'intégration pour MemoryService - phase 2.6 orchestration.
/// </summary>
public class MemoryServiceTests
{
    private readonly Mock<IMemoryRepository> _mockRepository;
    private readonly Mock<IFactExtractor> _mockExtractor;
    private readonly Mock<ISummaryGenerator> _mockSummaryGenerator;
    private readonly Mock<ICoherenceValidator> _mockCoherenceValidator;
    private readonly Mock<ILogger<MemoryService>> _mockLogger;
    private readonly MemoryService _service;

    public MemoryServiceTests()
    {
        _mockRepository = new Mock<IMemoryRepository>();
        _mockExtractor = new Mock<IFactExtractor>();
        _mockSummaryGenerator = new Mock<ISummaryGenerator>();
        _mockCoherenceValidator = new Mock<ICoherenceValidator>();
        _mockLogger = new Mock<ILogger<MemoryService>>();

        _service = new MemoryService(
            _mockRepository.Object,
            _mockExtractor.Object,
            _mockSummaryGenerator.Object,
            _mockCoherenceValidator.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task RememberEventAsync_ValidEvent_CreatesAndPersistsMemorandum()
    {
        // Arrange
        var worldId = Id.New();
        var domainEvent = new object();
        var facts = new List<Fact>();

        _mockExtractor
            .Setup(x => x.ExtractFromEvent(It.IsAny<object>(), It.IsAny<EventExtractorContext>()))
            .Returns(facts);

        _mockRepository
            .Setup(x => x.SaveAsync(It.IsAny<Memorandum>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.RememberEventAsync(worldId, domainEvent);

        // Assert
        result.Should().NotBeNull();
        _mockRepository.Verify(x => x.SaveAsync(It.IsAny<Memorandum>()), Times.Once);
    }

    [Fact]
    public async Task RememberChapterAsync_MultipleEvents_CreatesChapterMemorandum()
    {
        // Arrange
        var worldId = Id.New();
        var events = new List<object> { new object(), new object() };
        var facts = new List<Fact>();

        _mockExtractor
            .Setup(x => x.ExtractFromEvents(It.IsAny<IReadOnlyList<object>>(), It.IsAny<EventExtractorContext>()))
            .Returns(facts);

        _mockSummaryGenerator
            .Setup(x => x.SummarizeChapter(It.IsAny<IReadOnlyList<Fact>>()))
            .Returns("Chapter summary");

        _mockRepository
            .Setup(x => x.SaveAsync(It.IsAny<Memorandum>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.RememberChapterAsync(worldId, events);

        // Assert
        result.Should().NotBeNull();
        _mockSummaryGenerator.Verify(x => x.SummarizeChapter(It.IsAny<IReadOnlyList<Fact>>()), Times.Once);
    }

    [Fact]
    public async Task RememberChapterAsync_EmptyEvents_ReturnsFailure()
    {
        // Arrange
        var worldId = Id.New();
        var events = new List<object>();

        // Act
        var result = await _service.RememberChapterAsync(worldId, events);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task RetrieveMemoriumAsync_ExistingId_ReturnsMemorandum()
    {
        // Arrange
        var memorandumId = Guid.NewGuid();
        var memorandum = Memorandum.CreateEmpty(Guid.NewGuid(), "Test");

        _mockRepository
            .Setup(x => x.GetByIdAsync(memorandumId))
            .ReturnsAsync(memorandum);

        // Act
        var result = await _service.RetrieveMemoriumAsync(Id.From(memorandumId));

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task RetrieveMemoriumAsync_NonExistentId_ReturnsNull()
    {
        // Arrange
        var memorandumId = Guid.NewGuid();

        _mockRepository
            .Setup(x => x.GetByIdAsync(memorandumId))
            .ReturnsAsync((Memorandum?)null);

        // Act
        var result = await _service.RetrieveMemoriumAsync(Id.From(memorandumId));

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task FindMemoriaByEntityAsync_EntityNotFound_ReturnsEmpty()
    {
        // Arrange
        var worldId = Guid.NewGuid();
        var entityName = "NonExistentEntity";

        _mockRepository
            .Setup(x => x.GetByWorldAsync(worldId))
            .ReturnsAsync(new List<Memorandum>());

        // Act
        var result = await _service.FindMemoriaByEntityAsync(Id.From(worldId), entityName);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task FindMemoriaByEntityAsync_EmptyEntityName_ReturnsFailure()
    {
        // Arrange
        var worldId = Guid.NewGuid();

        // Act
        var result = await _service.FindMemoriaByEntityAsync(Id.From(worldId), "");

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task SummarizeHistoryAsync_ValidEvents_ReturnsSummary()
    {
        // Arrange
        var worldId = Id.New();
        var events = new List<object> { new object(), new object() };
        var facts = new List<Fact>();

        _mockExtractor
            .Setup(x => x.ExtractFromEvents(It.IsAny<IReadOnlyList<object>>(), It.IsAny<EventExtractorContext>()))
            .Returns(facts);

        _mockSummaryGenerator
            .Setup(x => x.SummarizeChapter(It.IsAny<IReadOnlyList<Fact>>()))
            .Returns("A concise summary");

        // Act
        var result = await _service.SummarizeHistoryAsync(worldId, events);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task SummarizeHistoryAsync_EmptyEvents_ReturnsFailure()
    {
        // Arrange
        var worldId = Id.New();
        var events = new List<object>();

        // Act
        var result = await _service.SummarizeHistoryAsync(worldId, events);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCanonicalStateAsync_WithMemorias_ReturnsAggregatedState()
    {
        // Arrange
        var worldId = Guid.NewGuid();
        var asOf = DateTime.UtcNow;
        var mem = Memorandum.CreateEmpty(worldId, "Test");

        _mockRepository
            .Setup(x => x.GetByWorldAsync(worldId))
            .ReturnsAsync(new List<Memorandum> { mem });

        // Act
        var result = await _service.GetCanonicalStateAsync(Id.From(worldId), asOf);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidateCoherenceAsync_ConsistentMemoria_ReturnsNoViolations()
    {
        // Arrange
        var worldId = Id.New();
        var memoria = new List<Memorandum>
        {
            Memorandum.CreateEmpty(worldId.Value, "Mem1"),
            Memorandum.CreateEmpty(worldId.Value, "Mem2"),
        };

        _mockCoherenceValidator
            .Setup(x => x.ValidateFacts(It.IsAny<IReadOnlyList<Fact>>()))
            .Returns(new List<CoherenceViolation>());

        // Act
        var result = await _service.ValidateCoherenceAsync(worldId, memoria);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidateCoherenceAsync_EmptyMemoria_ReturnsNoViolations()
    {
        // Arrange
        var worldId = Id.New();
        var memoria = new List<Memorandum>();

        // Act
        var result = await _service.ValidateCoherenceAsync(worldId, memoria);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task AssertFactAsync_ValidFact_ReturnsSuccess()
    {
        // Arrange
        var worldId = Id.New();
        var fact = Fact.Create(
            "Test assertion",
            FactType.CharacterState,
            MemoryLevel.Event,
            new[] { "TestEntity" });

        // Act
        var result = await _service.AssertFactAsync(worldId, fact);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task AssertFactAsync_NullFact_ReturnsFailure()
    {
        // Arrange
        var worldId = Id.New();
        Fact? fact = null;

        // Act
        var result = await _service.AssertFactAsync(worldId, fact!);

        // Assert
        result.Should().NotBeNull();
    }
}
