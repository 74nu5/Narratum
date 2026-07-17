namespace Narratum.Web.Services;

/// <summary>
/// Service for managing Expert Mode visibility and data access.
/// Provides toggle state and access to raw LLM outputs, prompts, and state data.
/// </summary>
public class ExpertModeService
{
    private bool _isExpertModeEnabled = false;

    public bool IsExpertModeEnabled
    {
        get => _isExpertModeEnabled;
        set
        {
            if (_isExpertModeEnabled == value)
                return;
            _isExpertModeEnabled = value;
            OnExpertModeToggled?.Invoke();
        }
    }

    public event Action? OnExpertModeToggled;

    public void ToggleExpertMode() => IsExpertModeEnabled = !_isExpertModeEnabled;
}
