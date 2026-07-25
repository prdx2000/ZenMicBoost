using System.Windows;
using System.Windows.Forms;
using MicBoost.App.ViewModels;
using Application = System.Windows.Application;

namespace MicBoost.App.Services;

/// <summary>
/// System tray icon with a right-click menu for quick mute/gain access and restoring
/// the main window, backed by <see cref="MainViewModel"/>.
/// </summary>
public sealed class TrayIconService : ITrayIconService
{
    private readonly MainViewModel _viewModel;
    private NotifyIcon? _notifyIcon;
    private ToolStripMenuItem? _muteItem;
    private ToolStripMenuItem? _gainItem;
    private Window? _window;

    public TrayIconService(MainViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public void Initialize()
    {
        _window = Application.Current.MainWindow;

        var menu = new ContextMenuStrip();

        var showItem = new ToolStripMenuItem("Show MicBoost", null, (_, _) => ShowWindow());
        showItem.Font = new System.Drawing.Font(showItem.Font, System.Drawing.FontStyle.Bold);
        menu.Items.Add(showItem);
        menu.Items.Add(new ToolStripSeparator());

        _gainItem = new ToolStripMenuItem(GainText()) { Enabled = false };
        menu.Items.Add(_gainItem);

        _muteItem = new ToolStripMenuItem("Mute", null, (_, _) => _viewModel.ToggleMuteCommand.Execute(null))
        {
            Checked = _viewModel.IsMuted,
        };
        menu.Items.Add(_muteItem);
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(new ToolStripMenuItem("Exit MicBoost", null, (_, _) => _viewModel.ExitCommand.Execute(null)));

        _notifyIcon = new NotifyIcon
        {
            Icon = IconFactory.LoadTrayIcon(),
            Visible = true,
            Text = "MicBoost",
            ContextMenuStrip = menu,
        };
        _notifyIcon.DoubleClick += (_, _) => ShowWindow();

        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.GainDb) or nameof(MainViewModel.SelectedDevice))
            {
                UpdateGainText();
            }
            else if (e.PropertyName == nameof(MainViewModel.IsMuted))
            {
                UpdateMuteState();
            }
        };

        UpdateGainText();
        UpdateMuteState();
    }

    public void SetTooltip(string text)
    {
        if (_notifyIcon is null)
        {
            return;
        }

        // NotifyIcon.Text is limited to 63 characters.
        _notifyIcon.Text = text.Length > 63 ? text[..63] : text;
    }

    private void ShowWindow()
    {
        if (_window is null)
        {
            return;
        }

        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private string GainText() =>
        $"Gain: {(_viewModel.GainDb >= 0 ? "+" : string.Empty)}{_viewModel.GainDb:0.0} dB";

    private void UpdateGainText()
    {
        if (_gainItem is not null)
        {
            _gainItem.Text = GainText();
        }

        SetTooltip($"MicBoost: {GainText()}");
    }

    private void UpdateMuteState()
    {
        if (_muteItem is not null)
        {
            _muteItem.Checked = _viewModel.IsMuted;
        }
    }

    public void Dispose()
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }
}
