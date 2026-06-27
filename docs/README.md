# HyoPDF

A modern WPF PDF viewer for Windows (.NET 8).

## Structure

- `src/HyoPDF.App` — Application entry point and DI composition
- `src/HyoPDF.Core` — Business logic, services, settings
- `src/HyoPDF.UI` — Views, ViewModels, styles, and themes

## Build

```bash
dotnet build HyoPDF.sln
dotnet run --project src/HyoPDF.App
```

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Ctrl+O | Open file |
| Ctrl+W | Close tab |
| Ctrl+F | Focus search |
| Ctrl++ / Ctrl+- | Zoom in/out |
| F11 | Toggle fullscreen |
| Ctrl+Z / Ctrl+Y | Undo / Redo |
