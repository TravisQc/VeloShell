using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using VeloShell.ViewModels;

namespace VeloShell.Views;

public partial class LauncherView : UserControl
{
    // In-process drag payload carrying the dragged connection's id.
    private static readonly DataFormat<string> ConnectionIdFormat =
        DataFormat.CreateInProcessFormat<string>("veloshell/connection-id");

    private PointerPressedEventArgs? _pressedArgs;
    private NodeViewModel? _pressedConnection;
    private Point _pressPosition;

    public LauncherView()
    {
        InitializeComponent();

        ConnectionTree.AddHandler(PointerPressedEvent, OnTreePointerPressed, RoutingStrategies.Tunnel);
        ConnectionTree.AddHandler(PointerMovedEvent, OnTreePointerMoved, RoutingStrategies.Tunnel);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private MainViewModel? Main => (DataContext as LauncherViewModel)?.Main;

    private void OnTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (Main is { SelectedNode: { IsFolder: false } } main)
            _ = main.ConnectSelectedCommand.ExecuteAsync(null);
    }

    private void OnTreePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var node = (e.Source as Control)?.DataContext as NodeViewModel;
        if (node is { IsFolder: false })
        {
            _pressedConnection = node;
            _pressedArgs = e;
            _pressPosition = e.GetPosition(this);
        }
        else
        {
            _pressedConnection = null;
            _pressedArgs = null;
        }
    }

    private async void OnTreePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pressedConnection is null || _pressedArgs is null)
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressedConnection = null;
            _pressedArgs = null;
            return;
        }

        var pos = e.GetPosition(this);
        if (Math.Abs(pos.X - _pressPosition.X) < 6 && Math.Abs(pos.Y - _pressPosition.Y) < 6)
            return;

        var node = _pressedConnection;
        var pressed = _pressedArgs;
        _pressedConnection = null;
        _pressedArgs = null;

        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(ConnectionIdFormat, node.Id));
        await DragDrop.DoDragDropAsync(pressed, data, DragDropEffects.Move);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = TargetFolder(e) is not null && e.DataTransfer.Contains(ConnectionIdFormat)
            ? DragDropEffects.Move
            : DragDropEffects.None;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (Main is not { } main)
            return;

        var folder = TargetFolder(e);
        if (folder is not null && e.DataTransfer.TryGetValue(ConnectionIdFormat) is { } connectionId)
            main.MoveConnection(connectionId, folder.Id);
    }

    private static NodeViewModel? TargetFolder(DragEventArgs e)
    {
        var node = (e.Source as Control)?.DataContext as NodeViewModel;
        return node is { IsFolder: true } ? node : null;
    }
}
