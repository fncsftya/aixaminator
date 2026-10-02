using Aixaminator.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;

namespace Aixaminator.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    // Clicking outside a dialog dismisses it, like the Escape key.
    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel { Dialogs.ActiveDialog: { } dialog })
        {
            dialog.CancelCommand.Execute(null);
            e.Handled = true;
        }
    }
}
