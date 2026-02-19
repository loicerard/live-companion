namespace LiveCompanion.App.Services;

/// <summary>
/// Controls which view is displayed in the main content area.
/// </summary>
public interface INavigationService
{
    /// <summary>Fired when the active view changes.</summary>
    event Action<ViewKey>? NavigatedTo;

    /// <summary>Current active view.</summary>
    ViewKey CurrentView { get; }

    /// <summary>Navigates to the specified view.</summary>
    void NavigateTo(ViewKey view);
}

/// <summary>Named views available for navigation.</summary>
public enum ViewKey
{
    Performance,
    Setlist,
    Config,
    Setup,
}
