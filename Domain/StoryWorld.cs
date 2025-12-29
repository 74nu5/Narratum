using Narratum.Core;

namespace Narratum.Domain;

/// <summary>
/// Represents a unique story world - the top-level container for all narrative elements.
/// </summary>
public class StoryWorld
{
    public Id Id { get; }
    public string Name { get; }
    public string Description { get; }
    public IReadOnlyList<IStoryRule> GlobalRules { get; private set; } = [];
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Creates a new story world with the specified name.
    /// </summary>
    public StoryWorld(string name, string description = "")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        Id = Id.New();
        Name = name;
        Description = description;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Internal constructor for deserialization.
    /// </summary>
    internal StoryWorld(Id id, string name, string description, IReadOnlyList<IStoryRule> globalRules, DateTime createdAt)
    {
        Id = id;
        Name = name;
        Description = description;
        GlobalRules = globalRules;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Adds a global rule that applies to all actions in this world.
    /// </summary>
    public void AddGlobalRule(IStoryRule rule)
    {
        if (rule == null)
            throw new ArgumentNullException(nameof(rule));

        var rules = GlobalRules.ToList();
        rules.Add(rule);
        GlobalRules = rules.AsReadOnly();
    }

    /// <summary>
    /// Removes a global rule by name.
    /// </summary>
    public void RemoveGlobalRule(string ruleName)
    {
        if (string.IsNullOrWhiteSpace(ruleName))
            throw new ArgumentException("Rule name cannot be empty.", nameof(ruleName));

        var rules = GlobalRules.Where(r => r.Name != ruleName).ToList();
        GlobalRules = rules.AsReadOnly();
    }
}
