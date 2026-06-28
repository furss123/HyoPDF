# HyoPDF 배포 · HyoT Software Center 게시 가이드

## 1. GitHub 릴리스 (자동)

태그를 푸시하면 `.github/workflows/release.yml`이 x64/ARM64 빌드 후 GitHub Release를 생성합니다.

```powershell
git push origin main
git tag v1.0.0
git push origin v1.0.0
```

| 항목 | 값 |
|------|-----|
| 저장소 | https://github.com/furss123/HyoPDF |
| 안정 채널 태그 | `v1.0.0` |
| 베타 채널 태그 | `v1.0.0-beta.2` (예시) |
| 다운로드 | https://github.com/furss123/HyoPDF/releases |

`SOFTWARE_CENTER_PAT` 시크릿이 설정되어 있으면 `hyot-software-center`의 `releases.json`도 자동 갱신됩니다.

---

## 2. Software Center `meta.json` (수동 반영)

경로: `hyot-software-center` 저장소 → `data/software/hyopdf/meta.json`

아래 JSON을 복사해 붙여넣거나 기존 파일과 병합하세요. (`icon`, `banner`, `screenshots` 파일은 별도 업로드 필요)

```json
{
  "slug": "hyopdf",
  "status": "active",
  "category": "utility",
  "tags": [
    "windows",
    "pdf",
    "viewer",
    "editor",
    "desktop",
    "wpf",
    "korean",
    "windows-11",
    "windows-10"
  ],
  "featured": true,
  "githubRepo": "furss123/HyoPDF",
  "icon": "/data/software/hyopdf/icon.webp",
  "banner": "/data/software/hyopdf/banner.webp",
  "screenshots": [
    {
      "file": "screenshots/main.png",
      "alt": {
        "ko": "HyoPDF 메인 화면 — 탭 뷰어와 툴바",
        "en": "HyoPDF main window with tabs and toolbar"
      }
    },
    {
      "file": "screenshots/print.png",
      "alt": {
        "ko": "인쇄 대화상자",
        "en": "Print dialog"
      }
    }
  ],
  "name": {
    "ko": "HyoPDF",
    "en": "HyoPDF"
  },
  "description": {
    "ko": "HyoPDF는 Windows 10/11용 탭 기반 PDF 뷰어·편집기입니다. 여러 PDF를 탭으로 열고, 북마크·텍스트 검색·페이지 관리(병합·분할·회전·삭제·복사·붙여넣기), 인쇄, 압축, 이미지보내기를 지원합니다. 다크/라이트 테마, 한국어/영어 UI, x64·ARM64 설치 프로그램(MSI) 및 포터블(EXE)을 제공합니다.",
    "en": "HyoPDF is a tabbed PDF viewer and editor for Windows 10/11. Open multiple PDFs in tabs, use bookmarks and search, manage pages (merge, split, rotate, delete, copy/paste), print, compress, and export pages as images. Includes dark/light themes, Korean/English UI, and x64/ARM64 MSI installers plus portable EXE builds."
  },
  "shortDescription": {
    "ko": "Windows 10/11용 탭 기반 PDF 뷰어 및 편집기",
    "en": "Tabbed PDF viewer and editor for Windows 10/11"
  },
  "requirements": {
    "os": "Windows 10 19041+ / Windows 11 (x64, ARM64)",
    "ram": "4 GB 이상 권장",
    "disk": "약 200 MB (설치 후)"
  },
  "links": {
    "github": "https://github.com/furss123/HyoPDF"
  },
  "createdAt": "2026-06-27",
  "updatedAt": "2026-06-28"
}
```

### 에셋 체크리스트

| 파일 | 권장 크기 | 비고 |
|------|-----------|------|
| `data/software/hyopdf/icon.webp` | 256×256 | `assets/icons/app-icon-512.png`에서 변환 |
| `data/software/hyopdf/banner.webp` | 1200×400 | 메인 UI 스크린샷 크롭 |
| `data/software/hyopdf/screenshots/main.png` | 1280×720 | 빈 상태 또는 문서 열린 화면 |
| `data/software/hyopdf/screenshots/print.png` | 1280×720 | 인쇄 대화상자 (선택) |

---

## 3. GitHub Release 노트 (복사용)

### 한국어

```markdown
## HyoPDF 1.0.0

Windows 10/11용 탭 기반 PDF 뷰어·편집기 첫 정식(stable) 릴리스입니다.

### 주요 기능
- **탭 뷰어** — 여러 PDF를 탭으로 열기, 최근 파일, 드래그 앤 드롭
- **페이지 도구** — 병합, 분할, 회전, 삭제, 복사/붙여넣기, 실행 취소
- **북마크** — 사이드바 북마크 탐색 및 추가
- **검색** — 문서 내 텍스트 검색
- **인쇄** — 미리보기, 페이지 범위, 맞춤/배율, 양면 인쇄
- **압축** — 5단계 품질 슬라이더, 예상 용량 표시
- **이미지보내기** — 페이지를 PNG/JPEG/WebP로 저장
- **UI** — 다크/라이트 테마, 한국어/영어, 커스텀 아이콘

### 다운로드
| 아키텍처 | 포터블 (EXE) | 설치 프로그램 (MSI) |
|----------|--------------|---------------------|
| x64 | HyoPDF-1.0.0-x64.exe | HyoPDF-1.0.0-x64.msi |
| ARM64 | HyoPDF-1.0.0-arm64.exe | HyoPDF-1.0.0-arm64.msi |

각 파일 옆 `.sha256` 체크섬 파일을 함께 제공합니다.

### 시스템 요구 사항
- Windows 10 (빌드 19041+) 또는 Windows 11
- x64 또는 ARM64
```

### English

```markdown
## HyoPDF 1.0.0

First stable release of the tabbed PDF viewer and editor for Windows 10/11.

### Highlights
- **Tabbed viewer** — multiple PDFs, recent files, drag and drop
- **Page tools** — merge, split, rotate, delete, copy/paste, undo
- **Bookmarks** — sidebar navigation and bookmark creation
- **Search** — in-document text search
- **Print** — preview, page range, fit/scale, duplex
- **Compress** — 5-level quality slider with size estimate
- **Image export** — save pages as PNG/JPEG/WebP
- **UI** — dark/light themes, Korean/English, custom icons

### Downloads
| Architecture | Portable (EXE) | Installer (MSI) |
|--------------|----------------|-----------------|
| x64 | HyoPDF-1.0.0-x64.exe | HyoPDF-1.0.0-x64.msi |
| ARM64 | HyoPDF-1.0.0-arm64.exe | HyoPDF-1.0.0-arm64.msi |

SHA256 checksum files (`.sha256`) are included for each asset.

### Requirements
- Windows 10 (build 19041+) or Windows 11
- x64 or ARM64
```

---

## 4. 홈페이지 최근 업데이트 카드 (참고)

Software Center 메인·소프트웨어 목록에 표시되는 항목은 `releases.json`의 `latest.stable` 값을 따릅니다. 자동 동기화 후 예시:

| 필드 | 값 |
|------|-----|
| slug | `hyopdf` |
| channel | `stable` |
| version | `1.0.0` |
| releaseDate | `2026-06-28` |
| 페이지 URL | https://furss123.github.io/hyot-software-center/ko/software/hyopdf/ |

---

## 5. 배포 후 확인

- [ ] GitHub Actions Release 워크플로우 성공
- [ ] https://github.com/furss123/HyoPDF/releases 에 8개 파일 (exe/msi × 2 arch + sha256 × 4)
- [ ] `hyot-software-center` `releases.json`에 `latest.stable: "1.0.0"` 반영
- [ ] Software Center 페이지에서 다운로드 링크 동작
- [ ] MSI/EXE 설치 후 앱 실행·정보 창 버전 `1.0.0` 확인
