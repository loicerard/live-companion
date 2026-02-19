using System.Windows;
using System.Windows.Threading;

namespace LiveCompanion.App.Services;

/// <summary>
/// WPF implementation of <see cref="IDispatcher"/> backed by
/// <see cref="Application.Current.Dispatcher"/>.
/// </summary>
public sealed class WpfDispatcher : IDispatcher
{
    private readonly Dispatcher _dispatcher;

    public WpfDispatcher()
    {
        _dispatcher = Application.Current.Dispatcher;
    }

    public void Invoke(Action action)
    {
        if (_dispatcher.CheckAccess())
            action();
        else
            _dispatcher.Invoke(action);
    }

    public void Post(Action action)
    {
        _dispatcher.BeginInvoke(action);
    }
}
