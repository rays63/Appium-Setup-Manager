using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Platform;
using AppiumSetupManager.Core.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AppiumSetupManager.Tests.Services;

public class StorageServiceTests : IDisposable
{
    private readonly string _tempHome;
    private readonly string _tempSdk;
    private readonly IPlatformAdapter _platform;
    private readonly StorageService _sut;

    public StorageServiceTests()
    {
        _tempHome = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        _tempSdk = Path.Combine(_tempHome, "android-sdk");
        Directory.CreateDirectory(_tempHome);

        // A non-Windows, non-mac adapter by default so paths are the unix ones and the
        // mac-only categories are gated off; individual tests flip IsMacOs as needed.
        _platform = Substitute.For<IPlatformAdapter>();
        _platform.IsMacOs.Returns(false);
        _platform.IsWindows.Returns(false);
        _platform.IsLinux.Returns(true);
        _platform.HomeDirectory.Returns(_tempHome);
        _platform.DefaultAndroidSdkPath.Returns(_tempSdk);
        _platform.LocateExecutable(Arg.Any<string>()).Returns((string?)null);

        _sut = new StorageService(_platform);
    }

    private string CreateFile(string relativePath, int sizeBytes)
    {
        var fullPath = Path.Combine(_tempHome, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, new byte[sizeBytes]);
        return fullPath;
    }

    [Fact]
    public async Task ScanAsync_FindsCacheCategoriesWithCorrectSizes()
    {
        CreateFile(Path.Combine(".gradle", "caches", "modules.bin"), 4096);
        CreateFile(Path.Combine(".npm", "_cacache", "content.bin"), 2048);
        CreateFile(Path.Combine(".appium", "logs", "appium.log"), 512);

        var items = await _sut.ScanAsync();

        items.Should().HaveCount(3);
        items.Single(i => i.Category == "Gradle caches").SizeBytes.Should().Be(4096);
        items.Single(i => i.Category == "npm cache").SizeBytes.Should().Be(2048);
        items.Single(i => i.Category == "Appium logs").SizeBytes.Should().Be(512);
    }

    [Fact]
    public async Task ScanAsync_AppiumLogsItem_PointsAtLogsSubdirectoryNeverWholeAppiumDir()
    {
        CreateFile(Path.Combine(".appium", "logs", "appium.log"), 100);
        // Installed drivers live next to logs — they must never be offered for deletion.
        CreateFile(Path.Combine(".appium", "node_modules", "driver.js"), 999);

        var items = await _sut.ScanAsync();

        var logs = items.Single(i => i.Category == "Appium logs");
        logs.Path.Should().Be(Path.Combine(_tempHome, ".appium", "logs"));
        logs.SizeBytes.Should().Be(100);
    }

    [Fact]
    public async Task ScanAsync_CreatesOneItemPerAvd_WithReviewOrInUseRisk_NeverLow()
    {
        CreateFile(Path.Combine(".android", "avd", "OldPhone.avd", "userdata.img"), 1000);
        CreateFile(Path.Combine(".android", "avd", "FreshPhone.avd", "userdata.img"), 2000);

        // Set mtimes AFTER file creation (creating files bumps the directory's mtime).
        var oldAvd = Path.Combine(_tempHome, ".android", "avd", "OldPhone.avd");
        var freshAvd = Path.Combine(_tempHome, ".android", "avd", "FreshPhone.avd");
        var staleTimestamp = DateTime.UtcNow.AddDays(-40);
        Directory.SetLastWriteTimeUtc(oldAvd, staleTimestamp);
        Directory.SetLastWriteTimeUtc(freshAvd, DateTime.UtcNow);

        var items = await _sut.ScanAsync();

        var avds = items.Where(i => i.Category == "Android AVDs").ToList();
        avds.Should().HaveCount(2);

        var old = avds.Single(i => i.Name == "OldPhone");
        old.Risk.Should().Be(RiskLevel.Review);
        old.IsSafeToDelete.Should().BeFalse();
        old.LastUsedUtc.Should().BeCloseTo(staleTimestamp, TimeSpan.FromSeconds(2));

        var fresh = avds.Single(i => i.Name == "FreshPhone");
        fresh.Risk.Should().Be(RiskLevel.InUse);
        fresh.IsSafeToDelete.Should().BeFalse();

        avds.Should().NotContain(i => i.Risk == RiskLevel.Low);
    }

    [Fact]
    public async Task ScanAsync_AvdWithResolvableAvdManager_GetsCliDeleteTool()
    {
        CreateFile(Path.Combine(".android", "avd", "Pixel.avd", "userdata.img"), 100);
        var avdManagerPath = CreateFile(
            Path.Combine("android-sdk", "cmdline-tools", "latest", "bin", "avdmanager"), 10);

        var items = await _sut.ScanAsync();

        var avd = items.Single(i => i.Category == "Android AVDs");
        avd.DeleteToolExecutable.Should().Be(avdManagerPath);
        avd.DeleteToolArguments.Should().Be("delete avd -n Pixel");
    }

    [Fact]
    public async Task ScanAsync_FindsSystemImagesUnderSubstitutedSdkPath()
    {
        CreateFile(Path.Combine("android-sdk", "system-images", "android-34", "google_apis", "image.img"), 5000);

        var items = await _sut.ScanAsync();

        var image = items.Single(i => i.Category == "Android system images");
        image.Name.Should().Be("android-34");
        image.Path.Should().Be(Path.Combine(_tempSdk, "system-images", "android-34"));
        image.SizeBytes.Should().Be(5000);
        image.Risk.Should().Be(RiskLevel.Review);
    }

    [Fact]
    public async Task ScanAsync_MissingDirectories_AreOmittedEntirely()
    {
        // Fresh home with nothing in it: no categories at all, not zero-sized placeholders.
        var items = await _sut.ScanAsync();

        items.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanAsync_MacOnlyCategories_AbsentWhenNotMacOs()
    {
        CreateFile(Path.Combine("Library", "Caches", "Homebrew", "bottle.tar.gz"), 100);
        CreateFile(Path.Combine("Library", "Developer", "Xcode", "DerivedData", "build.o"), 100);
        CreateFile(Path.Combine("Library", "Developer", "CoreSimulator", "Caches", "dyld.bin"), 100);

        var items = await _sut.ScanAsync();

        items.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanAsync_MacOnlyCategories_PresentWhenMacOs()
    {
        _platform.IsMacOs.Returns(true);
        _platform.IsLinux.Returns(false);
        CreateFile(Path.Combine("Library", "Caches", "Homebrew", "bottle.tar.gz"), 300);
        CreateFile(Path.Combine("Library", "Developer", "Xcode", "DerivedData", "build.o"), 200);
        CreateFile(Path.Combine("Library", "Developer", "CoreSimulator", "Caches", "dyld.bin"), 100);

        var items = await _sut.ScanAsync();

        items.Select(i => i.Category).Should().BeEquivalentTo(
            "Homebrew cache", "Xcode DerivedData", "iOS Simulator caches");

        // Homebrew cache is a manual walk on purpose (no `brew cleanup`), so no CLI tool.
        items.Single(i => i.Category == "Homebrew cache").DeleteToolExecutable.Should().BeNull();
    }

    [Fact]
    public async Task ScanAsync_UnreadableCategory_DoesNotSinkTheRestOfTheScan()
    {
        if (OperatingSystem.IsWindows())
            return; // Relies on unix permissions to make a directory unreadable.

        CreateFile(Path.Combine(".npm", "_cacache", "content.bin"), 2048);
        CreateFile(Path.Combine(".gradle", "caches", "modules.bin"), 100);
        var gradleCaches = Path.Combine(_tempHome, ".gradle", "caches");
        File.SetUnixFileMode(gradleCaches, UnixFileMode.None);

        try
        {
            var items = await _sut.ScanAsync();

            items.Should().ContainSingle(i => i.Category == "npm cache");
        }
        finally
        {
            File.SetUnixFileMode(gradleCaches,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public async Task ScanAsync_ReturnsItemsOrderedBySizeDescending()
    {
        CreateFile(Path.Combine(".npm", "_cacache", "small.bin"), 100);
        CreateFile(Path.Combine(".gradle", "caches", "big.bin"), 9000);
        CreateFile(Path.Combine(".appium", "logs", "medium.log"), 500);

        var items = await _sut.ScanAsync();

        items.Should().BeInDescendingOrder(i => i.SizeBytes);
        items[0].Category.Should().Be("Gradle caches");
    }

    [Fact]
    public async Task ScanAsync_NpmCacheItem_CarriesWhitelistableCliDeleteTool()
    {
        CreateFile(Path.Combine(".npm", "_cacache", "content.bin"), 2048);

        var items = await _sut.ScanAsync();

        var npm = items.Single(i => i.Category == "npm cache");
        npm.DeleteToolExecutable.Should().Be("npm"); // LocateExecutable returns null in this fixture.
        npm.DeleteToolArguments.Should().Be("cache clean --force");
        npm.Risk.Should().Be(RiskLevel.Low);
        npm.LastUsedUtc.Should().NotBeNull();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempHome))
            Directory.Delete(_tempHome, recursive: true);
    }
}
