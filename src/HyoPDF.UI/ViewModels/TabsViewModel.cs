using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HyoPDF.UI.Services;

namespace HyoPDF.UI.ViewModels;

public partial class TabsViewModel : ObservableObject
{
    private readonly TabItemFactory _factory;

    [ObservableProperty]
    private TabItemViewModel? _activeTab;

    public ObservableCollection<TabItemViewModel> Tabs { get; } = [];

    public event EventHandler? ActiveTabChanged;

    public TabsViewModel(TabItemFactory factory)
    {
        _factory = factory;
        EnsureWelcomeTab();
    }

    public void OpenNewTab(string path)
    {
        if (!File.Exists(path))
            return;

        var existing = Tabs.FirstOrDefault(t =>
            !string.IsNullOrEmpty(t.FilePath) &&
            string.Equals(t.FilePath, path, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            SwitchTab(existing);
            return;
        }

        RemoveWelcomeTabIfNeeded();

        var tab = _factory.CreateWithFile(path);
        Tabs.Add(tab);
        SwitchTab(tab);
    }

    [RelayCommand]
    public void CloseTab(TabItemViewModel? tab)
    {
        tab ??= ActiveTab;
        if (tab is null)
            return;

        tab.Viewer.CloseDocument();
        tab.Page.OnDocumentClosed();
        Tabs.Remove(tab);

        if (ActiveTab is null || !Tabs.Contains(ActiveTab))
            ActiveTab = Tabs.LastOrDefault();

        EnsureWelcomeTab();
    }

    [RelayCommand]
    public void SwitchTab(TabItemViewModel? tab)
    {
        if (tab is null || !Tabs.Contains(tab))
            return;

        ActiveTab = tab;
    }

    [RelayCommand]
    public void SwitchToNextTab()
    {
        if (Tabs.Count <= 1 || ActiveTab is null)
            return;

        var index = Tabs.IndexOf(ActiveTab);
        var next = (index + 1) % Tabs.Count;
        ActiveTab = Tabs[next];
    }

    [RelayCommand]
    public void SwitchToPreviousTab()
    {
        if (Tabs.Count <= 1 || ActiveTab is null)
            return;

        var index = Tabs.IndexOf(ActiveTab);
        var previous = (index - 1 + Tabs.Count) % Tabs.Count;
        ActiveTab = Tabs[previous];
    }

    partial void OnActiveTabChanged(TabItemViewModel? value) =>
        ActiveTabChanged?.Invoke(this, EventArgs.Empty);

    private void EnsureWelcomeTab()
    {
        if (Tabs.Count > 0)
            return;

        var tab = _factory.CreateWelcome();
        Tabs.Add(tab);
        ActiveTab = tab;
    }

    private void RemoveWelcomeTabIfNeeded()
    {
        if (Tabs.Count != 1 || ActiveTab?.FilePath is not null)
            return;

        var welcome = Tabs[0];
        Tabs.Clear();
        welcome.Viewer.CloseDocument();
        welcome.Page.OnDocumentClosed();
    }
}
