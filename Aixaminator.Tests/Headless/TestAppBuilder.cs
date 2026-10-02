using Avalonia;
using Avalonia.Headless;
using Avalonia.Logging;

[assembly: AvaloniaTestApplication(typeof(Aixaminator.Tests.Headless.TestAppBuilder))]

namespace Aixaminator.Tests.Headless;

/// <summary>
/// Runs the real <see cref="App"/> (styles, templates, resources) on Avalonia's headless platform.
/// Skia is used for rendering so text layout, hit testing and selection behave as on a desktop.
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHarfBuzz()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .AfterSetup(_ => Logger.Sink = BindingErrors.Sink);
}

/// <summary>Records binding warnings and errors reported by Avalonia, so tests can assert there are none.</summary>
public static class BindingErrors
{
    internal static RecordingSink Sink { get; } = new();

    public static IReadOnlyList<string> Snapshot()
    {
        lock (Sink.Messages)
        {
            return [.. Sink.Messages];
        }
    }

    public static void Clear()
    {
        lock (Sink.Messages)
        {
            Sink.Messages.Clear();
        }
    }

    internal sealed class RecordingSink : ILogSink
    {
        public List<string> Messages { get; } = [];

        public bool IsEnabled(LogEventLevel level, string area) => level >= LogEventLevel.Warning && area == LogArea.Binding;

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate) =>
            Log(level, area, source, messageTemplate, []);

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
        {
            if (!IsEnabled(level, area))
            {
                return;
            }

            var message = $"{messageTemplate} [{string.Join(", ", propertyValues)}] (source: {source})";
            lock (Messages)
            {
                Messages.Add(message);
            }
        }
    }
}
