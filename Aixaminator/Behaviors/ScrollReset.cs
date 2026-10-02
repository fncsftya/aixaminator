using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Aixaminator.Behaviors;

/// <summary>
/// Scrolls a <see cref="ScrollViewer"/> or <see cref="TextBox"/> back to the top whenever the bound
/// <c>Trigger</c> value changes, e.g. <c>behaviors:ScrollReset.Trigger="{Binding PartNumber}"</c>
/// so each newly shown part starts at its beginning.
/// </summary>
public static class ScrollReset
{
    public static readonly AttachedProperty<object?> TriggerProperty =
        AvaloniaProperty.RegisterAttached<Control, object?>("Trigger", typeof(ScrollReset));

    static ScrollReset()
    {
        TriggerProperty.Changed.AddClassHandler<Control>(OnTriggerChanged);
    }

    public static object? GetTrigger(Control control) => control.GetValue(TriggerProperty);

    public static void SetTrigger(Control control, object? value) => control.SetValue(TriggerProperty, value);

    private static void OnTriggerChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        // Wait for the new content to be laid out before scrolling.
        Dispatcher.UIThread.Post(() => Reset(control), DispatcherPriority.Loaded);
    }

    internal static void Reset(Control control)
    {
        switch (control)
        {
            case ScrollViewer scrollViewer:
                scrollViewer.ScrollToHome();
                break;
            case TextBox textBox:
                textBox.CaretIndex = 0;
                textBox.ClearSelection();
                textBox.ScrollToLine(0);
                break;
        }
    }
}
