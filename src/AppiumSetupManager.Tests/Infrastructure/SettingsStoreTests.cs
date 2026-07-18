using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Platform;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AppiumSetupManager.Tests.Infrastructure;

public class SettingsStoreTests : IDisposable
{
    private readonly string _tempHome;
    private readonly IPlatformAdapter _platform;

    public SettingsStoreTests()
    {
        _tempHome = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempHome);

        _platform = Substitute.For<IPlatformAdapter>();
        _platform.HomeDirectory.Returns(_tempHome);
    }

    private string SettingsFilePath => Path.Combine(_tempHome, ".appiumsetupmanager", "settings.json");

    [Fact]
    public void Current_MissingFile_ReturnsDefaultsWithoutException()
    {
        File.Exists(SettingsFilePath).Should().BeFalse();

        var sut = new SettingsStore(_platform);

        sut.Current.Should().Be(new AppSettings());
        sut.Current.Theme.Should().Be(AppThemeMode.Dark);
        sut.Current.AutomaticUpdateChecks.Should().BeTrue();
        sut.Current.NotifyOnFailure.Should().BeTrue();
        sut.Current.NotifyOnCompletion.Should().BeTrue();
        sut.Current.AnonymousUsageData.Should().BeFalse();
    }

    [Fact]
    public void Current_CorruptFile_ReturnsDefaultsInsteadOfThrowing()
    {
        WriteSettingsFile("{ this is not valid json !!! ");

        var sut = new SettingsStore(_platform);

        sut.Current.Should().Be(new AppSettings());
    }

    [Fact]
    public void Current_UnknownEnumStringInFile_ReturnsDefaultsInsteadOfThrowing()
    {
        WriteSettingsFile("""{ "Theme": "Solarized", "AutomaticUpdateChecks": false }""");

        var sut = new SettingsStore(_platform);

        sut.Current.Should().Be(new AppSettings());
    }

    [Fact]
    public void Update_ThenReloadInFreshStore_RoundTripsPersistedValues()
    {
        var writer = new SettingsStore(_platform);
        writer.Update(s => s with { Theme = AppThemeMode.Light, AnonymousUsageData = true });

        var reader = new SettingsStore(_platform);

        reader.Current.Theme.Should().Be(AppThemeMode.Light);
        reader.Current.AnonymousUsageData.Should().BeTrue();
    }

    [Fact]
    public void Update_SerializesThemeEnumAsString()
    {
        var sut = new SettingsStore(_platform);

        sut.Update(s => s with { Theme = AppThemeMode.Light });

        File.ReadAllText(SettingsFilePath).Should().Contain("\"Light\"");
    }

    [Fact]
    public void Update_PreservesUntouchedProperties()
    {
        var sut = new SettingsStore(_platform);
        sut.Update(s => s with { NotifyOnCompletion = false });

        sut.Update(s => s with { Theme = AppThemeMode.Light });

        var reloaded = new SettingsStore(_platform);
        reloaded.Current.NotifyOnCompletion.Should().BeFalse("an earlier Update set it");
        reloaded.Current.Theme.Should().Be(AppThemeMode.Light);
        reloaded.Current.AutomaticUpdateChecks.Should().BeTrue("defaults must survive unrelated updates");
        reloaded.Current.NotifyOnFailure.Should().BeTrue();
        reloaded.Current.AnonymousUsageData.Should().BeFalse();
    }

    [Fact]
    public void Update_ReflectsImmediatelyInCurrent()
    {
        var sut = new SettingsStore(_platform);

        sut.Update(s => s with { AutomaticUpdateChecks = false });

        sut.Current.AutomaticUpdateChecks.Should().BeFalse();
    }

    private void WriteSettingsFile(string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
        File.WriteAllText(SettingsFilePath, contents);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempHome))
            Directory.Delete(_tempHome, recursive: true);
    }
}
