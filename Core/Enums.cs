namespace Narratum.Core;

/// <summary>
/// Enumerates the possible vital statuses of a character.
/// </summary>
public enum VitalStatus
{
    /// <summary>Character is alive.</summary>
    Alive = 0,

    /// <summary>Character is dead.</summary>
    Dead = 1,

    /// <summary>Character's vital status is unknown.</summary>
    Unknown = 2
}

/// <summary>
/// Enumerates the possible states of an arc or chapter.
/// </summary>
public enum StoryProgressStatus
{
    /// <summary>Not yet started.</summary>
    NotStarted = 0,

    /// <summary>Currently in progress.</summary>
    InProgress = 1,

    /// <summary>Completed.</summary>
    Completed = 2,

    /// <summary>Abandoned or cancelled.</summary>
    Abandoned = 3
}
