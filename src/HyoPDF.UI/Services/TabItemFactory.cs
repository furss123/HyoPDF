using HyoPDF.Core.Localization;
using HyoPDF.Core.Services;
using HyoPDF.UI.ViewModels;

namespace HyoPDF.UI.Services;

public sealed class TabItemFactory
{
    private readonly IPageService _pageService;
    private readonly IToastService _toastService;
    private readonly ILocalizationService _localization;
    private readonly PageClipboardService _clipboardService;

    public TabItemFactory(
        IPageService pageService,
        IToastService toastService,
        ILocalizationService localization,
        PageClipboardService clipboardService)
    {
        _pageService = pageService;
        _toastService = toastService;
        _localization = localization;
        _clipboardService = clipboardService;
    }

    public TabItemViewModel CreateWelcome() => CreateInternal(null);

    public TabItemViewModel CreateWithFile(string path)
    {
        var tab = CreateInternal(path);
        tab.Viewer.LoadDocument(path);
        return tab;
    }

    private TabItemViewModel CreateInternal(string? path)
    {
        var pdfViewer = new LazyPdfViewerService();
        var viewer = new ViewerViewModel(pdfViewer, _toastService, _localization);
        var page = new PageViewModel(_pageService, pdfViewer, viewer, _toastService, _localization, _clipboardService);
        return new TabItemViewModel(viewer, page) { FilePath = path };
    }
}
