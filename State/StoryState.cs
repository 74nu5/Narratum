using Narratum.Core;
using Narratum.Domain;

namespace Narratum.State;

/// <summary>
/// Represents the complete and authoritative state of the narrative at a specific point.
/// This is the single source of truth for story progression.
/// Immutability is enforced through record semantics and controlled mutation.
/// </summary>
public record StoryState
{
    /// <summary>
    /// Global world state.
    /// </summary>
    public WorldState WorldState { get; init; }

    /// <summary>
    /// State of each character in the story.
    /// </summary>
    public IReadOnlyDictionary<Id, CharacterState> Characters { get; init; } = new Dictionary<Id, CharacterState>();

    /// <summary>
    /// Complete and immutable history of all events.
    /// Events never disappear - they're canonical facts.
    /// </summary>
    public IReadOnlyList<Event> EventHistory { get; init; } = [];

    /// <summary>
    /// Current chapter, if any.
    /// </summary>
    public StoryChapter? CurrentChapter { get; init; }

    /// <summary>
    /// Timestamp when this state was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    public StoryState(WorldState worldState)
    {
        WorldState = worldState ?? throw new ArgumentNullException(nameof(worldState));
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a state with all initial values.
    /// </summary>
    public static StoryState Create(
        Id worldId, 
        string worldName,
        IReadOnlyDictionary<Id, CharacterState>? characters = null)
    {
        var worldState = new WorldState(worldId, worldName);
        return new StoryState(worldState)
        {
            Characters = characters ?? new Dictionary<Id, CharacterState>(),
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Adds or updates a character state.
    /// </summary>
    public StoryState WithCharacter(CharacterState characterState)
    {
        var characters = new Dictionary<Id, CharacterState>(Characters)
        {
            [characterState.CharacterId] = characterState
        };
        return this with { Characters = characters.AsReadOnly() };
    }

    /// <summary>
    /// Adds multiple character states.
    /// </summary>
    public StoryState WithCharacters(params CharacterState[] states)
    {
        var characters = new Dictionary<Id, CharacterState>(Characters);
        foreach (var state in states)
        {
            characters[state.CharacterId] = state;
        }
        return this with { Characters = characters.AsReadOnly() };
    }

    /// <summary>
    /// Adds an event to the history.
    /// Events are immutable - they form the canonical record.
    /// </summary>
    public StoryState WithEvent(Event storyEvent)
    {
        if (storyEvent == null)
            throw new ArgumentNullException(nameof(storyEvent));

        var events = new List<Event>(EventHistory) { storyEvent };
        var newWorldState = WorldState.WithEventOccurred();

        return this with
        {
            EventHistory = events.AsReadOnly(),
            WorldState = newWorldState
        };
    }

    /// <summary>
    /// Adds multiple events to the history.
    /// </summary>
    public StoryState WithEvents(params Event[] storyEvents)
    {
        var events = new List<Event>(EventHistory);
        var newWorldState = WorldState;

        foreach (var storyEvent in storyEvents)
        {
            if (storyEvent == null)
                throw new ArgumentNullException(nameof(storyEvent));

            events.Add(storyEvent);
            newWorldState = newWorldState.WithEventOccurred();
        }

        return this with
        {
            EventHistory = events.AsReadOnly(),
            WorldState = newWorldState
        };
    }

    /// <summary>
    /// Updates the current chapter.
    /// </summary>
    public StoryState WithCurrentChapter(StoryChapter? chapter)
    {
        var newWorldState = chapter != null
            ? WorldState.WithCurrentChapter(chapter.Id)
            : WorldState;

        return this with
        {
            CurrentChapter = chapter,
            WorldState = newWorldState
        };
    }

    /// <summary>
    /// Gets the current state of a character.
    /// </summary>
    public CharacterState? GetCharacter(Id characterId)
    {
        return Characters.TryGetValue(characterId, out var state) ? state : null;
    }

    /// <summary>
    /// Creates a snapshot of this state for persistence.
    /// </summary>
    public StateSnapshot CreateSnapshot()
    {
        return new StateSnapshot(
            Id.New(),
            WorldState.WorldId,
            WorldState.WorldName,
            this,
            DateTime.UtcNow,
            $"Snapshot at chapter {CurrentChapter?.Index ?? -1} with {EventHistory.Count} events");
    }
}

/// <summary>
/// Represents a snapshot of story state for persistence.
/// </summary>
public record StateSnapshot
{
    public Id SnapshotId { get; init; }
    public Id WorldId { get; init; }
    public string WorldName { get; init; }
    public StoryState State { get; init; }
    public DateTime CreatedAt { get; init; }
    public string Description { get; init; }

    public StateSnapshot(Id snapshotId, Id worldId, string worldName, StoryState state, DateTime createdAt, string description)
    {
        SnapshotId = snapshotId;
        WorldId = worldId;
        WorldName = worldName;
        State = state;
        CreatedAt = createdAt;
        Description = description;
    }
}
