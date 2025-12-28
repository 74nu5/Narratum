using Narratum.Core;
using Narratum.Domain;
using Narratum.Memory.Services;

namespace Narratum.Memory.Tests;

public class FactExtractorServiceTests
{
    private readonly FactExtractorService _service;
    private readonly EventExtractorContext _context;

    public FactExtractorServiceTests()
    {
        var extractors = new IEventFactExtractor[]
        {
            new CharacterDeathEventExtractor(),
            new CharacterMovedEventExtractor(),
            new CharacterEncounterEventExtractor()
        };

        _service = new FactExtractorService(extractors);

        _context = new EventExtractorContext(
            WorldId: Guid.NewGuid(),
            EventTimestamp: DateTime.UtcNow,
            EntityNameMap: new Dictionary<string, string>
            {
                { Guid.NewGuid().ToString(), "Aric" },
                { Guid.NewGuid().ToString(), "Lyra" },
                { Guid.NewGuid().ToString(), "Tower" },
                { Guid.NewGuid().ToString(), "Forest" }
            }
        );
    }

    [Fact]
    public void CanExtract_WithSupportedEventType_ShouldReturnTrue()
    {
        // Arrange
        var eventType = typeof(CharacterDeathEvent);

        // Act
        var canExtract = _service.CanExtract(eventType);

        // Assert
        Assert.True(canExtract);
    }

    [Fact]
    public void CanExtract_WithUnsupportedEventType_ShouldReturnFalse()
    {
        // Act
        var canExtract = _service.CanExtract(typeof(string));

        // Assert
        Assert.False(canExtract);
    }

    [Fact]
    public void SupportedEventTypes_ShouldIncludeAllExtractors()
    {
        // Act
        var supported = _service.SupportedEventTypes;

        // Assert
        Assert.Contains(typeof(CharacterDeathEvent), supported);
        Assert.Contains(typeof(CharacterMovedEvent), supported);
        Assert.Contains(typeof(CharacterEncounterEvent), supported);
    }

    [Fact]
    public void ExtractFromEvent_WithNullEvent_ShouldThrowException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            _service.ExtractFromEvent(null!, _context));
    }

    [Fact]
    public void ExtractFromEvent_WithUnsupportedType_ShouldThrowException()
    {
        // Act & Assert
        Assert.Throws<NotSupportedException>(() =>
            _service.ExtractFromEvent("not an event", _context));
    }

    [Fact]
    public void ExtractFromEvent_CharacterDeathEvent_ShouldProduceFact()
    {
        // Arrange
        var characterGuid = Guid.NewGuid();
        var characterId = new Id(characterGuid);
        var deathEvent = new CharacterDeathEvent(characterId);
        
        var contextMap = new Dictionary<string, string>
        {
            { characterGuid.ToString(), "Aric" }
        };
        var context = new EventExtractorContext(Guid.NewGuid(), DateTime.UtcNow, contextMap);

        // Act
        var facts = _service.ExtractFromEvent(deathEvent, context);

        // Assert
        Assert.Single(facts);
        var fact = facts[0];
        Assert.Contains("died", fact.Content);
        Assert.Contains("Aric", fact.Content);
        Assert.Equal(FactType.CharacterState, fact.FactType);
        Assert.Equal(1.0, fact.Confidence);
    }

    [Fact]
    public void ExtractFromEvent_CharacterDeathWithCause_ShouldIncludeCause()
    {
        // Arrange
        var characterGuid = Guid.NewGuid();
        var characterId = new Id(characterGuid);
        var deathEvent = new CharacterDeathEvent(characterId, cause: "poisoned");
        
        var contextMap = new Dictionary<string, string>
        {
            { characterGuid.ToString(), "Aric" }
        };
        var context = new EventExtractorContext(Guid.NewGuid(), DateTime.UtcNow, contextMap);

        // Act
        var facts = _service.ExtractFromEvent(deathEvent, context);

        // Assert
        Assert.Single(facts);
        Assert.Contains("poisoned", facts[0].Content);
    }

    [Fact]
    public void ExtractFromEvent_CharacterMovedEvent_ShouldProduceTwoFacts()
    {
        // Arrange
        var characterGuid = Guid.NewGuid();
        var fromLocationGuid = Guid.NewGuid();
        var toLocationGuid = Guid.NewGuid();
        
        var characterId = new Id(characterGuid);
        var fromLocation = new Id(fromLocationGuid);
        var toLocation = new Id(toLocationGuid);
        var movedEvent = new CharacterMovedEvent(characterId, fromLocation, toLocation);
        
        var contextMap = new Dictionary<string, string>
        {
            { characterGuid.ToString(), "Aric" },
            { fromLocationGuid.ToString(), "Tower" },
            { toLocationGuid.ToString(), "Forest" }
        };
        var context = new EventExtractorContext(Guid.NewGuid(), DateTime.UtcNow, contextMap);

        // Act
        var facts = _service.ExtractFromEvent(movedEvent, context);

        // Assert
        Assert.Equal(2, facts.Count);
        
        // Vérifier le fait de mouvement
        var movementFact = facts.First(f => f.FactType == FactType.Event);
        Assert.Contains("moved from", movementFact.Content);
        Assert.Contains("Tower", movementFact.Content);
        Assert.Contains("Forest", movementFact.Content);

        // Vérifier le fait de location
        var locationFact = facts.First(f => f.FactType == FactType.LocationState);
        Assert.Contains("is at", locationFact.Content);
        Assert.Contains("Forest", locationFact.Content);
    }

    [Fact]
    public void ExtractFromEvent_CharacterEncounterEvent_ShouldProduceTwoFacts()
    {
        // Arrange
        var char1Guid = Guid.NewGuid();
        var char2Guid = Guid.NewGuid();
        var locationGuid = Guid.NewGuid();
        
        var char1 = new Id(char1Guid);
        var char2 = new Id(char2Guid);
        var location = new Id(locationGuid);
        var encounterEvent = new CharacterEncounterEvent(char1, char2, location);
        
        var contextMap = new Dictionary<string, string>
        {
            { char1Guid.ToString(), "Aric" },
            { char2Guid.ToString(), "Lyra" },
            { locationGuid.ToString(), "Tower" }
        };
        var context = new EventExtractorContext(Guid.NewGuid(), DateTime.UtcNow, contextMap);

        // Act
        var facts = _service.ExtractFromEvent(encounterEvent, context);

        // Assert
        Assert.Equal(2, facts.Count);

        // Vérifier le fait de rencontre
        var meetingFact = facts.First(f => f.FactType == FactType.Event);
        Assert.Contains("met", meetingFact.Content);
        Assert.Contains("Aric", meetingFact.Content);
        Assert.Contains("Lyra", meetingFact.Content);

        // Vérifier le fait de relation
        var relationshipFact = facts.First(f => f.FactType == FactType.Relationship);
        Assert.Contains("knows", relationshipFact.Content);
    }

    [Fact]
    public void ExtractFromEvent_IsDeterministic_SameFacts()
    {
        // Arrange
        var characterGuid = Guid.NewGuid();
        var characterId = new Id(characterGuid);
        var deathEvent = new CharacterDeathEvent(characterId);
        
        var contextMap = new Dictionary<string, string>
        {
            { characterGuid.ToString(), "Aric" }
        };
        var context = new EventExtractorContext(Guid.NewGuid(), DateTime.UtcNow, contextMap);

        // Act
        var facts1 = _service.ExtractFromEvent(deathEvent, context);
        var facts2 = _service.ExtractFromEvent(deathEvent, context);

        // Assert - Même contenu produit
        Assert.Equal(facts1.Count, facts2.Count);
        for (int i = 0; i < facts1.Count; i++)
        {
            Assert.Equal(facts1[i].Content, facts2[i].Content);
            Assert.Equal(facts1[i].FactType, facts2[i].FactType);
            Assert.Equal(facts1[i].Confidence, facts2[i].Confidence);
        }
    }

    [Fact]
    public void ExtractFromEvents_WithEmptyList_ShouldReturnEmpty()
    {
        // Act
        var facts = _service.ExtractFromEvents(new List<object>(), _context);

        // Assert
        Assert.Empty(facts);
    }

    [Fact]
    public void ExtractFromEvents_WithMultipleEvents_ShouldExtractAll()
    {
        // Arrange
        var char1Guid = Guid.NewGuid();
        var char2Guid = Guid.NewGuid();
        var loc1Guid = Guid.NewGuid();
        var loc2Guid = Guid.NewGuid();
        
        var char1 = new Id(char1Guid);
        var char2 = new Id(char2Guid);
        var loc1 = new Id(loc1Guid);
        var loc2 = new Id(loc2Guid);

        var events = new object[]
        {
            new CharacterMovedEvent(char1, loc1, loc2),
            new CharacterDeathEvent(char2),
            new CharacterEncounterEvent(char1, char2, loc2)
        };
        
        var contextMap = new Dictionary<string, string>
        {
            { char1Guid.ToString(), "Aric" },
            { char2Guid.ToString(), "Lyra" },
            { loc1Guid.ToString(), "Tower" },
            { loc2Guid.ToString(), "Forest" }
        };
        var context = new EventExtractorContext(Guid.NewGuid(), DateTime.UtcNow, contextMap);

        // Act
        var facts = _service.ExtractFromEvents(events, context);

        // Assert
        Assert.NotEmpty(facts);
        Assert.True(facts.Count >= 5); // 2 + 1 + 2 = 5 minimum

        // Vérifier qu'on a au moins un de chaque type
        Assert.Contains(FactType.Event, facts.Select(f => f.FactType));
        Assert.Contains(FactType.CharacterState, facts.Select(f => f.FactType));
        Assert.Contains(FactType.Relationship, facts.Select(f => f.FactType));
    }

    [Fact]
    public void ExtractFromEvents_IsDeterministic_SameOrder()
    {
        // Arrange
        var char1Guid = Guid.NewGuid();
        var char2Guid = Guid.NewGuid();
        
        var events = new object[]
        {
            new CharacterDeathEvent(new Id(char1Guid)),
            new CharacterDeathEvent(new Id(char2Guid))
        };
        
        var contextMap = new Dictionary<string, string>
        {
            { char1Guid.ToString(), "Aric" },
            { char2Guid.ToString(), "Lyra" }
        };
        var context = new EventExtractorContext(Guid.NewGuid(), DateTime.UtcNow, contextMap);

        // Act
        var facts1 = _service.ExtractFromEvents(events, context);
        var facts2 = _service.ExtractFromEvents(events, context);

        // Assert - Même ordre et contenu
        Assert.Equal(facts1.Count, facts2.Count);
        for (int i = 0; i < facts1.Count; i++)
        {
            Assert.Equal(facts1[i].Content, facts2[i].Content);
        }
    }

    [Fact]
    public void ExtractFromEvents_DeduplicatesIdenticalFacts()
    {
        // Arrange
        var characterGuid = Guid.NewGuid();
        var characterId = new Id(characterGuid);
        var sameEvent1 = new CharacterDeathEvent(characterId);
        var sameEvent2 = new CharacterDeathEvent(characterId);

        var events = new object[] { sameEvent1, sameEvent2 };
        
        var contextMap = new Dictionary<string, string>
        {
            { characterGuid.ToString(), "Aric" }
        };
        var context = new EventExtractorContext(Guid.NewGuid(), DateTime.UtcNow, contextMap);

        // Act
        var facts = _service.ExtractFromEvents(events, context);

        // Assert - Les faits identiques sont dédupliqués
        Assert.Single(facts);
    }

    [Fact]
    public void CharacterDeathEventExtractor_SupportsCorrectType()
    {
        // Arrange
        var extractor = new CharacterDeathEventExtractor();

        // Act & Assert
        Assert.Contains(typeof(CharacterDeathEvent), extractor.SupportedEventTypes);
        Assert.True(extractor.SupportedEventTypes.Count == 1);
    }

    [Fact]
    public void CharacterMovedEventExtractor_SupportsCorrectType()
    {
        // Arrange
        var extractor = new CharacterMovedEventExtractor();

        // Act & Assert
        Assert.Contains(typeof(CharacterMovedEvent), extractor.SupportedEventTypes);
    }

    [Fact]
    public void CharacterEncounterEventExtractor_SupportsCorrectType()
    {
        // Arrange
        var extractor = new CharacterEncounterEventExtractor();

        // Act & Assert
        Assert.Contains(typeof(CharacterEncounterEvent), extractor.SupportedEventTypes);
    }

    [Fact]
    public void ExtractFromEvent_EntityNamesAreResolved()
    {
        // Arrange
        var characterGuid = Guid.NewGuid();
        var characterId = new Id(characterGuid);
        var deathEvent = new CharacterDeathEvent(characterId);
        
        var contextMap = new Dictionary<string, string>
        {
            { characterGuid.ToString(), "Aric" }
        };
        var context = new EventExtractorContext(Guid.NewGuid(), DateTime.UtcNow, contextMap);

        // Act
        var facts = _service.ExtractFromEvent(deathEvent, context);

        // Assert
        Assert.Contains("Aric", facts[0].Content);
        Assert.DoesNotContain(characterGuid.ToString(), facts[0].Content);
    }

    [Fact]
    public void ExtractFromEvent_UnknownEntityIdsAreFallback()
    {
        // Arrange
        var unknownGuid = Guid.NewGuid();
        var characterId = new Id(unknownGuid);
        var deathEvent = new CharacterDeathEvent(characterId);
        
        var contextMap = new Dictionary<string, string>(); // Empty mapping
        var context = new EventExtractorContext(Guid.NewGuid(), DateTime.UtcNow, contextMap);

        // Act
        var facts = _service.ExtractFromEvent(deathEvent, context);

        // Assert
        Assert.Contains("Character_", facts[0].Content);
    }
}
