using System.Drawing;
using FlyingAzure.Core;

namespace FlyingAzure;

public sealed class ConfigForm : Form
{
    private readonly SettingsStore _store;
    private readonly AnimatedSurface _preview;

    private readonly TrackBar _count = NewTrack(1, 80);
    private readonly TrackBar _speed = NewTrack(0, 100);
    private readonly TrackBar _size = NewTrack(0, 100);
    private readonly TrackBar _trail = NewTrack(0, 100);
    private readonly Button _colorButton = new() { Text = "Background…", Width = 110, Height = 28 };

    private Color _background;

    public ConfigForm(Settings settings, SettingsStore store, ChevronRenderer renderer)
    {
        _store = store;
        _background = settings.BackgroundColor();

        Text = "Flying Azure — Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(440, 360);

        _count.Value = settings.LogoCount;
        _speed.Value = settings.Speed;
        _size.Value = settings.Size;
        _trail.Value = settings.TrailLength;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
            Padding = new Padding(12),
        };
        AddRow(layout, "Number of logos", _count);
        AddRow(layout, "Speed", _speed);
        AddRow(layout, "Logo size", _size);
        AddRow(layout, "Trail length", _trail);
        AddRow(layout, "Background", _colorButton);

        _preview = new AnimatedSurface(CurrentSettings(), renderer)
        {
            Height = 120,
            Dock = DockStyle.Fill,
            BackColor = _background,
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 44,
            Padding = new Padding(8),
        };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 80 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80 };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;

        Controls.Add(_preview);
        Controls.Add(buttons);
        Controls.Add(layout);

        foreach (var track in new[] { _count, _speed, _size, _trail })
        {
            track.ValueChanged += (_, _) => RefreshPreview();
        }

        _colorButton.Click += (_, _) =>
        {
            using var dialog = new ColorDialog { Color = _background, FullOpen = true };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _background = dialog.Color;
                RefreshPreview();
            }
        };

        ok.Click += (_, _) => _store.Save(CurrentSettings());
    }

    private Settings CurrentSettings() => new()
    {
        LogoCount = _count.Value,
        Speed = _speed.Value,
        Size = _size.Value,
        TrailLength = _trail.Value,
        BackgroundArgb = _background.ToArgb(),
    };

    private void RefreshPreview()
    {
        _preview.BackColor = _background;
        _preview.ApplySettings(CurrentSettings());
    }

    private static TrackBar NewTrack(int min, int max) => new()
    {
        Minimum = min,
        Maximum = max,
        TickStyle = TickStyle.None,
        Width = 240,
    };

    private static void AddRow(TableLayoutPanel layout, string label, Control control)
    {
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 3, 3) });
        layout.Controls.Add(control);
    }
}
