using System.ComponentModel;
using System.Windows;
using MicBoost.App.ViewModels;

namespace MicBoost.App.Views;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = _viewModel;

        InitializeComponent();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_viewModel.IsExiting && _viewModel.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
        System.Windows.Application.Current.Shutdown();
    }
}
