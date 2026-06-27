using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace HyoPDF.UI.Services;

public sealed class ToastService : IToastService
{
    private const int DismissMilliseconds = 3000;
    private readonly Dispatcher _dispatcher;

    public ToastService()
    {
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        Messages = new ObservableCollection<ToastMessage>();
    }

    public ObservableCollection<ToastMessage> Messages { get; }

    public event EventHandler? MessagesChanged;

    public void Show(string message, ToastType type = ToastType.Info)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        _dispatcher.Invoke(() =>
        {
            var toast = new ToastMessage { Message = message, Type = type };
            Messages.Add(toast);
            MessagesChanged?.Invoke(this, EventArgs.Empty);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DismissMilliseconds) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                Messages.Remove(toast);
                MessagesChanged?.Invoke(this, EventArgs.Empty);
            };
            timer.Start();
        });
    }
}
