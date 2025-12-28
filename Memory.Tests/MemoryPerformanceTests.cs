using System;
using System.Collections.Generic;
using System.Diagnostics;
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
/// Performance tests for the memory system.
/// Measures throughput, latency, and scalability of memory operations.
/// </summary>
public class MemoryPerformanceTests
{
    [Fact]
    public async Task Performance_RememberEvent_CompletesQuickly()
    {
        // Arrange
        var mockRepository = new Mock<IMemoryRepository>();
        var mockFactExtractor = new Mock<IFactExtractor>();
        var mockSummaryGenerator = new Mock<ISummaryGenerator>();
        var mockCoherenceValidator = new Mock<ICoherenceValidator>();

        var service = new MemoryService(
            mockRepository.Object,
            mockFactExtractor.Object,
            mockSummaryGenerator.Object,
            mockCoherenceValidator.Object,
            CreateLogger<MemoryService>()
        );

        var worldId = Id.New();
        var @event = new { type = "death" };
        var fact = Fact.Create("Event happened", FactType.Event, MemoryLevel.Event, new[] { "Entity" });

        mockFactExtractor
            .Setup(x => x.ExtractFromEvent(It.IsAny<object>(), It.IsAny<EventExtractorContext>()))
            .Returns(new List<Fact> { fact });

        mockRepository
            .Setup(x => x.SaveAsync(It.IsAny<Memorandum>()))
            .Returns(Task.CompletedTask);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await service.RememberEventAsync(worldId, @event);
        stopwatch.Stop();

        // Assert
        result.Should().NotBeNull();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000);
    }

    [Fact]
    public async Task Performance_RememberChapter_50Events_CompletesInTime()
    {
        // Arrange
        var mockRepository = new Mock<IMemoryRepository>();
        var mockFactExtractor = new Mock<IFactExtractor>();
        var mockSummaryGenerator = new Mock<ISummaryGenerator>();
        var mockCoherenceValidator = new Mock<ICoherenceValidator>();

        var service = new MemoryService(
            mockRepository.Object,
            mockFactExtractor.Object,
            mockSummaryGenerator.Object,
            mockCoherenceValidator.Object,
            CreateLogger<MemoryService>()
        );

        var worldId = Id.New();
        var events = Enumerable.Range(0, 50).Select(i => new { id = i }).Cast<object>().ToList();

        var facts = Enumerable.Range(0, 50)
            .Select(i => Fact.Create($"Fact {i}", FactType.Event, MemoryLevel.Event, new[] { $"Entity_{i}" }))
            .ToList();

        mockFactExtractor
            .Setup(x => x.ExtractFromEvents(It.IsAny<IReadOnlyList<object>>(), It.IsAny<EventExtractorContext>()))
            .Returns(facts);

        mockSummaryGenerator
            .Setup(x => x.SummarizeChapter(It.IsAny<IReadOnlyList<Fact>>()))
            .Returns("Summary");

        mockRepository
            .Setup(x => x.SaveAsync(It.IsAny<Memorandum>()))
            .Returns(Task.CompletedTask);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await service.RememberChapterAsync(worldId, events);
        stopwatch.Stop();

        // Assert
        result.Should().NotBeNull();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000);
    }

    [Fact]
    public async Task Performance_Summarize_100Events_CompletesInTime()
    {
        // Arrange
        var mockRepository = new Mock<IMemoryRepository>();
        var mockFactExtractor = new Mock<IFactExtractor>();
        var mockSummaryGenerator = new Mock<ISummaryGenerator>();
        var mockCoherenceValidator = new Mock<ICoherenceValidator>();

        var service = new MemoryService(
            mockRepository.Object,
            mockFactExtractor.Object,
            mockSummaryGenerator.Object,
            mockCoherenceValidator.Object,
            CreateLogger<MemoryService>()
        );

        var worldId = Id.New();
        var events = Enumerable.Range(0, 100).Select(i => new { id = i }).Cast<object>().ToList();

        var facts = Enumerable.Range(0, 100)
            .Select(i => Fact.Create($"Fact {i}", FactType.Event, MemoryLevel.Event, new[] { $"Entity_{i}" }))
            .ToList();

        mockFactExtractor
            .Setup(x => x.ExtractFromEvents(It.IsAny<IReadOnlyList<object>>(), It.IsAny<EventExtractorContext>()))
            .Returns(facts);

        mockSummaryGenerator
            .Setup(x => x.SummarizeChapter(It.IsAny<IReadOnlyList<Fact>>()))
            .Returns("Summary");

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await service.SummarizeHistoryAsync(worldId, events, targetLength: 500);
        stopwatch.Stop();

        // Assert
        result.Should().NotBeNull();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000);
    }

    [Fact]
    public async Task Performance_ValidateCoherence_CompletesQuickly()
    {
        // Arrange
        var mockRepository = new Mock<IMemoryRepository>();
        var mockFactExtractor = new Mock<IFactExtractor>();
        var mockSummaryGenerator = new Mock<ISummaryGenerator>();
        var mockCoherenceValidator = new Mock<ICoherenceValidator>();

        var service = new MemoryService(
            mockRepository.Object,
            mockFactExtractor.Object,
            mockSummaryGenerator.Object,
            mockCoherenceValidator.Object,
            CreateLogger<MemoryService>()
        );

        var worldId = Id.New();
        var fact = Fact.Create("Test fact", FactType.Event, MemoryLevel.Event, new[] { "Entity" });
        var canonicalState = CanonicalState.CreateEmpty(worldId.Value, MemoryLevel.Event).AddFact(fact);
        
        var memoria = new List<Memorandum>
        {
            new Memorandum(
                Id: Guid.NewGuid(),
                WorldId: worldId.Value,
                Title: "Test",
                Description: "Test",
                CanonicalStates: new Dictionary<MemoryLevel, CanonicalState>
                {
                    { MemoryLevel.Event, canonicalState }
                },
                Violations: new HashSet<CoherenceViolation>(),
                CreatedAt: DateTime.UtcNow,
                LastUpdated: DateTime.UtcNow
            )
        };

        mockCoherenceValidator
            .Setup(x => x.ValidateFacts(It.IsAny<IReadOnlyList<Fact>>()))
            .Returns(new List<CoherenceViolation>());

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await service.ValidateCoherenceAsync(worldId, memoria);
        stopwatch.Stop();

        // Assert
        result.Should().NotBeNull();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000);
    }

    [Fact]
    public async Task Scalability_EventCountIncrease_StaysLinear()
    {
        // Arrange
        var mockRepository = new Mock<IMemoryRepository>();
        var mockFactExtractor = new Mock<IFactExtractor>();
        var mockSummaryGenerator = new Mock<ISummaryGenerator>();
        var mockCoherenceValidator = new Mock<ICoherenceValidator>();

        var service = new MemoryService(
            mockRepository.Object,
            mockFactExtractor.Object,
            mockSummaryGenerator.Object,
            mockCoherenceValidator.Object,
            CreateLogger<MemoryService>()
        );

        mockSummaryGenerator
            .Setup(x => x.SummarizeChapter(It.IsAny<IReadOnlyList<Fact>>()))
            .Returns("Summary");

        mockRepository
            .Setup(x => x.SaveAsync(It.IsAny<Memorandum>()))
            .Returns(Task.CompletedTask);

        var timings = new List<long>();

        // Act: Test with increasing event counts
        for (int eventCount = 10; eventCount <= 50; eventCount += 10)
        {
            var worldId = Id.New();
            var events = Enumerable.Range(0, eventCount).Select(i => new { id = i }).Cast<object>().ToList();
            var facts = Enumerable.Range(0, eventCount)
                .Select(i => Fact.Create($"Fact {i}", FactType.Event, MemoryLevel.Event, new[] { $"Entity_{i}" }))
                .ToList();

            mockFactExtractor
                .Setup(x => x.ExtractFromEvents(It.IsAny<IReadOnlyList<object>>(), It.IsAny<EventExtractorContext>()))
                .Returns(facts);

            var stopwatch = Stopwatch.StartNew();
            await service.RememberChapterAsync(worldId, events);
            stopwatch.Stop();

            timings.Add(stopwatch.ElapsedMilliseconds);
        }

        // Assert: Performance should scale roughly linearly
        // 5x events should not take 25x time
        var ratio = (double)timings.Last() / timings.First();
        ratio.Should().BeLessThan(10);
    }

    [Fact]
    public async Task StressTest_ConcurrentOperations_HandleMultipleRequests()
    {
        // Arrange
        var mockRepository = new Mock<IMemoryRepository>();
        var mockFactExtractor = new Mock<IFactExtractor>();
        var mockSummaryGenerator = new Mock<ISummaryGenerator>();
        var mockCoherenceValidator = new Mock<ICoherenceValidator>();

        var service = new MemoryService(
            mockRepository.Object,
            mockFactExtractor.Object,
            mockSummaryGenerator.Object,
            mockCoherenceValidator.Object,
            CreateLogger<MemoryService>()
        );

        var fact = Fact.Create("Test", FactType.Event, MemoryLevel.Event, new[] { "Entity" });

        mockFactExtractor
            .Setup(x => x.ExtractFromEvent(It.IsAny<object>(), It.IsAny<EventExtractorContext>()))
            .Returns(new List<Fact> { fact });

        mockRepository
            .Setup(x => x.SaveAsync(It.IsAny<Memorandum>()))
            .Returns(Task.CompletedTask);

        var tasks = new List<Task<Result<Memorandum>>>();

        // Act: Fire off 10 concurrent requests
        for (int i = 0; i < 10; i++)
        {
            var worldId = Id.New();
            var @event = new { id = i };
            tasks.Add(service.RememberEventAsync(worldId, @event));
        }

        var results = await Task.WhenAll(tasks);

        // Assert: All should succeed
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
    }

    private static Microsoft.Extensions.Logging.ILogger<T> CreateLogger<T>()
    {
        return new Mock<Microsoft.Extensions.Logging.ILogger<T>>().Object;
    }
}
