namespace Narratum.Web.Models;

/// <summary>
/// A selectable LLM model: the id sent to the provider, plus a human label.
/// </summary>
public record ModelOption(string Id, string Label);
