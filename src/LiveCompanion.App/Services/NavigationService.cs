namespace LiveCompanion.App.Services;

/// <summary>
/// Simple navigation service. Notifies subscribers when the active view changes.
/// </summary>
public sealed class NavigationService : INavigationService
{
    public event Action<ViewKey>? NavigatedTo;

    public ViewKey CurrentView { get; private set; } = ViewKey.Setlist;

    public void NavigateTo(ViewKey view)
    {
        if (CurrentView == view) return;
        CurrentView = view;
        NavigatedTo?.Invoke(view);
    }
}
