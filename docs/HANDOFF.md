# HyoPDF 프로젝트 인수인계 문서

> Claude(또는 다른 AI)가 새 작업을 바로 시작할 수 있도록 작성된 전체 맥락 정리입니다.  
> 작성일: 2026-06-28  
> 저장소: `C:\Users\HyoT\Desktop\work\HyoPDF`

---

## 1. 프로젝트 개요

**HyoPDF**는 Windows용 현대적 WPF PDF 뷰어/편집기입니다.

| 항목 | 내용 |
|------|------|
| 프레임워크 | .NET 8 (`net8.0-windows10.0.19041.0`) |
| UI | WPF + MaterialDesignThemes 5.3.2 |
| MVVM | CommunityToolkit.Mvvm 8.4.2 |
| PDF 엔진 | PdfiumViewer 2.13.0 (NU1701 경고 — .NET Framework 타깃 패키지) |
| 기본 언어 | ko-KR (`Strings.resx` / `Strings.ko.resx` / `Strings.en.resx`) |
| 기본 테마 | Dark (`App.xaml` → `DarkTheme.xaml`) |

### 주요 기능

- PDF 열기/탭, 드래그앤드롭, CLI 인자로 파일 열기
- 연속 스크롤 뷰어 (ListBox + 가상 렌더링)
- 사이드바: 북마크 탭 / 썸네일 탭
- 확대·축소·맞춤·회전·전체화면·검색
- 툴바 페이지 번호 직접 입력 (Enter/Escape)
- 페이지 관리: 삭제, 복사/잘라내기/붙여넣기, 회전, 추출, 이미지 변환
- PDF 병합 / 분할 / 압축 / 인쇄
- 북마크 추가
- Undo/Redo (페이지 스냅샷 기반)
- 다국어 (ko/en), 테마 (Dark/Light/System)
- 로딩 오버레이, 토스트 알림

---

## 2. 솔루션 구조

```
HyoPDF/
├── HyoPDF.sln
├── Directory.Build.props          # 버전 1.0.0
├── docs/
│   ├── README.md
│   └── HANDOFF.md                 # ← 이 문서
├── build/scripts/                 # Publish, Checksums
├── installer/HyoPDF.Installer/    # WiX 인스톨러
└── src/
    ├── HyoPDF.App/                # 진입점, DI, App.xaml
    ├── HyoPDF.Core/               # 서비스, 모델, 설정, PDF 로직
    └── HyoPDF.UI/                 # View, ViewModel, 스타일, 리소스
```

### 프로젝트 역할

| 프로젝트 | 역할 |
|----------|------|
| `HyoPDF.App` | `Host` + `Microsoft.Extensions.DependencyInjection`, `MainWindow` 생성 |
| `HyoPDF.Core` | `IPdfViewerService`, `IPageService`, 설정 저장, 인쇄/압축 등 |
| `HyoPDF.UI` | WPF UI 전체 (Library 프로젝트, exe는 App에서 참조) |

---

## 3. 아키텍처

### MVVM + DI

```
App.OnStartup
  → ServiceCollectionExtensions.AddHyoPdfServices()
  → MainWindow(MainViewModel)
  → DataContext = MainViewModel
```

**탭 모델**: `TabsViewModel`이 `TabItemViewModel` 컬렉션 관리. 각 탭은 독립적인 `ViewerViewModel` + `PageViewModel` + `LazyPdfViewerService` 인스턴스를 가짐 (`TabItemFactory`).

```
MainViewModel
├── Tabs (TabsViewModel)
│   └── TabItemViewModel
│       ├── Viewer (ViewerViewModel)   ← PDF 표시·렌더·검색
│       └── Page (PageViewModel)         ← 페이지 편집·병합·분할
├── Viewer  → ActiveTab.Viewer (프록시)
├── Page    → ActiveTab.Page
└── ActiveTab, RecentFiles, Settings 등
```

### 핵심 서비스 (`ServiceCollectionExtensions.cs`)

| 서비스 | 구현 |
|--------|------|
| `IPdfViewerService` | 탭별 `LazyPdfViewerService` (팩토리에서 생성, DI 미등록) |
| `IPageService` | `PageService` |
| `IPrintService` | `PrintService` |
| `ICompressService` | `CompressService` |
| `IRecentFilesService` | `RecentFilesService` |
| `ILocalSettingsStore` | `LocalSettingsStore` |
| `IUndoRedoStack` | `UndoRedoStack` |
| `ILocalizationService` | `LocalizationService` |
| `IThemeService` | `ThemeService` |
| `IToastService` | `ToastService` |
| `PageClipboardService` | 페이지 클립보드 (싱글톤) |

---

## 4. UI 레이아웃 (`MainWindow.xaml`)

```
Row 0: TitleBarView          — 커스텀 타이틀바 (WindowStyle=None)
Row 1: ToolbarView           — 아이콘 툴바 (가로 스크롤)
Row 2: [Sidebar | Splitter | ViewerArea | PageManager]
Row 3: StatusBarView
Overlay: LoadingOverlay      — Viewer.IsLoading 바인딩
```

| 영역 | 파일 | 설명 |
|------|------|------|
| 사이드바 | `SidebarView.xaml` | 북마크/썸네일 Expander, 접기 버튼 |
| 뷰어 | `ViewerView.xaml` | ListBox 연속 스크롤, 스크롤 페이지 인디케이터 |
| 페이지 관리 | `PageManagerView.xaml` | 우측 패널 (토글) |
| 툴바 | `ToolbarView.xaml` | 열기, 줌, 페이지, 검색, 설정 등 |

`MainWindow.xaml.cs` 주요 기능:
- 사이드바 접기/펼치기 (`ApplySidebarLayout`)
- 창 크기/위치 저장 (`AppSettings.LastWindowSize`)
- 전역 키보드 단축키 (`OnPreviewKeyDown`)
- `OpenFileFromPath(string)` — CLI/드래그용
- `FocusViewerArea()` — 페이지 입력 후 포커스 복귀

---

## 5. PDF 렌더링 파이프라인 (`ViewerViewModel`)

### 문서 로드 흐름

```
LoadDocumentAsync(path)
  1. CancelRenderTasks() + 캐시 클리어
  2. IsLoading = true (LoadingOverlay 표시)
  3. await _pdfViewer.OpenFileAsync(path)
  4. UI 스레드: PageCount, Pages[], CurrentPageIndex, FitToWidth
  5. await RenderPageRangeAsync(0..2)   ← 첫 3페이지만 먼저
  6. IsLoading = false                    ← 오버레이 숨김
  7. _ = GenerateThumbnailsAsync()        ← 백그라운드 썸네일
```

### 렌더 vs 썸네일 취소 토큰 (중요!)

**과거 버그**: `RenderPageRangeAsync`가 썸네일 CTS까지 취소해서 썸네일이 5개만 생성됨.

**현재 구조**:
- `_renderCancellation` — 페이지 본문 렌더 전용 (`CancelPageRenderTasks`)
- `_thumbnailCancellation` — 썸네일 전용 (`CancelThumbnailTasks`)
- `CancelRenderTasks()` — 둘 다 취소 (문서 닫기/새로 열기 시)

`RenderPageRangeAsync`는 **`CancelPageRenderTasks()`만** 호출해야 함.

### 뷰어 표시

- `ViewerView`: `ListBox` + `ScrollViewer`, 스크롤 시 visible range 계산 → `ViewerViewModel.RequestVisiblePageRender()`
- `IsUpdatingFromScroll` 플래그로 스크롤↔CurrentPageIndex 루프 방지
- `ScrollPageIndicator` 오버레이: 스크롤 시 `{N} / {Total} 페이지` fade in/out

### 페이지 인덱스 가드 (최근 수정)

```csharp
// GoToPage — HasDocument, PageCount==0 체크, Clamp
// OnCurrentPageIndexChanged — 범위 밖이면 재설정 후 return
```

툴바 `PageInputBox`는 code-behind에서 1-based 입력 → 0-based index 변환 후 `CurrentPageIndex` 설정.

---

## 6. 사이드바 썸네일 (`SidebarView`)

### 현재 구현

- `ScrollViewer` + `ItemsControl` (`VirtualizingPanel.IsVirtualizing="False"`)
- `ItemsSource="{Binding Viewer.Pages}"` — `PdfPageItemViewModel.Thumbnail`
- 썸네일 null → 회색 `#1E1E1E` 플레이스홀더 (`NullToVisibilityConverter`)
- 커스텀 스크롤바: `SidebarThumbnailScrollBar` (12px, thumb `#484848`, track 클릭 `PageUp`/`PageDown`)
- Ctrl+스크롤: 썸네일 크기 조절 (`MainViewModel.ThumbnailSize`)

### 과거 시도 (실패/롤백됨)

- `TranslateTransform` + 수동 ScrollBar → 스크롤 문제
- `ItemsControl.ActualHeight`가 viewport에 맞춰지는 측정 버그 → `SidebarExpanderFill` Grid `Height="*"` 등으로 해결 시도 후 **ScrollViewer로 복귀**

### 썸네일 선택/컨텍스트 메뉴

- `SidebarView.xaml.cs`: 클릭 → `GoToPageCommand`, 선택 하이라이트 (`BoolToAccentBrushConverter`)
- 컨텍스트 메뉴: 삭제, 회전, 추출, 복사 등 → `PageViewModel` 커맨드 (Window RelativeSource)

---

## 7. 페이지 편집 (`PageViewModel`)

| 커맨드 | 기능 |
|--------|------|
| `DeleteSelectedCommand` | 선택 페이지 삭제 (async, undo 지원) |
| `CopySelectedCommand` / `CutSelectedCommand` / `PasteCommand` | 클립보드 (`PageClipboardService`) |
| `RotateSelectedCommand` | 90/180/270° 회전 |
| `ExtractSelectedCommand` | 선택 페이지를 새 PDF로 추출 |
| `ConvertToImageCommand` | `ImageExportDialog` |
| `MergeCommand` / `SplitCommand` | `MergeDialog` / `SplitDialog` |
| `UndoCommand` / `RedoCommand` | `PageSnapshotCommand` + `ReloadDocument` |

편집 후 `ReloadDocumentAsync`로 뷰어 갱신. 삭제 시 크래시 방지 로직 포함됨.

---

## 8. 스타일·테마

| 파일 | 내용 |
|------|------|
| `Themes/DarkTheme.xaml` / `LightTheme.xaml` | DynamicResource 브러시 |
| `Styles/Colors.xaml` | 색상 토큰 |
| `Styles/Controls.xaml` | `ToolbarButton`, `ZoomTextBox`, `SearchTextBox` 등 |
| `Styles/Scrollbars.xaml` | `ViewerScrollBar` (뷰어용, thumb `#505050`) |
| `Styles/Typography.xaml` | 폰트 |
| `Styles/DialogStyles.xaml` | 다이얼로그 공통 |

뷰어 스크롤바: `ViewerView.xaml` 내부 `ViewerScrollBar` 스타일 (thumb hover `#787878`).

---

## 9. 설정 (`AppSettings` / `LocalSettingsStore`)

```csharp
Theme, Language, DefaultZoom, SidebarVisible,
ThumbnailSize, UserResized, LastWindowSize
```

설정 UI: `SettingsDialog.xaml` + `SettingsViewModel`.

---

## 10. 키보드 단축키 (`MainWindow.xaml.cs`)

| 단축키 | 동작 |
|--------|------|
| Ctrl+O | 파일 열기 |
| Ctrl+W | 탭 닫기 |
| Ctrl+Tab / Ctrl+Shift+Tab | 다음/이전 탭 |
| Ctrl+F | 검색 포커스 |
| Ctrl+P | 인쇄 |
| Ctrl+Z / Ctrl+Y | Undo / Redo |
| Ctrl++ / Ctrl+- / Ctrl+0 | 줌 |
| Ctrl+C/X/V | 페이지 복사/잘라내기/붙여넣기 (사이드바 포커스 시) |
| F11 | 전체화면 |
| ← / → | 이전/다음 페이지 |
| Delete | 선택 페이지 삭제 |
| Escape | EscapeCommand |

텍스트 입력 포커스 시 (`IsTextInputFocused`) 전역 단축키 무시.

---

## 11. 빌드·실행

```powershell
# Debug
dotnet build HyoPDF.sln
dotnet run --project src/HyoPDF.App

# Release (사용자 선호)
dotnet build src/HyoPDF.App/HyoPDF.App.csproj -c Release

# 실행
Start-Process "src\HyoPDF.App\bin\Release\net8.0-windows10.0.19041.0\HyoPDF.exe"

# CLI로 PDF 열기
HyoPDF.exe "C:\path\to\file.pdf"
```

**주의**: HyoPDF.exe가 실행 중이면 DLL 파일 잠금으로 빌드 실패.

**사용자 관행**: 작업 완료 후 Release 빌드 → exe 실행 → 완료 시 `[System.Media.SystemSounds]::Asterisk.Play()`.

---

## 12. Git 상태 (2026-06-28 기준)

### 커밋 이력

```
444db70 fix(release): restore with PublishReadyToRun for CI publish
d1fa30f Initial commit: HyoPDF WPF PDF viewer with release pipeline
```

### 미커밋 변경 (대량)

`git status` 기준 **50+ 수정 파일**, **20+ 신규 파일** — 대부분의 UI/기능 개선이 **아직 커밋되지 않음**.

주요 신규 파일:
- `LazyPdfViewerService.cs`, `PageClipboardService.cs`, `PdfBookmarkWriter.cs`
- `LoadingOverlay.xaml`, `ImageExportDialog.xaml`, `BookmarkNameDialog.xaml`
- `Styles/Scrollbars.xaml`, `Styles/DialogStyles.xaml`
- 다수 Converter (`NullToVisibilityConverter`, `BoolToAccentBrushConverter` 등)
- `ImageExportViewModel.cs`, `MergeViewModel.cs` 등

---

## 13. 최근 완료 작업 (Cursor 세션 요약)

### 썸네일
- [x] 백그라운드 순차 생성 (`GenerateThumbnailsAsync`)
- [x] 렌더/썸네일 CTS 분리 (5개만 로드되던 버그 수정)
- [x] null 썸네일 플레이스홀더
- [x] ScrollViewer + 커스텀 스크롤바 (track 클릭, 12px thumb)

### 뷰어
- [x] 스크롤 페이지 인디케이터 오버레이
- [x] 첫 페이지 렌더 후 로딩 오버레이 숨김
- [x] 커스텀 뷰어 스크롤바

### 툴바
- [x] 페이지 번호 `TextBox` (`PageInputBox`) — Enter/Escape/LostFocus
- [x] `GoToPage` / `CurrentPageIndex` 범위 가드

### 기타
- [x] CLI PDF 열기 (`App.xaml.cs` args → `MainWindow.OpenFileFromPath`)
- [x] 로딩 오버레이 (`LoadingOverlay`)
- [x] 이미지보내기 다이얼로그
- [x] 페이지 삭제 크래시 수정, 복사 컨텍스트 메뉴
- [x] Compress/Merge/Split 다이얼로그 UI 개선
- [x] 사이드바 접기/펼치기, 북마크 기능

---

## 14. 핵심 파일 빠른 참조

| 작업 영역 | 파일 |
|-----------|------|
| PDF 엔진 | `Core/Services/PdfViewerService.cs`, `IPdfViewerService.cs` |
| 뷰어 VM | `UI/ViewModels/ViewerViewModel.cs` |
| 페이지 편집 VM | `UI/ViewModels/PageViewModel.cs` |
| 메인 VM | `UI/ViewModels/MainViewModel.cs` |
| 탭 | `UI/ViewModels/TabsViewModel.cs`, `Services/TabItemFactory.cs` |
| 뷰어 UI | `UI/Views/ViewerView.xaml(.cs)` |
| 사이드바 | `UI/Views/SidebarView.xaml(.cs)` |
| 툴바 | `UI/Views/ToolbarView.xaml(.cs)` |
| 메인 창 | `UI/Views/MainWindow.xaml(.cs)` |
| 문자열 | `UI/Resources/Strings.resx`, `.ko.resx`, `.en.resx` |
| DI | `App/DependencyInjection/ServiceCollectionExtensions.cs` |

---

## 15. 개발 시 주의사항

1. **스레드**: PDF 렌더는 백그라운드, UI 업데이트는 `Dispatcher.InvokeAsync`. 예외가 UI로 전파되지 않게 처리.
2. **CancellationToken**: 페이지 렌더와 썸네일 렌더 토큰을 혼동하지 말 것.
3. **CommunityToolkit `[ObservableProperty]`**: `OnXxxChanging(ref int value)`는 생성기 버전에 따라 없을 수 있음. 8.4.2에서는 `OnCurrentPageIndexChanged`에서 clamp 패턴 사용 중.
4. **탭별 Viewer**: `MainViewModel.Viewer`는 활성 탭 프록시. 직접 필드 접근 시 탭 전환 wiring 확인 (`WireActiveTab` 등).
5. **WPF 측정**: 사이드바/썸네일 레이아웃은 `VerticalAlignment="Stretch"`, Expander `Grid Height="*"` 등이 중요.
6. **최소 변경 원칙**: 사용자는 focused diff 선호. 관련 없는 리팩터링 금지.
7. **커밋**: 사용자가 명시적으로 요청할 때만 git commit.

---

## 16. 알려진 이슈 / 미완료 가능 영역

- PdfiumViewer NU1701 호환성 경고 (빌드는 성공)
- 대량 변경이 미커밋 상태 — 브랜치/PR 정리 필요할 수 있음
- 썸네일 스크롤바는 여러 번의 template 시도 끝에 ScrollViewer + scoped style로 안정화 — 추가 커스터마이징 시 회귀 주의
- Release exe 실행 중 빌드 파일 잠금

---

## 17. 새 작업 시작 체크리스트

```powershell
cd C:\Users\HyoT\Desktop\work\HyoPDF
git status
dotnet build src/HyoPDF.App/HyoPDF.App.csproj -c Release
```

1. 위 HANDOFF + `git diff`로 현재 상태 확인
2. 관련 View/ViewModel/Service 파일 먼저 읽기
3. WPF 변경 후 Release 빌드
4. exe 실행으로 수동 검증
5. 커밋은 사용자 요청 시에만

---

## 18. 대화 이력

Cursor Agent 세션 전체 기록:  
`C:\Users\HyoT\.cursor\projects\c-Users-HyoT-Desktop-work-HyoPDF\agent-transcripts\623beb5c-2ca0-4d43-aa1e-58f1e9514ea3\623beb5c-2ca0-4d43-aa1e-58f1e9514ea3.jsonl`

세부 구현 결정(썸네일 스크롤 진단, scrollbar template 시도 등)은 위 transcript에서 키워드 검색으로 확인 가능.
