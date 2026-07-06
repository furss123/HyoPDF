# HyoPDF – Cursor Agent Prompt

## Project
WPF PDF viewer/editor. .NET 8, WPF + MaterialDesignThemes, CommunityToolkit.Mvvm, PdfiumViewer.
Repo: `C:\Users\HyoT\Desktop\work\HyoPDF`

## Structure
```
src/
  HyoPDF.App/   – entry point, DI, App.xaml
  HyoPDF.Core/  – services, models, PDF logic
  HyoPDF.UI/    – Views, ViewModels, styles
```

## Architecture
MVVM + DI. `MainViewModel` → `TabsViewModel` → `TabItemViewModel` (each tab has its own `ViewerViewModel` + `PageViewModel` + `LazyPdfViewerService`).

## Key Files
| Area | File |
|------|------|
| PDF engine | `Core/Services/PdfViewerService.cs` |
| Viewer VM | `UI/ViewModels/ViewerViewModel.cs` |
| Page edit VM | `UI/ViewModels/PageViewModel.cs` |
| Main VM | `UI/ViewModels/MainViewModel.cs` |
| Main window | `UI/Views/MainWindow.xaml(.cs)` |
| Sidebar | `UI/Views/SidebarView.xaml(.cs)` |
| DI setup | `App/DependencyInjection/ServiceCollectionExtensions.cs` |

## Critical Rules
1. **Two separate CancellationTokens**: `_renderCancellation` (page render) and `_thumbnailCancellation` (thumbnails). Never cancel both when only page render needs cancelling — this was the bug that caused only 5 thumbnails to load.
2. **Thread safety**: PDF render on background thread; UI updates via `Dispatcher.InvokeAsync`.
3. **Tab proxy**: `MainViewModel.Viewer` is a proxy to the active tab. Verify tab-switching wiring before accessing directly.
4. **WPF layout**: Sidebar/thumbnail layout depends on `VerticalAlignment="Stretch"` and `Grid Height="*"` — fragile, don't touch unless necessary.
5. **Minimal diffs**: No unrelated refactoring. Change only what's needed.
6. **No commits** unless explicitly requested.

## Build & Run
```powershell
cd C:\Users\HyoT\Desktop\work\HyoPDF
dotnet build src/HyoPDF.App/HyoPDF.App.csproj -c Release
Start-Process "src\HyoPDF.App\bin\Release\net8.0-windows10.0.19041.0\HyoPDF.exe"
```
Kill the exe before rebuilding (DLL lock).

## Before Starting Any Task
1. Read relevant View/ViewModel/Service files first.
2. Make change → Release build → manual verify with exe.

## See Also
- Full handoff: `docs/HANDOFF.md`
