using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace VeloShell.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        PropertyChanged += OnWindowPropertyChanged;
    }

    // ===== CSD window controls =====

    private void OnMinimize(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void OnMaximizeRestore(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ToggleMaximize();

    private void OnClose(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => Close();

    private void ToggleMaximize()
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    // ===== Title-bar drag / double-tap =====

    private void OnTitleBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        // Double-click the blank chrome toggles maximize.
        if (e.ClickCount == 2)
        {
            if (!IsInteractive(e.Source as Control))
                ToggleMaximize();
            return;
        }

        // Drag only from blank chrome — never from tabs, [+], or window controls.
        if (!IsInteractive(e.Source as Control))
            BeginMoveDrag(e);
    }

    // Walks up from the pressed element to the title bar; interactive controls
    // (tab strip, buttons) must handle their own clicks, not start a window drag.
    private bool IsInteractive(Control? source)
    {
        var current = source;
        while (current is not null && current != TitleBar)
        {
            if (current is Button || current is ListBox || current is ListBoxItem)
                return true;
            current = current.Parent as Control;
        }
        return false;
    }

    // ===== Maximized padding =====
    // With an extended client area, a maximized window overhangs the work area by
    // the resize-border width; pad the content so nothing is clipped off-screen.
    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WindowStateProperty)
            Padding = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
    }
}
