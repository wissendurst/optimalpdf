# Optimal PDF Reader

Minimal Windows PDF reader with no ads, no accounts, and no extra AI panels.

Optimal PDF Reader is a small WinForms app that uses Microsoft Edge WebView2 for PDF rendering and adds a clean native shell around it: open, print, close, zoom controls, page/file indicators, and selected-area snapshots.

## Features

- Native Windows Open dialog and drag-and-drop.
- Zoom in, zoom out, and live zoom percentage.
- Page count and file size indicators while a PDF is open.
- Snapshot tool with drag-to-select area export as PNG.
- Small installer UI with Start Menu option and PDF app registration.
- Self-contained Windows build option for users who do not have .NET installed.

## Requirements

- Windows 10 or newer.
- .NET SDK 10 to build from source.
- Microsoft Edge WebView2 runtime for PDF rendering. It is usually already present with Microsoft Edge.

## Build

From the repository root:

```powershell
.\build.ps1
```

The installer will be written to `dist\OptimalPDFReaderSetup.exe`.

## Source Layout

- `src/` - Optimal PDF Reader app.
- `installer/` - single-file setup app.
- `assets/` - app icon.

## License

MIT
