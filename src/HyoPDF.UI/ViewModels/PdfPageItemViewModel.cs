using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HyoPDF.UI.ViewModels;

public partial class PdfPageItemViewModel : ObservableObject
{
    [ObservableProperty]
    private int _pageIndex;

    [ObservableProperty]
    private int _pageNumber;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isCut;

    [ObservableProperty]
    private ImageSource? _thumbnail;

    [ObservableProperty]
    private ImageSource? _pageImage;

    [ObservableProperty]
    private double _displayWidth;

    [ObservableProperty]
    private double _displayHeight;

    public ObservableCollection<Rect> HighlightRects { get; } = [];
}
