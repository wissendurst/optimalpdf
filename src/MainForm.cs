using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace SimplePdfReaderWin;

public sealed class MainForm : Form
{
    private readonly ToolStrip _toolbar = new();
    private readonly ToolStripButton _openButton = new("Open");
    private readonly ToolStripButton _zoomOutButton = new("Zoom -");
    private readonly ToolStripButton _zoomResetButton = new("100%");
    private readonly ToolStripButton _zoomInButton = new("Zoom +");
    private readonly ToolStripButton _snapshotButton = new("Snapshot");
    private readonly ToolStripButton _printButton = new("Print");
    private readonly ToolStripButton _closeButton = new("Close");
    private readonly ToolStripLabel _pageLabel = new("Page 1 / 1");
    private readonly ToolStripLabel _fileLabel = new("No PDF open");
    private readonly Label _emptyLabel = new();
    private readonly WebView2 _viewer = new();
    private readonly System.Windows.Forms.Timer _pageTimer = new();

    private string? _currentFile;
    private int _pageCount = 1;
    private bool _pageCheckRunning;

    public MainForm(string? initialFile)
    {
        Text = "Optimal PDF Reader";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon;
        Width = 1100;
        Height = 800;
        MinimumSize = new System.Drawing.Size(720, 480);
        AllowDrop = true;

        BuildToolbar();
        BuildViewer();
        WireEvents();

        Shown += async (_, _) =>
        {
            await EnsureViewerAsync();
            if (!string.IsNullOrWhiteSpace(initialFile))
            {
                OpenPdf(initialFile);
            }
        };
    }

    private void BuildToolbar()
    {
        _toolbar.GripStyle = ToolStripGripStyle.Hidden;
        _toolbar.Padding = new Padding(8, 6, 8, 6);
        _toolbar.ImageScalingSize = new System.Drawing.Size(16, 16);

        _openButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _zoomOutButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _zoomResetButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _zoomInButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _snapshotButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _printButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _closeButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _zoomOutButton.Enabled = false;
        _zoomResetButton.Enabled = false;
        _zoomInButton.Enabled = false;
        _snapshotButton.Enabled = false;
        _printButton.Enabled = false;
        _closeButton.Enabled = false;

        _pageLabel.Visible = false;
        _pageLabel.Margin = new Padding(10, 1, 4, 2);

        _fileLabel.Alignment = ToolStripItemAlignment.Right;
        _fileLabel.AutoSize = false;
        _fileLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
        _fileLabel.Width = 300;

        _toolbar.Items.Add(_openButton);
        _toolbar.Items.Add(new ToolStripSeparator());
        _toolbar.Items.Add(_zoomOutButton);
        _toolbar.Items.Add(_zoomResetButton);
        _toolbar.Items.Add(_zoomInButton);
        _toolbar.Items.Add(_snapshotButton);
        _toolbar.Items.Add(new ToolStripSeparator());
        _toolbar.Items.Add(_printButton);
        _toolbar.Items.Add(_closeButton);
        _toolbar.Items.Add(new ToolStripSeparator());
        _toolbar.Items.Add(_pageLabel);
        _toolbar.Items.Add(_fileLabel);

        Controls.Add(_toolbar);
    }

    private void BuildViewer()
    {
        _emptyLabel.Dock = DockStyle.Fill;
        _emptyLabel.Text = "Open a PDF to start reading.";
        _emptyLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
        _emptyLabel.Font = new System.Drawing.Font(Font.FontFamily, 18);

        _viewer.Dock = DockStyle.Fill;
        _viewer.Visible = false;

        Controls.Add(_viewer);
        Controls.Add(_emptyLabel);
        _toolbar.Dock = DockStyle.Top;
    }

    private void WireEvents()
    {
        _openButton.Click += (_, _) => ChoosePdf();
        _zoomOutButton.Click += (_, _) => SetZoom(_viewer.ZoomFactor - 0.1);
        _zoomResetButton.Click += (_, _) => SetZoom(1.0);
        _zoomInButton.Click += (_, _) => SetZoom(_viewer.ZoomFactor + 0.1);
        _snapshotButton.Click += async (_, _) => await SaveSnapshotAsync();
        _printButton.Click += async (_, _) =>
        {
            if (_viewer.CoreWebView2 is not null)
            {
                await _viewer.CoreWebView2.ExecuteScriptAsync("window.print()");
            }
        };
        _closeButton.Click += (_, _) => ClosePdf();
        _pageTimer.Interval = 800;
        _pageTimer.Tick += async (_, _) => await UpdatePageIndicatorAsync();
        _viewer.ZoomFactorChanged += (_, _) => UpdateZoomLabel();

        DragEnter += (_, e) =>
        {
            if (GetDroppedPdf(e) is not null)
            {
                e.Effect = DragDropEffects.Copy;
            }
        };

        DragDrop += (_, e) =>
        {
            var path = GetDroppedPdf(e);
            if (path is not null)
            {
                OpenPdf(path);
            }
        };
    }

    private async System.Threading.Tasks.Task EnsureViewerAsync()
    {
        try
        {
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Optimal PDF Reader",
                "WebView2UserData-v101");
            Directory.CreateDirectory(userDataFolder);

            var options = new CoreWebView2EnvironmentOptions
            {
                AdditionalBrowserArguments = "--disable-gpu --disable-gpu-compositing"
            };
            var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);

            await _viewer.EnsureCoreWebView2Async(environment);
            _viewer.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            _viewer.CoreWebView2.Settings.AreDevToolsEnabled = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Optimal PDF Reader needs the Microsoft Edge WebView2 runtime. It is usually installed with Edge.\n\n" + ex.Message,
                "Unable to start PDF viewer",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ChoosePdf()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf|All files (*.*)|*.*",
            Title = "Open PDF",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            OpenPdf(dialog.FileName);
        }
    }

    private void OpenPdf(string path)
    {
        if (!File.Exists(path) || !path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("Please choose a PDF file.", "Optimal PDF Reader", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _currentFile = path;
        var info = new FileInfo(path);
        _pageCount = 1;
        _viewer.Source = new Uri(path);
        _viewer.ZoomFactor = 1.0;
        _viewer.Visible = true;
        _emptyLabel.Visible = false;
        SetDocumentControls(true);
        UpdateZoomLabel();
        _pageLabel.Text = $"Page 1 / {_pageCount}";
        _fileLabel.Text = $"{Path.GetFileName(path)} ({FormatFileSize(info.Length)})";
        Text = Path.GetFileName(path) + " - Optimal PDF Reader";
        _pageTimer.Start();
        _ = UpdatePageCountInBackgroundAsync(path);
    }

    private void ClosePdf()
    {
        _pageTimer.Stop();
        _currentFile = null;
        _pageCount = 1;
        _viewer.ZoomFactor = 1.0;
        _viewer.Source = new Uri("about:blank");
        _viewer.Visible = false;
        _emptyLabel.Visible = true;
        SetDocumentControls(false);
        UpdateZoomLabel();
        _pageLabel.Text = "Page 1 / 1";
        _fileLabel.Text = "No PDF open";
        Text = "Optimal PDF Reader";
    }

    private void SetDocumentControls(bool enabled)
    {
        _zoomOutButton.Enabled = enabled;
        _zoomResetButton.Enabled = enabled;
        _zoomInButton.Enabled = enabled;
        _snapshotButton.Enabled = enabled;
        _printButton.Enabled = enabled;
        _closeButton.Enabled = enabled;
        _pageLabel.Visible = enabled;
    }

    private void SetZoom(double zoom)
    {
        if (_currentFile is null)
        {
            return;
        }

        _viewer.ZoomFactor = Math.Clamp(zoom, 0.25, 4.0);
        UpdateZoomLabel();
    }

    private void UpdateZoomLabel()
    {
        _zoomResetButton.Text = Math.Round(_viewer.ZoomFactor * 100) + "%";
    }

    private async Task SaveSnapshotAsync()
    {
        if (_currentFile is null || _viewer.CoreWebView2 is null)
        {
            return;
        }

        using var previewStream = new MemoryStream();
        await _viewer.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, previewStream);
        previewStream.Position = 0;

        using var preview = new Bitmap(previewStream);
        using var selector = new SnapshotSelectionForm(preview);
        if (selector.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        using var snapshot = selector.CreateSelectedImage();
        var defaultName = Path.GetFileNameWithoutExtension(_currentFile) + "-snapshot.png";
        using var dialog = new SaveFileDialog
        {
            Filter = "PNG image (*.png)|*.png",
            Title = "Save snapshot",
            FileName = defaultName,
            AddExtension = true,
            DefaultExt = "png",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        snapshot.Save(dialog.FileName, ImageFormat.Png);
    }

    private async Task UpdatePageIndicatorAsync()
    {
        if (_currentFile is null || _viewer.CoreWebView2 is null || _pageCheckRunning)
        {
            return;
        }

        _pageCheckRunning = true;
        try
        {
            var result = await _viewer.CoreWebView2.ExecuteScriptAsync("""
                (() => {
                  const seen = new Set();
                  const roots = [document];
                  for (let i = 0; i < roots.length; i++) {
                    const root = roots[i];
                    const nodes = root.querySelectorAll ? root.querySelectorAll('*') : [];
                    for (const node of nodes) {
                      if (node.shadowRoot && !seen.has(node.shadowRoot)) {
                        seen.add(node.shadowRoot);
                        roots.push(node.shadowRoot);
                      }
                      const value = node.value || node.getAttribute?.('value') || '';
                      const label = [
                        node.getAttribute?.('aria-label'),
                        node.getAttribute?.('title'),
                        node.getAttribute?.('placeholder')
                      ].filter(Boolean).join(' ');
                      if (/page/i.test(label) && /^\d+$/.test(String(value).trim())) {
                        return Number(value);
                      }
                    }
                  }
                  return null;
                })()
                """);

            var page = JsonSerializer.Deserialize<int?>(result);
            if (page is > 0)
            {
                _pageLabel.Text = $"Page {Math.Min(page.Value, _pageCount)} / {_pageCount}";
            }
        }
        catch
        {
            _pageLabel.Text = $"Page 1 / {_pageCount}";
        }
        finally
        {
            _pageCheckRunning = false;
        }
    }

    private async Task UpdatePageCountInBackgroundAsync(string path)
    {
        var count = await Task.Run(() => CountPdfPages(path));
        if (!IsHandleCreated || _currentFile != path)
        {
            return;
        }

        BeginInvoke(new Action(() =>
        {
            if (_currentFile != path)
            {
                return;
            }

            _pageCount = count;
            _pageLabel.Text = $"Page 1 / {_pageCount}";
        }));
    }

    private static int CountPdfPages(string path)
    {
        try
        {
            var text = Encoding.Latin1.GetString(File.ReadAllBytes(path));
            var matches = Regex.Matches(text, @"/Type\s*/Page(?!s)\b");
            return Math.Max(1, matches.Count);
        }
        catch
        {
            return 1;
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var size = (double)bytes;
        var unit = 0;

        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : $"{size:0.##} {units[unit]}";
    }

    private static string? GetDroppedPdf(DragEventArgs e)
    {
        if (e.Data is null || !e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return null;
        }

        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (files is null || files.Length == 0)
        {
            return null;
        }

        return files[0].EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? files[0] : null;
    }
}
