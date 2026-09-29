using System;
using Avalonia.Controls;
using Avalonia.Threading;
using VeloShell.ViewModels;

namespace VeloShell.Views;

public partial class TerminalView : UserControl
{
    private TerminalViewModel? _vm;

    public TerminalView()
    {
        InitializeComponent();

        TerminalCtrl.UserInput += OnCtrlUserInput;
        TerminalCtrl.TerminalResized += OnCtrlTerminalResized;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm != null)
        {
            _vm.OutputReceived -= OnVmOutputReceived;
            _vm.CommandInsertRequested -= OnVmCommandInsertRequested;
        }

        _vm = DataContext as TerminalViewModel;

        if (_vm != null)
        {
            _vm.OutputReceived += OnVmOutputReceived;
            _vm.CommandInsertRequested += OnVmCommandInsertRequested;
        }
    }

    private void OnCtrlUserInput(object? sender, string text)
    {
        _vm?.OnUserInput(text);
    }

    private void OnCtrlTerminalResized(object? sender, Controls.TerminalResizeEventArgs e)
    {
        _vm?.OnTerminalResized(e.Columns, e.Rows, e.Width, e.Height);
    }

    private void OnVmOutputReceived(object? sender, ReadOnlyMemory<byte> data)
    {
        Dispatcher.UIThread.Post(() =>
        {
            TerminalCtrl.Write(data.Span);
        });
    }

    private void OnVmCommandInsertRequested(object? sender, string cmd)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _vm?.OnUserInput(cmd);
            TerminalCtrl.Focus();
        });
    }
}
