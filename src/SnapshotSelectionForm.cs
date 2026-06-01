using System;
using System.Drawing;
using System.Windows.Forms;

namespace SimplePdfReaderWin;

public sealed class SnapshotSelectionForm : Form
{
    private readonly Bitmap _source;
    private readonly PictureBox _picture = new();
    private readonly Panel _buttonPanel = new();
    private readonly Button _saveButton = new();
    private readonly Button _cancelButton = new();
    private Point? _dragStart;
    private Rectangle _selection;

    public SnapshotSelectionForm(Bitmap source)
    {
        _source = new Bitmap(source);
        Text = "Select snapshot area";
        Width = Math.Min(1100, Math.Max(720, _source.Width + 40));
        Height = Math.Min(820, Math.Max(520, _source.Height + 100));
        MinimumSize = new Size(640, 420);
        StartPosition = FormStartPosition.CenterParent;

        _picture.Dock = DockStyle.Fill;
        _picture.BackColor = Color.FromArgb(38, 38, 38);
        _picture.Image = _source;
        _picture.SizeMode = PictureBoxSizeMode.Zoom;
        _picture.Paint += PaintSelection;
        _picture.MouseDown += StartSelection;
        _picture.MouseMove += UpdateSelection;
        _picture.MouseUp += FinishSelection;

        _saveButton.Text = "Save selected area";
        _saveButton.Enabled = false;
        _saveButton.AutoSize = true;
        _saveButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };

        _cancelButton.Text = "Cancel";
        _cancelButton.AutoSize = true;
        _cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        _buttonPanel.Dock = DockStyle.Bottom;
        _buttonPanel.Height = 48;
        _buttonPanel.Padding = new Padding(8);
        _buttonPanel.Controls.Add(_saveButton);
        _buttonPanel.Controls.Add(_cancelButton);

        _cancelButton.Left = 8;
        _cancelButton.Top = 10;
        _saveButton.Left = _cancelButton.Right + 8;
        _saveButton.Top = 10;

        Controls.Add(_picture);
        Controls.Add(_buttonPanel);
    }

    public Bitmap CreateSelectedImage()
    {
        var imageRect = GetImageRectangle();
        var clipped = Rectangle.Intersect(_selection, imageRect);
        if (clipped.Width <= 0 || clipped.Height <= 0)
        {
            return new Bitmap(_source);
        }

        var scaleX = (double)_source.Width / imageRect.Width;
        var scaleY = (double)_source.Height / imageRect.Height;
        var sourceRect = new Rectangle(
            (int)Math.Round((clipped.Left - imageRect.Left) * scaleX),
            (int)Math.Round((clipped.Top - imageRect.Top) * scaleY),
            (int)Math.Round(clipped.Width * scaleX),
            (int)Math.Round(clipped.Height * scaleY));

        sourceRect.Intersect(new Rectangle(Point.Empty, _source.Size));
        return _source.Clone(sourceRect, _source.PixelFormat);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _source.Dispose();
            _picture.Dispose();
            _buttonPanel.Dispose();
            _saveButton.Dispose();
            _cancelButton.Dispose();
        }

        base.Dispose(disposing);
    }

    private void StartSelection(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !GetImageRectangle().Contains(e.Location))
        {
            return;
        }

        _dragStart = e.Location;
        _selection = Rectangle.Empty;
        _saveButton.Enabled = false;
        _picture.Invalidate();
    }

    private void UpdateSelection(object? sender, MouseEventArgs e)
    {
        if (_dragStart is null)
        {
            return;
        }

        var imageRect = GetImageRectangle();
        var current = ClampToRectangle(e.Location, imageRect);
        _selection = MakeRectangle(_dragStart.Value, current);
        _saveButton.Enabled = _selection.Width > 4 && _selection.Height > 4;
        _picture.Invalidate();
    }

    private void FinishSelection(object? sender, MouseEventArgs e)
    {
        if (_dragStart is null)
        {
            return;
        }

        UpdateSelection(sender, e);
        _dragStart = null;
    }

    private void PaintSelection(object? sender, PaintEventArgs e)
    {
        if (_selection.Width <= 0 || _selection.Height <= 0)
        {
            return;
        }

        using var shade = new SolidBrush(Color.FromArgb(80, Color.Black));
        using var border = new Pen(Color.White, 2);
        var imageRect = GetImageRectangle();

        e.Graphics.FillRectangle(shade, imageRect);
        e.Graphics.SetClip(_selection);
        e.Graphics.DrawImage(_source, imageRect);
        e.Graphics.ResetClip();
        e.Graphics.DrawRectangle(border, _selection);
    }

    private Rectangle GetImageRectangle()
    {
        var box = _picture.ClientRectangle;
        var imageRatio = (double)_source.Width / _source.Height;
        var boxRatio = (double)box.Width / Math.Max(1, box.Height);

        if (boxRatio > imageRatio)
        {
            var height = box.Height;
            var width = (int)Math.Round(height * imageRatio);
            return new Rectangle((box.Width - width) / 2, 0, width, height);
        }

        var fittedHeight = (int)Math.Round(box.Width / imageRatio);
        return new Rectangle(0, (box.Height - fittedHeight) / 2, box.Width, fittedHeight);
    }

    private static Point ClampToRectangle(Point point, Rectangle rect)
    {
        return new Point(
            Math.Clamp(point.X, rect.Left, rect.Right),
            Math.Clamp(point.Y, rect.Top, rect.Bottom));
    }

    private static Rectangle MakeRectangle(Point a, Point b)
    {
        return Rectangle.FromLTRB(
            Math.Min(a.X, b.X),
            Math.Min(a.Y, b.Y),
            Math.Max(a.X, b.X),
            Math.Max(a.Y, b.Y));
    }
}
