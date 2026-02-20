namespace LiveCompanion.App.Services;

/// <summary>
/// Abstracts the UI dispatcher so ViewModels can post to the UI thread
/// without taking a direct dependency on WPF types.
/// </summary>
public interface IDispatcher
{
    /// <summary>
    /// Invokes <paramref name="action"/> on the UI thread.
    /// Returns immediately if already on the UI thread.
    /// </summary>
    void Invoke(Action action);

    /// <summary>Posts <paramref name="action"/> to the UI thread asynchronously.</summary>
    void Post(Action action);
}
