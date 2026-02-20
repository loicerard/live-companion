using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveCompanion.App.Services;

namespace LiveCompanion.App.ViewModels;

/// <summary>
/// Non-blocking overlay notification (audio / MIDI fault banner).
/// Auto-dismisses after 5 seconds.
/// </summary>
public sealed partial class NotificationViewModel : ObservableObject
{
    private readonly IDispatcher _dispatcher;
    private CancellationTokenSource? _dismissCts;

    public NotificationViewModel(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _isError = true;

    public void ShowError(string message) => Show(message, isError: true);
    public void ShowWarning(string message) => Show(message, isError: false);

    private void Show(string message, bool isError)
    {
        _dispatcher.Invoke(() =>
        {
            Message    = message;
            IsError    = isError;
            IsVisible  = true;
        });

        _dismissCts?.Cancel();
        _dismissCts = new CancellationTokenSource();
        var ct = _dismissCts.Token;

        _ = Task.Delay(5000, ct).ContinueWith(t =>
        {
            if (!t.IsCanceled)
                _dispatcher.Post(Dismiss);
        }, TaskScheduler.Default);
    }

    [RelayCommand]
    private void Dismiss()
    {
        _dismissCts?.Cancel();
        IsVisible = false;
    }
}
