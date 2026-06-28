using System.Windows;
using System.Windows.Controls;

namespace HyoPDF.UI.Views;

public partial class LoadingOverlay : UserControl
{
    public static readonly DependencyProperty IsShownProperty =
        DependencyProperty.Register(
            nameof(IsShown),
            typeof(bool),
            typeof(LoadingOverlay),
            new PropertyMetadata(false));

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(
            nameof(Message),
            typeof(string),
            typeof(LoadingOverlay),
            new PropertyMetadata(string.Empty));

    public bool IsShown
    {
        get => (bool)GetValue(IsShownProperty);
        set => SetValue(IsShownProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public LoadingOverlay()
    {
        InitializeComponent();
    }
}
