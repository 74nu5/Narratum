namespace Narratum.Core;

/// <summary>
/// Represents a unique identifier.
/// </summary>
public record Id(Guid Value)
{
    public static Id New() => new(Guid.NewGuid());
    public static Id From(Guid value) => new(value);
}
