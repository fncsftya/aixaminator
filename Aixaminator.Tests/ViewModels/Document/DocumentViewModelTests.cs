using Aixaminator.Data;
using Aixaminator.Tests.Infrastructure;
using Aixaminator.ViewModels.Dialogs;
using Aixaminator.ViewModels.Document;

namespace Aixaminator.Tests.ViewModels.Document;

public sealed class DocumentViewModelTests : IAsyncLifetime
{
    private DocumentFixture _f = null!;

    public async ValueTask InitializeAsync() => _f = await DocumentFixture.CreateAsync();

    public ValueTask DisposeAsync() => _f.DisposeAsync();

    private async Task<(Data.Document Document, DocumentViewModel Vm)> OpenThreePartsAsync()
    {
        var document = await _f.AddDocumentAsync("Three parts", "Part zero.", "Part one.", "Part two.");
        return (document, await _f.OpenAsync(document.Id));
    }

    private ReaderViewModel Reader(DocumentViewModel vm) => Assert.IsType<ReaderViewModel>(vm.ModeContent);

    [Fact]
    public async Task Unknown_documents_fail_to_load()
    {
        var vm = await _f.OpenAsync(Guid.NewGuid());

        Assert.True(vm.LoadFailed);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task Loading_shows_the_document_in_read_mode()
    {
        var (_, vm) = await OpenThreePartsAsync();

        Assert.False(vm.IsLoading);
        Assert.False(vm.LoadFailed);
        Assert.Equal("Three parts", vm.Name);
        Assert.Equal(["Part 1", "Part 2", "Part 3"], vm.Parts.Select(p => p.DisplayName));
        Assert.Equal(DocumentMode.Read, vm.Mode);
        Assert.Equal(0, vm.CurrentPartNumber);
        Assert.True(vm.Parts[0].IsCurrent);
        Assert.Equal("Part zero.", Reader(vm).Content.Text);
        Assert.Equal("0%", vm.ProgressText);
    }

    [Fact]
    public async Task Loading_resumes_at_the_first_unread_part()
    {
        var document = await _f.AddDocumentAsync("Doc", "a", "b", "c");
        await _f.MarkReadAsync(document, 0, 2);

        var vm = await _f.OpenAsync(document.Id);

        Assert.Equal(1, vm.CurrentPartNumber);
        Assert.Equal("66%", vm.ProgressText);
    }

    [Fact]
    public async Task Loading_a_finished_document_starts_at_the_beginning()
    {
        var document = await _f.AddDocumentAsync("Doc", "a", "b");
        await _f.MarkReadAsync(document, 0, 1);

        var vm = await _f.OpenAsync(document.Id);

        Assert.Equal(0, vm.CurrentPartNumber);
        Assert.Equal("100%", vm.ProgressText);
    }

    [Fact]
    public async Task Hidden_parts_are_not_listed_or_counted()
    {
        var document = await _f.AddDocumentAsync("Doc", "a", "b");
        document.Parts[0].Hidden = true;
        await _f.Repository.UpdatePartAsync(document.Parts[0]);
        await _f.MarkReadAsync(document, 1);

        var vm = await _f.OpenAsync(document.Id);

        Assert.Equal([1], vm.Parts.Select(p => p.PartNumber));
        Assert.Equal(1, vm.CurrentPartNumber);
        Assert.Equal("100%", vm.ProgressText);
    }

    [Fact]
    public async Task Selecting_a_part_shows_it()
    {
        var (_, vm) = await OpenThreePartsAsync();

        await vm.Parts[2].SelectCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.CurrentPartNumber);
        Assert.Equal([false, false, true], vm.Parts.Select(p => p.IsCurrent));
        Assert.Equal("Part two.", Reader(vm).Content.Text);
        Assert.Empty(_f.Dialogs.Shown);
    }

    [Fact]
    public async Task Next_part_marks_the_current_part_read_when_it_has_notes()
    {
        var (document, vm) = await OpenThreePartsAsync();
        await _f.AddNoteAsync(document.Id, 0);
        vm = await _f.OpenAsync(document.Id);

        await vm.NextPartCommand.ExecuteAsync(null);

        Assert.Empty(_f.Dialogs.Shown);
        Assert.Equal(1, vm.CurrentPartNumber);
        Assert.True(vm.Parts[0].IsRead);
        Assert.Equal("33%", vm.ProgressText);
        Assert.True((await _f.Repository.GetAsync(document.Id))!.Parts[0].IsRead);
    }

    [Fact]
    public async Task Leaving_a_part_without_notes_asks_first()
    {
        var (_, vm) = await OpenThreePartsAsync();
        _f.Dialogs.Respond = _ => false;

        await vm.NextPartCommand.ExecuteAsync(null);

        var dialog = Assert.IsType<ConfirmationDialogViewModel>(Assert.Single(_f.Dialogs.Shown));
        Assert.Equal("No notes taken", dialog.Title);
        Assert.Equal(0, vm.CurrentPartNumber);
        Assert.False(vm.Parts[0].IsRead);
    }

    [Fact]
    public async Task Continuing_without_notes_moves_on()
    {
        var (_, vm) = await OpenThreePartsAsync();

        await vm.NextPartCommand.ExecuteAsync(null);

        Assert.Single(_f.Dialogs.Shown);
        Assert.Equal(1, vm.CurrentPartNumber);
        Assert.True(vm.Parts[0].IsRead);
    }

    [Fact]
    public async Task Previous_part_does_not_mark_anything_read()
    {
        var (_, vm) = await OpenThreePartsAsync();
        await vm.Parts[2].SelectCommand.ExecuteAsync(null);

        await vm.PreviousPartCommand.ExecuteAsync(null);

        Assert.Equal(1, vm.CurrentPartNumber);
        Assert.All(vm.Parts, p => Assert.False(p.IsRead));
    }

    [Fact]
    public async Task Previous_and_next_are_disabled_at_the_ends()
    {
        var (_, vm) = await OpenThreePartsAsync();

        Assert.False(vm.PreviousPartCommand.CanExecute(null));
        Assert.True(vm.NextPartCommand.CanExecute(null));

        await vm.Parts[2].SelectCommand.ExecuteAsync(null);

        Assert.True(vm.PreviousPartCommand.CanExecute(null));
        Assert.False(vm.NextPartCommand.CanExecute(null));
    }

    [Fact]
    public async Task Navigation_skips_hidden_parts()
    {
        var document = await _f.AddDocumentAsync("Doc", "a", "b", "c");
        document.Parts[1].Hidden = true;
        await _f.Repository.UpdatePartAsync(document.Parts[1]);
        var vm = await _f.OpenAsync(document.Id);

        await vm.NextPartCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.CurrentPartNumber);
    }

    [Fact]
    public async Task Marking_a_part_without_notes_as_read_asks_first()
    {
        var (_, vm) = await OpenThreePartsAsync();
        _f.Dialogs.Respond = _ => false;

        await vm.Parts[1].ToggleReadCommand.ExecuteAsync(null);

        Assert.Single(_f.Dialogs.Shown);
        Assert.False(vm.Parts[1].IsRead);
    }

    [Fact]
    public async Task Parts_can_be_marked_read_and_unread()
    {
        var (document, vm) = await OpenThreePartsAsync();
        await _f.AddNoteAsync(document.Id, 1);
        vm = await _f.OpenAsync(document.Id);

        await vm.Parts[1].ToggleReadCommand.ExecuteAsync(null);

        Assert.Empty(_f.Dialogs.Shown);
        Assert.True(vm.Parts[1].IsRead);
        Assert.Equal("Mark as unread", vm.Parts[1].ReadToggleToolTip);
        Assert.Equal("33%", vm.ProgressText);

        await vm.Parts[1].ToggleReadCommand.ExecuteAsync(null);

        Assert.False(vm.Parts[1].IsRead);
        Assert.Equal("Mark as read", vm.Parts[1].ReadToggleToolTip);
        Assert.Equal("0%", vm.ProgressText);
        Assert.Empty(_f.Dialogs.Shown);
    }

    [Fact]
    public async Task Study_mode_shows_notes_and_note_counts()
    {
        var (document, vm) = await OpenThreePartsAsync();
        await _f.AddNoteAsync(document.Id, 0);
        await _f.AddNoteAsync(document.Id, 0);
        vm = await _f.OpenAsync(document.Id);

        vm.StudyCommand.Execute(null);

        Assert.Equal(DocumentMode.Study, vm.Mode);
        Assert.IsType<StudyViewModel>(vm.ModeContent);
        Assert.True(vm.ShowReadButton);
        Assert.False(vm.ShowStudyButton);
        Assert.False(vm.EditCommand.CanExecute(null));
        Assert.All(vm.Parts, p => Assert.True(p.ShowNoteCount));
        Assert.All(vm.Parts, p => Assert.False(p.ShowReadToggle));
        Assert.Equal([2, 0, 0], vm.Parts.Select(p => p.NoteCount));

        vm.ReadCommand.Execute(null);

        Assert.Equal(DocumentMode.Read, vm.Mode);
        Assert.All(vm.Parts, p => Assert.False(p.ShowNoteCount));
        Assert.All(vm.Parts, p => Assert.True(p.ShowReadToggle));
    }

    [Fact]
    public async Task Recap_toggles()
    {
        var (_, vm) = await OpenThreePartsAsync();

        vm.ToggleRecapCommand.Execute(null);

        Assert.Equal(DocumentMode.Recap, vm.Mode);
        Assert.IsType<RecapViewModel>(vm.ModeContent);

        vm.ToggleRecapCommand.Execute(null);

        Assert.Equal(DocumentMode.Read, vm.Mode);
    }

    [Fact]
    public async Task Configure_mode_can_be_closed()
    {
        var (_, vm) = await OpenThreePartsAsync();

        vm.ConfigureCommand.Execute(null);

        Assert.Equal(DocumentMode.Configure, vm.Mode);
        Assert.IsType<ConfigureViewModel>(vm.ModeContent);
        Assert.True(vm.ShowConfigureActions);
        Assert.False(vm.ShowDefaultActions);
        Assert.All(vm.Parts, p => Assert.False(p.ShowReadToggle));

        vm.CloseConfigureCommand.Execute(null);

        Assert.Equal(DocumentMode.Read, vm.Mode);
    }

    [Fact]
    public async Task Editing_locks_the_mode_until_saved_or_cancelled()
    {
        var (_, vm) = await OpenThreePartsAsync();

        vm.EditCommand.Execute(null);

        var editor = Assert.IsType<EditorViewModel>(vm.ModeContent);
        Assert.Equal("Part zero.", editor.Text);
        Assert.True(vm.ShowEditActions);
        Assert.False(vm.ShowDefaultActions);
        Assert.False(vm.StudyCommand.CanExecute(null));
        Assert.False(vm.ToggleRecapCommand.CanExecute(null));

        editor.Text = "Changed";
        vm.CancelEditCommand.Execute(null);

        Assert.Equal(DocumentMode.Read, vm.Mode);
        Assert.Equal("Part zero.", Reader(vm).Content.Text);
        Assert.Equal("Part zero.", await _f.Repository.GetPartTextAsync(vm.DocumentId, 0));
    }

    [Fact]
    public async Task Edits_to_several_parts_are_saved_together()
    {
        var (document, vm) = await OpenThreePartsAsync();
        await _f.AddNoteAsync(document.Id, 0, NoteKind.Highlight, "Part", new HighlightLocation(0, 0, 0, 4), "#f59e0b");
        await _f.AddNoteAsync(document.Id, 2, NoteKind.Highlight, "Part", new HighlightLocation(0, 0, 0, 4), "#f59e0b");
        vm = await _f.OpenAsync(document.Id);

        vm.EditCommand.Execute(null);
        var editor = Assert.IsType<EditorViewModel>(vm.ModeContent);
        editor.Text = "Edited zero.";
        await vm.Parts[1].SelectCommand.ExecuteAsync(null);
        Assert.Equal("Part one.", editor.Text);
        editor.Text = "Edited one.";
        await vm.Parts[0].SelectCommand.ExecuteAsync(null);
        Assert.Equal("Edited zero.", editor.Text);

        await vm.SaveEditsCommand.ExecuteAsync(null);

        Assert.Equal(DocumentMode.Read, vm.Mode);
        Assert.Equal("Edited zero.", Reader(vm).Content.Text);
        Assert.Equal("Edited one.", await _f.Repository.GetPartTextAsync(document.Id, 1));
        Assert.Equal("Part two.", await _f.Repository.GetPartTextAsync(document.Id, 2));
        var notes = (await _f.Repository.GetAsync(document.Id))!.Notes;
        Assert.Null(notes.Single(n => n.DocumentPartNumber == 0).Location);
        Assert.NotNull(notes.Single(n => n.DocumentPartNumber == 2).Location);
    }

    [Fact]
    public async Task Closing_returns_home()
    {
        var (_, vm) = await OpenThreePartsAsync();

        await vm.CloseCommand.ExecuteAsync(null);

        Assert.Equal(1, _f.Navigation.HomeCount);
        Assert.Empty(_f.Dialogs.Shown);
    }

    [Fact]
    public async Task Closing_with_unsaved_edits_asks_first()
    {
        var (_, vm) = await OpenThreePartsAsync();
        vm.EditCommand.Execute(null);
        ((EditorViewModel)vm.ModeContent!).Text = "Unsaved";
        _f.Dialogs.Respond = _ => false;

        await vm.CloseCommand.ExecuteAsync(null);

        Assert.Single(_f.Dialogs.Shown);
        Assert.Equal(0, _f.Navigation.HomeCount);
        Assert.Equal(DocumentMode.Edit, vm.Mode);
    }

    [Fact]
    public async Task Only_one_part_settings_panel_is_open_at_a_time()
    {
        var (_, vm) = await OpenThreePartsAsync();

        vm.Parts[0].ToggleSettingsCommand.Execute(null);
        vm.Parts[1].ToggleSettingsCommand.Execute(null);

        Assert.Equal([false, true, false], vm.Parts.Select(p => p.IsSettingsOpen));
        Assert.NotNull(vm.Parts[1].Settings);
        Assert.Null(vm.Parts[0].Settings);

        vm.Parts[1].ToggleSettingsCommand.Execute(null);

        Assert.All(vm.Parts, p => Assert.False(p.IsSettingsOpen));
    }

    [Fact]
    public async Task Part_settings_rename_and_colour_parts()
    {
        var (document, vm) = await OpenThreePartsAsync();
        var item = vm.Parts[1];
        item.ToggleSettingsCommand.Execute(null);
        var settings = item.Settings!;

        settings.Name = "Introduction";
        settings.UseCustomColour = true;
        settings.PickColourCommand.Execute("#ef4444");
        await vm.Session!.WhenIdleAsync();

        Assert.Equal("Introduction", item.DisplayName);
        Assert.Equal("#ef4444", item.AccentColour);
        var saved = (await _f.Repository.GetAsync(document.Id))!.Parts[1];
        Assert.Equal("Introduction", saved.Name);
        Assert.True(saved.UseColour);
        Assert.Equal("#ef4444", saved.Colour);

        settings.UseCustomColour = false;
        settings.Name = "  ";
        await vm.Session.WhenIdleAsync();

        Assert.Null(item.AccentColour);
        Assert.Equal("Part 2", item.DisplayName);
    }

    [Fact]
    public async Task Invalid_part_colours_are_rejected()
    {
        var (_, vm) = await OpenThreePartsAsync();
        vm.Parts[0].ToggleSettingsCommand.Execute(null);
        var settings = vm.Parts[0].Settings!;
        settings.UseCustomColour = true;

        settings.Colour = "red";

        Assert.True(settings.HasErrors);
        Assert.NotEqual("red", vm.Parts[0].AccentColour);
    }

    [Fact]
    public async Task Hiding_the_current_part_removes_it_and_clears_the_reader()
    {
        var (document, vm) = await OpenThreePartsAsync();
        vm.Parts[0].ToggleSettingsCommand.Execute(null);

        vm.Parts[0].Settings!.HideCommand.Execute(null);
        await vm.Session!.WhenIdleAsync();

        Assert.Equal([1, 2], vm.Parts.Select(p => p.PartNumber));
        Assert.Null(vm.CurrentPartNumber);
        Assert.False(Reader(vm).HasPart);
        Assert.True((await _f.Repository.GetAsync(document.Id))!.Parts[0].Hidden);
    }

    [Fact]
    public async Task Renaming_the_document_updates_the_header()
    {
        var (_, vm) = await OpenThreePartsAsync();
        vm.ConfigureCommand.Execute(null);

        ((ConfigureViewModel)vm.ModeContent!).Name = "New name";

        Assert.Equal("New name", vm.Name);
    }

    [Fact]
    public async Task Reader_uses_the_display_settings()
    {
        _f.Settings.Settings.FontSize = 24;
        _f.Settings.Settings.BackgroundColor = "#101010";
        var (_, vm) = await OpenThreePartsAsync();

        Assert.Equal(24, Reader(vm).Appearance.FontSize);
        Assert.Equal("#101010", vm.Appearance.Background);
    }
}
