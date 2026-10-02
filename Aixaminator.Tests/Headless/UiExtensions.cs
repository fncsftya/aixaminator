using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Aixaminator.Tests.Headless;

/// <summary>Helpers for driving views on the headless platform like a user would.</summary>
public static class UiExtensions
{
    /// <summary>Shows a control in a window and lays it out.</summary>
    public static Window ShowInWindow(this Control content, double width = 1280, double height = 800)
    {
        var window = new Window { Content = content, Width = width, Height = height };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>Processes queued work and renders, so the UI reflects the latest view model state.</summary>
    public static void Settle(this TopLevel topLevel)
    {
        Dispatcher.UIThread.RunJobs();
        topLevel.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Finds a visible, named descendant (names inside templates included).</summary>
    public static T Find<T>(this Visual root, string name) where T : Control =>
        root.FindAll<T>(name).FirstOrDefault()
        ?? throw new InvalidOperationException($"No visible {typeof(T).Name} named '{name}' was found.");

    public static IEnumerable<T> FindAll<T>(this Visual root, string? name = null) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Where(c => (name is null || c.Name == name) && c.IsEffectivelyVisible);

    /// <summary>Finds a visible button by its text content.</summary>
    public static Button FindButton(this Visual root, string content) =>
        root.FindAll<Button>().FirstOrDefault(b => Equals(b.Content, content))
        ?? throw new InvalidOperationException($"No visible button '{content}' was found.");

    /// <summary>Clicks the centre of a control with the mouse.</summary>
    public static void Click(this TopLevel topLevel, Control control)
    {
        topLevel.Settle();
        var centre = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), topLevel)
                     ?? throw new InvalidOperationException($"{control} is not in the window.");
        topLevel.MouseMove(centre, RawInputModifiers.None);
        topLevel.MouseDown(centre, MouseButton.Left, RawInputModifiers.None);
        topLevel.MouseUp(centre, MouseButton.Left, RawInputModifiers.None);
        topLevel.Settle();
    }

    /// <summary>Drags the mouse between two points relative to a control, e.g. to select text.</summary>
    public static void Drag(this TopLevel topLevel, Control control, Point from, Point to)
    {
        topLevel.Settle();
        var start = control.TranslatePoint(from, topLevel)!.Value;
        var end = control.TranslatePoint(to, topLevel)!.Value;
        topLevel.MouseMove(start, RawInputModifiers.None);
        topLevel.MouseDown(start, MouseButton.Left, RawInputModifiers.None);
        topLevel.MouseMove(end, RawInputModifiers.LeftMouseButton);
        topLevel.MouseUp(end, MouseButton.Left, RawInputModifiers.None);
        topLevel.Settle();
    }

    /// <summary>Focuses a text box, replaces its content and types the given text.</summary>
    public static void TypeInto(this TopLevel topLevel, TextBox textBox, string text)
    {
        topLevel.Click(textBox);
        textBox.SelectAll();
        topLevel.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);
        topLevel.KeyTextInput(text);
        topLevel.Settle();
    }

    public static void PressKey(this TopLevel topLevel, Key key, PhysicalKey physicalKey)
    {
        topLevel.KeyPress(key, RawInputModifiers.None, physicalKey, null);
        topLevel.KeyRelease(key, RawInputModifiers.None, physicalKey, null);
        topLevel.Settle();
    }

    /// <summary>Waits for asynchronous work (database, files) to be reflected, processing UI jobs meanwhile.</summary>
    public static async Task WaitUntilAsync(Func<bool> condition, string description, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Timed out waiting for: {description}");
            }
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }
        Dispatcher.UIThread.RunJobs();
    }
}
