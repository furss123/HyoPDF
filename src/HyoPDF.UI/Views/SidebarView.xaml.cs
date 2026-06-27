using System.Windows;
using System.Windows.Controls;
using HyoPDF.Core.Models;
using HyoPDF.UI.ViewModels;

namespace HyoPDF.UI.Views;

public partial class SidebarView : UserControl
{
    public SidebarView()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void OnBookmarkSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is BookmarkItem bookmark)
            ViewModel?.Viewer.GoToBookmark(bookmark);
    }

    private void OnThumbnailSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThumbnailList.SelectedItem is PdfPageItemViewModel page)
            ViewModel?.Viewer.GoToPageCommand.Execute(page.PageIndex);
    }
}
