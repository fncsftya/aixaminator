using Aixaminator.Tests.Infrastructure;
using Aixaminator.ViewModels.Document;

namespace Aixaminator.Tests.ViewModels.Document;

public sealed class ConfigureViewModelTests : IAsyncLifetime
{
    private DocumentFixture _f = null!;

    public async ValueTask InitializeAsync() => _f = await DocumentFixture.CreateAsync();

    public ValueTask DisposeAsync() => _f.DisposeAsync();

    private async Task<(DocumentViewModel Vm, ConfigureViewModel Configure)> ConfigureAsync(Action<Data.Document>? arrange = null)
    {
        var document = await _f.Database.AddDocumentAsync("Original", "Description", "a", "b", "c");
        if (arrange is not null)
        {
            arrange(document);
            foreach (var part in document.Parts)
            {
                await _f.Repository.UpdatePartAsync(part);
            }
        }
        var vm = await _f.OpenAsync(document.Id);
        vm.ConfigureCommand.Execute(null);
        return (vm, Assert.IsType<ConfigureViewModel>(vm.ModeContent));
    }

    [Fact]
    public async Task Title_and_description_are_saved()
    {
        var (vm, configure) = await ConfigureAsync();
        Assert.Equal("Original", configure.Name);
        Assert.Equal("Description", configure.Description);

        configure.Name = "Renamed";
        configure.Description = "Updated";
        await vm.Session!.WhenIdleAsync();

        var saved = await _f.Repository.GetAsync(vm.DocumentId);
        Assert.Equal("Renamed", saved!.Name);
        Assert.Equal("Updated", saved.Description);
    }

    [Fact]
    public async Task A_blank_title_is_not_saved()
    {
        var (vm, configure) = await ConfigureAsync();

        configure.Name = "   ";
        await vm.Session!.WhenIdleAsync();

        Assert.Equal("A document needs a title.", configure.GetErrors(nameof(configure.Name)).Single().ErrorMessage);
        Assert.Equal("Original", (await _f.Repository.GetAsync(vm.DocumentId))!.Name);
        Assert.Equal("Original", vm.Name);
    }

    [Fact]
    public async Task Hidden_parts_are_listed_and_can_be_unhidden()
    {
        var (vm, configure) = await ConfigureAsync(document =>
        {
            document.Parts[0].Hidden = true;
            document.Parts[2].Hidden = true;
            document.Parts[2].Name = "Appendix";
        });

        Assert.True(configure.HasHiddenParts);
        Assert.Equal(["Part 1", "Appendix"], configure.HiddenParts.Select(p => p.DisplayName));

        await configure.HiddenParts[1].UnhideCommand.ExecuteAsync(null);

        Assert.Equal(["Part 1"], configure.HiddenParts.Select(p => p.DisplayName));
        Assert.Equal([1, 2], vm.Parts.Select(p => p.PartNumber));
        Assert.False((await _f.Repository.GetAsync(vm.DocumentId))!.Parts[2].Hidden);
    }

    [Fact]
    public async Task Documents_without_hidden_parts_say_so()
    {
        var (_, configure) = await ConfigureAsync();

        Assert.False(configure.HasHiddenParts);
    }
}
