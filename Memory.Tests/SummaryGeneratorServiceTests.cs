using Narratum.Core;
using Narratum.Memory;
using Narratum.Memory.Services;

namespace Narratum.Memory.Tests;

public class SummaryGeneratorServiceTests
{
    private readonly SummaryGeneratorService _service;

    public SummaryGeneratorServiceTests()
    {
        _service = new SummaryGeneratorService();
    }

    #region SummarizeChapter Tests

    [Fact]
    public void SummarizeChapter_WithEmptyFacts_ShouldReturnPlaceholder()
    {
        // Act
        var summary = _service.SummarizeChapter(new List<Fact>());

        // Assert
        Assert.Equal("[No events]", summary);
    }

    [Fact]
    public void SummarizeChapter_WithSingleFact_ShouldReturnFactContent()
    {
        // Arrange
        var fact = Fact.Create(
            content: "Aric met Lyra",
            factType: FactType.Event,
            memoryLevel: MemoryLevel.Event,
            entityReferences: new[] { "Aric", "Lyra" },
            timeContext: "At the tower",
            confidence: 1.0,
            source: Guid.NewGuid().ToString()
        );

        // Act
        var summary = _service.SummarizeChapter(new List<Fact> { fact });

        // Assert
        Assert.Equal("Aric met Lyra", summary);
    }

    [Fact]
    public void SummarizeChapter_WithMultipleFacts_ShouldConcatenateDeterministically()
    {
        // Arrange
        var fact1 = Fact.Create(
            content: "Aric moved to Tower",
            factType: FactType.Event,
            memoryLevel: MemoryLevel.Event,
            entityReferences: new[] { "Aric", "Tower" },
            timeContext: "At noon",
            confidence: 1.0,
            source: Guid.NewGuid().ToString()
        );

        var fact2 = Fact.Create(
            content: "Tower is safe",
            factType: FactType.LocationState,
            memoryLevel: MemoryLevel.Event,
            entityReferences: new[] { "Tower" },
            timeContext: "At noon",
            confidence: 1.0,
            source: Guid.NewGuid().ToString()
        );

        var facts = new List<Fact> { fact1, fact2 };

        // Act
        var summary = _service.SummarizeChapter(facts);

        // Assert
        Assert.Contains(" | ", summary);
        Assert.Contains("Aric moved to Tower", summary);
        Assert.Contains("Tower is safe", summary);
    }

    [Fact]
    public void SummarizeChapter_IsDeterministic_SameFacts()
    {
        // Arrange
        var facts = CreateTestFacts(5);

        // Act
        var summary1 = _service.SummarizeChapter(facts);
        var summary2 = _service.SummarizeChapter(facts);

        // Assert
        Assert.Equal(summary1, summary2);
    }

    [Fact]
    public void SummarizeChapter_TruncatesLongSummary()
    {
        // Arrange - Create many facts to exceed 300 chars
        var facts = new List<Fact>();
        for (int i = 0; i < 20; i++)
        {
            facts.Add(Fact.Create(
                content: $"Long fact number {i} with lots of details and information",
                factType: FactType.Event,
                memoryLevel: MemoryLevel.Event,
                entityReferences: new[] { $"Entity{i}" },
                timeContext: $"At time {i}",
                confidence: 1.0,
                source: Guid.NewGuid().ToString()
            ));
        }

        // Act
        var summary = _service.SummarizeChapter(facts);

        // Assert
        Assert.True(summary.Length <= 300);
        if (summary.Length >= 297)
        {
            Assert.EndsWith("…", summary);
        }
    }

    [Fact]
    public void SummarizeChapter_FiltersHighConfidenceFacts()
    {
        // Arrange
        var highConfidenceFact = Fact.Create(
            content: "Important death",
            factType: FactType.CharacterState,
            memoryLevel: MemoryLevel.Event,
            entityReferences: new[] { "Aric" },
            timeContext: "Critical",
            confidence: 1.0,
            source: Guid.NewGuid().ToString()
        );

        var lowConfidenceFacts = CreateLowConfidenceFacts(10);
        var allFacts = new List<Fact> { highConfidenceFact };
        allFacts.AddRange(lowConfidenceFacts);

        // Act
        var summary = _service.SummarizeChapter(allFacts);

        // Assert
        Assert.Contains("Important death", summary);
    }

    #endregion

    #region SummarizeArc Tests

    [Fact]
    public void SummarizeArc_WithEmptyChapters_ShouldReturnPlaceholder()
    {
        // Act
        var summary = _service.SummarizeArc(new List<string>());

        // Assert
        Assert.Equal("[No chapters]", summary);
    }

    [Fact]
    public void SummarizeArc_WithSingleChapter_ShouldExtractKeyPoints()
    {
        // Arrange
        var chapterSummaries = new List<string>
        {
            "Aric arrived | Tower was safe | Meeting occurred"
        };

        // Act
        var summary = _service.SummarizeArc(chapterSummaries);

        // Assert
        Assert.Contains("→", summary);
        Assert.Contains("Aric arrived", summary);
    }

    [Fact]
    public void SummarizeArc_WithMultipleChapters_ShouldAggregateKeyPoints()
    {
        // Arrange
        var chapterSummaries = new List<string>
        {
            "Aric arrived | Tower was safe",
            "Lyra appeared | Meeting occurred",
            "Combat started | Important revelation"
        };

        // Act
        var summary = _service.SummarizeArc(chapterSummaries);

        // Assert
        Assert.NotEmpty(summary);
        Assert.Contains("→", summary);
    }

    [Fact]
    public void SummarizeArc_IsDeterministic_SameChapters()
    {
        // Arrange
        var chapterSummaries = new List<string>
        {
            "Event A | Event B",
            "Event C | Event D",
            "Event E | Event F"
        };

        // Act
        var summary1 = _service.SummarizeArc(chapterSummaries);
        var summary2 = _service.SummarizeArc(chapterSummaries);

        // Assert
        Assert.Equal(summary1, summary2);
    }

    [Fact]
    public void SummarizeArc_RemovesDuplicateKeyPoints()
    {
        // Arrange
        var chapterSummaries = new List<string>
        {
            "Aric moved | Important revelation",
            "Aric moved | Combat started",
            "Important revelation | Lyra appeared"
        };

        // Act
        var summary = _service.SummarizeArc(chapterSummaries);

        // Assert
        // "Aric moved" and "Important revelation" should appear only once
        var arcCount = summary.Split(" → ").Where(p => p == "Aric moved").Count();
        Assert.True(arcCount <= 1);
    }

    [Fact]
    public void SummarizeArc_TruncatesLongSummary()
    {
        // Arrange - Create many key points to exceed 500 chars
        var summaries = new List<string>();
        for (int i = 0; i < 30; i++)
        {
            summaries.Add($"Event number {i} with a very long description containing lots of important information");
        }

        // Act
        var summary = _service.SummarizeArc(summaries);

        // Assert
        Assert.True(summary.Length <= 500);
    }

    #endregion

    #region SummarizeWorld Tests

    [Fact]
    public void SummarizeWorld_WithEmptyArcs_ShouldReturnPlaceholder()
    {
        // Act
        var summary = _service.SummarizeWorld(new List<string>());

        // Assert
        Assert.Equal("[Empty world history]", summary);
    }

    [Fact]
    public void SummarizeWorld_WithSingleArc_ShouldFormatAsStructuredOutput()
    {
        // Arrange
        var arcSummaries = new List<string>
        {
            "Aric arrived → Tower was safe → Meeting with Lyra"
        };

        // Act
        var summary = _service.SummarizeWorld(arcSummaries);

        // Assert
        Assert.Contains("## World History", summary);
        Assert.Contains("### Arc 1", summary);
        Assert.Contains("Aric arrived", summary);
    }

    [Fact]
    public void SummarizeWorld_WithMultipleArcs_ShouldNumberThemSequentially()
    {
        // Arrange
        var arcSummaries = new List<string>
        {
            "Arc 1 content",
            "Arc 2 content",
            "Arc 3 content"
        };

        // Act
        var summary = _service.SummarizeWorld(arcSummaries);

        // Assert
        Assert.Contains("### Arc 1", summary);
        Assert.Contains("### Arc 2", summary);
        Assert.Contains("### Arc 3", summary);
    }

    [Fact]
    public void SummarizeWorld_IsDeterministic_SameArcs()
    {
        // Arrange
        var arcSummaries = new List<string>
        {
            "First arc events",
            "Second arc events",
            "Third arc events"
        };

        // Act
        var summary1 = _service.SummarizeWorld(arcSummaries);
        var summary2 = _service.SummarizeWorld(arcSummaries);

        // Assert
        Assert.Equal(summary1, summary2);
    }

    [Fact]
    public void SummarizeWorld_IncludesMajorEventsSection()
    {
        // Arrange
        var arcSummaries = new List<string>
        {
            "Death → Important → Revelation",
            "Combat → Important → Meeting"
        };

        // Act
        var summary = _service.SummarizeWorld(arcSummaries);

        // Assert
        Assert.Contains("### Major Events", summary);
        Assert.Contains("- ", summary);
    }

    #endregion

    #region FilterImportantFacts Tests

    [Fact]
    public void FilterImportantFacts_WithEmptyList_ShouldReturnEmpty()
    {
        // Act
        var filtered = _service.FilterImportantFacts(new List<Fact>(), 10);

        // Assert
        Assert.Empty(filtered);
    }

    [Fact]
    public void FilterImportantFacts_WithFewerFactsThanMax_ShouldReturnAll()
    {
        // Arrange
        var facts = CreateTestFacts(5);

        // Act
        var filtered = _service.FilterImportantFacts(facts, 10);

        // Assert
        Assert.Equal(5, filtered.Count);
    }

    [Fact]
    public void FilterImportantFacts_WithMoreFactsThanMax_ShouldReturnMaxCount()
    {
        // Arrange
        var facts = CreateTestFacts(20);

        // Act
        var filtered = _service.FilterImportantFacts(facts, 5);

        // Assert
        Assert.Equal(5, filtered.Count);
    }

    [Fact]
    public void FilterImportantFacts_PrioritizesHighConfidenceFacts()
    {
        // Arrange
        var highConfidenceFact = Fact.Create(
            content: "High confidence important",
            factType: FactType.CharacterState,
            memoryLevel: MemoryLevel.Event,
            entityReferences: new[] { "Aric" },
            timeContext: "Important",
            confidence: 1.0,
            source: Guid.NewGuid().ToString()
        );

        var lowConfidenceFacts = CreateLowConfidenceFacts(10);
        var allFacts = new List<Fact> { highConfidenceFact };
        allFacts.AddRange(lowConfidenceFacts);

        // Act
        var filtered = _service.FilterImportantFacts(allFacts, 3);

        // Assert
        Assert.Contains(highConfidenceFact, filtered);
    }

    [Fact]
    public void FilterImportantFacts_IsDeterministic_SameFacts()
    {
        // Arrange
        var facts = CreateTestFacts(15);

        // Act
        var filtered1 = _service.FilterImportantFacts(facts, 5);
        var filtered2 = _service.FilterImportantFacts(facts, 5);

        // Assert
        Assert.Equal(filtered1.Count, filtered2.Count);
        for (int i = 0; i < filtered1.Count; i++)
        {
            Assert.Equal(filtered1[i].Id, filtered2[i].Id);
        }
    }

    #endregion

    #region ExtractKeyPoints Tests

    [Fact]
    public void ExtractKeyPoints_WithEmptyString_ShouldReturnEmpty()
    {
        // Act
        var points = _service.ExtractKeyPoints(string.Empty);

        // Assert
        Assert.Empty(points);
    }

    [Fact]
    public void ExtractKeyPoints_WithPipeSeparator_ShouldSplitCorrectly()
    {
        // Arrange
        var summary = "Event A | Event B | Event C";

        // Act
        var points = _service.ExtractKeyPoints(summary);

        // Assert
        Assert.Equal(3, points.Count);
        Assert.Contains("Event A", points);
        Assert.Contains("Event B", points);
        Assert.Contains("Event C", points);
    }

    [Fact]
    public void ExtractKeyPoints_WithArrowSeparator_ShouldSplitCorrectly()
    {
        // Arrange
        var summary = "Arc 1 → Arc 2 → Arc 3";

        // Act
        var points = _service.ExtractKeyPoints(summary);

        // Assert
        Assert.Equal(3, points.Count);
        Assert.Contains("Arc 1", points);
        Assert.Contains("Arc 2", points);
        Assert.Contains("Arc 3", points);
    }

    [Fact]
    public void ExtractKeyPoints_RemovesDuplicates()
    {
        // Arrange
        var summary = "Event A | Event A | Event B";

        // Act
        var points = _service.ExtractKeyPoints(summary);

        // Assert
        Assert.Equal(2, points.Count);
        Assert.DoesNotContain("Event A | Event A", points);
    }

    [Fact]
    public void ExtractKeyPoints_IsDeterministic_SameSummary()
    {
        // Arrange
        var summary = "Point A | Point B | Point C | Point D";

        // Act
        var points1 = _service.ExtractKeyPoints(summary);
        var points2 = _service.ExtractKeyPoints(summary);

        // Assert
        Assert.Equal(points1.Count, points2.Count);
        for (int i = 0; i < points1.Count; i++)
        {
            Assert.Equal(points1[i], points2[i]);
        }
    }

    [Fact]
    public void ExtractKeyPoints_TrimsWhitespace()
    {
        // Arrange
        var summary = "  Event A  |  Event B  |  Event C  ";

        // Act
        var points = _service.ExtractKeyPoints(summary);

        // Assert
        Assert.All(points, p => Assert.False(p.StartsWith(" ")));
        Assert.All(points, p => Assert.False(p.EndsWith(" ")));
    }

    #endregion

    #region Determinism Tests

    [Fact]
    public void FullHierarchy_IsDeterministic_ChapterToArcToWorld()
    {
        // Arrange
        var chapterFacts = CreateTestFacts(10);
        var chapter1 = _service.SummarizeChapter(chapterFacts);
        var chapter2 = _service.SummarizeChapter(chapterFacts);
        var chapter3 = _service.SummarizeChapter(chapterFacts);

        var chapters = new List<string> { chapter1, chapter2, chapter3 };

        // Act
        var arc1 = _service.SummarizeArc(chapters);
        var arc2 = _service.SummarizeArc(chapters);

        var arcs = new List<string> { arc1 };
        var world1 = _service.SummarizeWorld(arcs);
        var world2 = _service.SummarizeWorld(arcs);

        // Assert - All levels deterministic
        Assert.Equal(chapter1, chapter2);
        Assert.Equal(arc1, arc2);
        Assert.Equal(world1, world2);
    }

    [Fact]
    public void DeterminismUnderVariousInputSizes()
    {
        // Test with different fact counts
        for (int size = 0; size <= 50; size += 10)
        {
            // Arrange
            var facts = CreateTestFacts(size);

            // Act
            var summary1 = _service.SummarizeChapter(facts);
            var summary2 = _service.SummarizeChapter(facts);

            // Assert
            Assert.True(summary1 == summary2, $"Failed at size {size}");
        }
    }

    #endregion

    #region Helper Methods

    private List<Fact> CreateTestFacts(int count)
    {
        var facts = new List<Fact>();
        for (int i = 0; i < count; i++)
        {
            facts.Add(Fact.Create(
                content: $"Event {i}: Important narrative point",
                factType: (FactType)(i % 5),
                memoryLevel: MemoryLevel.Event,
                entityReferences: new[] { $"Character{i % 5}" },
                timeContext: $"At time {i}",
                confidence: 1.0,
                source: Guid.NewGuid().ToString()
            ));
        }
        return facts;
    }

    private List<Fact> CreateLowConfidenceFacts(int count)
    {
        var facts = new List<Fact>();
        for (int i = 0; i < count; i++)
        {
            facts.Add(Fact.Create(
                content: $"Low confidence detail {i}",
                factType: FactType.Knowledge,
                memoryLevel: MemoryLevel.Event,
                entityReferences: new[] { $"Detail{i}" },
                timeContext: "Background",
                confidence: 0.3,
                source: Guid.NewGuid().ToString()
            ));
        }
        return facts;
    }

    private List<Fact> CreateNonCanonicalFacts(int count)
    {
        var facts = new List<Fact>();
        for (int i = 0; i < count; i++)
        {
            var fact = Fact.Create(
                content: $"Minor detail {i}",
                factType: FactType.Knowledge,
                memoryLevel: MemoryLevel.Event,
                entityReferences: new[] { $"Minor{i}" },
                timeContext: "Background",
                confidence: 0.5,
                source: Guid.NewGuid().ToString()
            );
            facts.Add(fact);
        }
        return facts;
    }

    #endregion
}
