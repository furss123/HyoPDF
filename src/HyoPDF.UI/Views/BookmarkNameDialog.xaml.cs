using System.Windows;
using System.Windows.Input;

namespace HyoPDF.UI.Views;

public partial class BookmarkNameDialog : Window
{
    public static readonly DependencyProperty BookmarkTitleProperty =
        DependencyProperty.Register(
            nameof(BookmarkTitle),
            typeof(string),
            typeof(BookmarkNameDialog),
            new PropertyMetadata(string.Empty));

    public string BookmarkTitle
    {
        get => (string)GetValue(BookmarkTitleProperty);
        set => SetValue(BookmarkTitleProperty, value);
    }

    public BookmarkNameDialog()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            TitleTextBox.Focus();
            TitleTextBox.SelectAll();
        };
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(BookmarkTitle))
            return;

        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
            e.Handled = true;
        }
    }
}
