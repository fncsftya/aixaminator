using Aixaminator.Models.Ai;

namespace Aixaminator.Tests.Models;

public class AiSettingsTests
{
    [Fact]
    public void New_settings_have_an_empty_mapping_for_every_action()
    {
        var settings = new AiSettings();
        settings.Normalise();

        Assert.All(AiAction.All, action =>
        {
            Assert.Equal("", settings.ActionProviderMap[action]);
            Assert.Equal("", settings.ActionModelMap[action]);
        });
    }

    [Fact]
    public void Adding_the_first_provider_maps_every_action_to_its_default_model()
    {
        var settings = new AiSettings();

        settings.AddProvider(new AiProvider { Name = "google", ApiKey = "key" });

        Assert.All(AiAction.All, action =>
        {
            Assert.Equal("google", settings.ActionProviderMap[action]);
            Assert.Equal("gemini-2.0-flash", settings.ActionModelMap[action]);
        });
    }

    [Fact]
    public void Adding_another_provider_keeps_existing_mappings()
    {
        var settings = new AiSettings();
        settings.AddProvider(new AiProvider { Name = "google", ApiKey = "key" });

        settings.AddProvider(new AiProvider { Name = "openai", ApiKey = "key2" });

        Assert.Equal(["google", "openai"], settings.AiProviders.Select(p => p.Name));
        Assert.All(AiAction.All, action => Assert.Equal("google", settings.ActionProviderMap[action]));
    }

    [Fact]
    public void Adding_an_existing_provider_replaces_its_api_key()
    {
        var settings = new AiSettings();
        settings.AddProvider(new AiProvider { Name = "openai", ApiKey = "old" });

        settings.AddProvider(new AiProvider { Name = "openai", ApiKey = "new" });

        var provider = Assert.Single(settings.AiProviders);
        Assert.Equal("new", provider.ApiKey);
    }

    [Fact]
    public void Adding_a_provider_maps_actions_that_have_no_provider()
    {
        var settings = new AiSettings();
        settings.AddProvider(new AiProvider { Name = "google", ApiKey = "key" });
        settings.SetActionProvider(AiAction.QuizGeneration, "");

        settings.AddProvider(new AiProvider { Name = "openai", ApiKey = "key2" });

        Assert.Equal("google", settings.ActionProviderMap[AiAction.DocumentCleaning]);
        Assert.Equal("openai", settings.ActionProviderMap[AiAction.QuizGeneration]);
        Assert.Equal("gpt-4o-mini", settings.ActionModelMap[AiAction.QuizGeneration]);
    }

    [Fact]
    public void Unknown_providers_cannot_be_added()
    {
        var settings = new AiSettings();

        Assert.Throws<ArgumentException>(() => settings.AddProvider(new AiProvider { Name = "acme", ApiKey = "key" }));
    }

    [Fact]
    public void Removing_a_provider_clears_the_actions_that_used_it()
    {
        var settings = new AiSettings();
        settings.AddProvider(new AiProvider { Name = "google", ApiKey = "key" });
        settings.AddProvider(new AiProvider { Name = "openai", ApiKey = "key2" });
        settings.SetActionProvider(AiAction.QuizGeneration, "openai");

        settings.RemoveProvider("google");

        Assert.Equal("", settings.ActionProviderMap[AiAction.DocumentCleaning]);
        Assert.Equal("", settings.ActionModelMap[AiAction.DocumentCleaning]);
        Assert.Equal("openai", settings.ActionProviderMap[AiAction.QuizGeneration]);
    }

    [Fact]
    public void Changing_the_provider_of_an_action_selects_its_default_model()
    {
        var settings = new AiSettings();
        settings.AddProvider(new AiProvider { Name = "google", ApiKey = "key" });
        settings.AddProvider(new AiProvider { Name = "openrouter", ApiKey = "key2" });

        settings.SetActionProvider(AiAction.DocumentCleaning, "openrouter");

        Assert.Equal("deepseek/deepseek-v4.1-flash", settings.ActionModelMap[AiAction.DocumentCleaning]);
    }

    [Fact]
    public void Clearing_the_provider_of_an_action_clears_its_model()
    {
        var settings = new AiSettings();
        settings.AddProvider(new AiProvider { Name = "google", ApiKey = "key" });

        settings.SetActionProvider(AiAction.DocumentCleaning, "");

        Assert.Equal("", settings.ActionModelMap[AiAction.DocumentCleaning]);
    }

    [Fact]
    public void Models_are_listed_for_a_provider_and_action()
    {
        Assert.Equal(["gpt-4o-mini", "gpt-4o"], AiSettings.ModelsFor("openai", AiAction.QuizGeneration).Select(m => m.Id));
        Assert.Empty(AiSettings.ModelsFor("", AiAction.QuizGeneration));
    }

    [Fact]
    public void Resolving_an_action_uses_the_mapped_provider_and_model()
    {
        var settings = new AiSettings();
        settings.AddProvider(new AiProvider { Name = "google", ApiKey = "g" });
        settings.AddProvider(new AiProvider { Name = "openai", ApiKey = "o" });
        settings.SetActionProvider(AiAction.QuizGeneration, "openai");
        settings.SetActionModel(AiAction.QuizGeneration, "gpt-4o");

        var resolved = settings.ResolveAction(AiAction.QuizGeneration);

        Assert.NotNull(resolved);
        Assert.Equal("openai", resolved.Value.Provider.Name);
        Assert.Equal("gpt-4o", resolved.Value.Model);
    }

    [Fact]
    public void Resolving_an_unmapped_action_falls_back_to_the_first_provider()
    {
        var settings = new AiSettings();
        settings.AddProvider(new AiProvider { Name = "openai", ApiKey = "o" });
        settings.SetActionProvider(AiAction.QuizGeneration, "");

        var resolved = settings.ResolveAction(AiAction.QuizGeneration);

        Assert.NotNull(resolved);
        Assert.Equal("openai", resolved.Value.Provider.Name);
        Assert.Equal("gpt-4o-mini", resolved.Value.Model);
    }

    [Fact]
    public void Resolving_without_any_provider_returns_null()
    {
        Assert.Null(new AiSettings().ResolveAction(AiAction.DocumentCleaning));
    }

    [Fact]
    public void Normalise_drops_mappings_to_providers_that_are_not_configured()
    {
        var settings = new AiSettings
        {
            ActionProviderMap = { [AiAction.DocumentCleaning] = "openai" },
            ActionModelMap = { [AiAction.DocumentCleaning] = "gpt-4o" },
        };

        settings.Normalise();

        Assert.Equal("", settings.ActionProviderMap[AiAction.DocumentCleaning]);
        Assert.Equal("", settings.ActionModelMap[AiAction.DocumentCleaning]);
    }
}
