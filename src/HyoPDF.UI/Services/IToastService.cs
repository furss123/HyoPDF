using System.Collections.ObjectModel;

namespace HyoPDF.UI.Services;

public enum ToastType
{
    Success,
    Error,
    Info
}

public sealed class ToastMessage
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Message { get; init; } = string.Empty;
    public ToastType Type { get; init; }
}

public interface IToastService
{
    ObservableCollection<ToastMessage> Messages { get; }
    event EventHandler? MessagesChanged;
    void Show(string message, ToastType type = ToastType.Info);
}
