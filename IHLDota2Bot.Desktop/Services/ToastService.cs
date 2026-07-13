using System;
using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Threading;

namespace IHLDota2Bot.Desktop.Services;

public enum ToastKind
{
    Info,
    Success,
    Error,
}

/// <summary>A transient notification shown top-right and auto-dismissed.</summary>
public sealed class ToastItem(string message, ToastKind kind)
{
    public string Message { get; } = message;
    public ToastKind Kind { get; } = kind;

    public IBrush Accent =>
        Kind switch
        {
            ToastKind.Success => SuccessBrush,
            ToastKind.Error => ErrorBrush,
            _ => InfoBrush,
        };

    private static readonly IBrush SuccessBrush = new SolidColorBrush(Color.Parse("#3FD6A0"));
    private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.Parse("#FF5C5C"));
    private static readonly IBrush InfoBrush = new SolidColorBrush(Color.Parse("#FF8A3D"));
}

/// <summary>
/// App-wide toast notifications. View-models call <see cref="Show"/>/<see cref="Success"/>/
/// <see cref="Error"/>; the toast host in the main window renders <see cref="Items"/> and
/// each entry self-removes after a few seconds. Call on the UI thread.
/// </summary>
public sealed class ToastService
{
    public ObservableCollection<ToastItem> Items { get; } = new();

    public void Show(string message, ToastKind kind = ToastKind.Info)
    {
        var item = new ToastItem(message, kind);
        Items.Add(item);
        DispatcherTimer.RunOnce(() => Items.Remove(item), TimeSpan.FromSeconds(3.5));
    }

    public void Success(string message) => Show(message, ToastKind.Success);

    public void Error(string message) => Show(message, ToastKind.Error);
}
