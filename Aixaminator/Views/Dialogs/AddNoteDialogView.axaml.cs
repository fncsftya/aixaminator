using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Aixaminator.Views.Dialogs;

public partial class AddNoteDialogView : UserControl
{
    public AddNoteDialogView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Start typing straight away.
        Dispatcher.UIThread.Post(() => NoteText.Focus(), DispatcherPriority.Loaded);
    }
}
