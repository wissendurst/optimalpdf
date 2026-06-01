using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace OptimalPdfReaderInstaller;

public sealed class SetupForm : Form
{
    private const string AppName = "Optimal PDF Reader";
    private const string ExeName = "OptimalPdfReader.exe";
    private readonly CheckBox _startMenu = new();
    private readonly Button _installButton = new();
    private readonly Button _cancelButton = new();
    private readonly Label _status = new();
    private readonly ProgressBar _progress = new();
    private readonly Label _version = new();

    public SetupForm()
    {
        Text = "Install Optimal PDF Reader";
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon;
        Width = 520;
        Height = 300;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        var title = new Label
        {
            Text = AppName,
            Left = 18,
            Top = 16,
            Width = 460,
            Height = 32,
            Font = new System.Drawing.Font(Font.FontFamily, 16, System.Drawing.FontStyle.Bold)
        };

        var detail = new Label
        {
            Text = "Install a lightweight local PDF reader with no ads, accounts, or extra panels.",
            Left = 20,
            Top = 54,
            Width = 460,
            Height = 34
        };

        var sourceText = "This code is minimal and open-source: github.com/wissendurst/optimalpdf";
        var source = new LinkLabel
        {
            Text = sourceText,
            Left = 20,
            Top = 88,
            Width = 460,
            Height = 34
        };
        var linkText = "github.com/wissendurst/optimalpdf";
        source.Links.Add(sourceText.IndexOf(linkText, StringComparison.Ordinal), linkText.Length, "https://github.com/wissendurst/optimalpdf");
        source.LinkClicked += (_, e) =>
        {
            if (e.Link?.LinkData is string url)
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
        };

        _startMenu.Text = "Create Start Menu entry";
        _startMenu.Left = 22;
        _startMenu.Top = 136;
        _startMenu.Width = 430;
        _startMenu.Checked = true;

        _status.Left = 22;
        _status.Top = 178;
        _status.Width = 455;
        _status.Height = 24;
        _status.Text = "Ready to install.";

        _progress.Left = 22;
        _progress.Top = 202;
        _progress.Width = 455;
        _progress.Height = 12;
        _progress.Visible = false;

        _installButton.Text = "Install";
        _installButton.Left = 296;
        _installButton.Top = 230;
        _installButton.Width = 90;
        _installButton.Click += async (_, _) => await InstallAsync();

        _cancelButton.Text = "Cancel";
        _cancelButton.Left = 396;
        _cancelButton.Top = 230;
        _cancelButton.Width = 90;
        _cancelButton.Click += (_, _) => Close();

        _version.Text = "V1.0";
        _version.Left = 20;
        _version.Top = 236;
        _version.Width = 80;
        _version.Height = 20;
        _version.ForeColor = System.Drawing.SystemColors.GrayText;

        Controls.AddRange([title, detail, source, _startMenu, _status, _progress, _version, _installButton, _cancelButton]);
    }

    private async System.Threading.Tasks.Task InstallAsync()
    {
        try
        {
            ToggleUi(false);
            _status.Text = "Installing...";
            _progress.Visible = true;
            _progress.Value = 0;

            var installDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "Optimal PDF Reader");

            Directory.CreateDirectory(installDir);
            await System.Threading.Tasks.Task.Run(() => ExtractPayload(installDir, ReportProgress));

            var appPath = Path.Combine(installDir, ExeName);
            if (_startMenu.Checked)
            {
                _status.Text = "Creating Start Menu entry...";
                CreateStartMenuShortcut(appPath);
                SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
            }

            _status.Text = "Registering PDF support...";
            RegisterPdfReader(appPath);

            _progress.Value = 100;
            _status.Text = "Installed.";

            Process.Start(new ProcessStartInfo(appPath) { UseShellExecute = true });

            MessageBox.Show(
                "Optimal PDF Reader has been installed.\n\nWindows may still ask you to confirm the default PDF app in Settings.",
                "Install complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            Close();
        }
        catch (Exception ex)
        {
            ToggleUi(true);
            _progress.Visible = false;
            _status.Text = "Install failed.";
            MessageBox.Show(ex.Message, "Install failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void ExtractPayload(string installDir, Action<int, string> report)
    {
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload.zip")
            ?? throw new InvalidOperationException("Installer payload is missing.");
        using var archive = new ZipArchive(resource, ZipArchiveMode.Read);
        var totalBytes = archive.Entries.Sum(entry => Math.Max(0, entry.Length));
        long copiedBytes = 0;

        foreach (var entry in archive.Entries)
        {
            var destination = Path.GetFullPath(Path.Combine(installDir, entry.FullName));
            if (!destination.StartsWith(Path.GetFullPath(installDir), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destination);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var input = entry.Open();
            using var output = File.Create(destination);
            var buffer = new byte[1024 * 128];
            int read;

            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, read);
                copiedBytes += read;
                if (totalBytes > 0)
                {
                    var percent = (int)Math.Min(95, copiedBytes * 95 / totalBytes);
                    report(percent, "Installing files...");
                }
            }
        }
    }

    private void ReportProgress(int percent, string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => ReportProgress(percent, text)));
            return;
        }

        _progress.Value = Math.Clamp(percent, 0, 100);
        _status.Text = text;
    }

    private static void CreateStartMenuShortcut(string appPath)
    {
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
        var shortcutPath = Path.Combine(programs, "Programs", "Optimal PDF Reader.lnk");

        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("Unable to create Start Menu shortcut.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = appPath;
        shortcut.WorkingDirectory = Path.GetDirectoryName(appPath);
        shortcut.Description = AppName;
        shortcut.IconLocation = Path.Combine(Path.GetDirectoryName(appPath)!, "OptimalPdfReader.ico");
        shortcut.Save();
    }

    private static void RegisterPdfReader(string appPath)
    {
        var command = "\"" + appPath + "\" \"%1\"";

        using (var app = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Applications\" + ExeName))
        {
            app?.SetValue("FriendlyAppName", AppName);
        }

        using (var capabilities = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Applications\" + ExeName + @"\Capabilities"))
        {
            capabilities?.SetValue("ApplicationName", AppName);
            capabilities?.SetValue("ApplicationDescription", "Lightweight local PDF reader");
        }

        using (var fileAssociations = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Applications\" + ExeName + @"\Capabilities\FileAssociations"))
        {
            fileAssociations?.SetValue(".pdf", "OptimalPDFReader.pdf");
        }

        using (var registeredApps = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
        {
            registeredApps?.SetValue(AppName, @"Software\Classes\Applications\" + ExeName + @"\Capabilities");
        }

        using (var commandKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Applications\" + ExeName + @"\shell\open\command"))
        {
            commandKey?.SetValue("", command);
        }

        using (var type = Registry.CurrentUser.CreateSubKey(@"Software\Classes\OptimalPDFReader.pdf"))
        {
            type?.SetValue("", "PDF Document");
        }

        using (var icon = Registry.CurrentUser.CreateSubKey(@"Software\Classes\OptimalPDFReader.pdf\DefaultIcon"))
        {
            icon?.SetValue("", "\"" + appPath + "\",0");
        }

        using (var commandKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\OptimalPDFReader.pdf\shell\open\command"))
        {
            commandKey?.SetValue("", command);
        }

        using (var openWith = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.pdf\OpenWithProgids"))
        {
            openWith?.SetValue("OptimalPDFReader.pdf", Array.Empty<byte>(), RegistryValueKind.None);
        }

        using (var pdfClass = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.pdf"))
        {
            pdfClass?.SetValue("", "OptimalPDFReader.pdf");
        }

        SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    }

    private void ToggleUi(bool enabled)
    {
        _installButton.Enabled = enabled;
        _cancelButton.Enabled = enabled;
        _startMenu.Enabled = enabled;
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
