using Xunit;

namespace FileConversionApi.Tests.Fixtures;

/// <summary>
/// xUnit collection fixture that captures pre-test subdirectory counts in the three filesystem
/// locations the conversion pipeline writes to, then asserts equality on <see cref="Dispose"/>.
/// A leaked operation directory under <c>App_Data/temp/uploads</c>, <c>App_Data/temp/converted</c>,
/// or <c>App_Data/libreoffice-profiles</c> fails the collection teardown with all three deltas
/// reported in a single message rather than stopping at the first.
/// </summary>
public sealed class AppDataSnapshot : IDisposable
{
    private readonly string _tempUploadsDir;
    private readonly string _tempConvertedDir;
    private readonly string _profilesDir;
    private readonly int _uploadsBaseline;
    private readonly int _convertedBaseline;
    private readonly int _profilesBaseline;

    /// <summary>
    /// Resolves the three pipeline directories under <see cref="AppContext.BaseDirectory"/> and
    /// records the baseline subdirectory count for each one.
    /// </summary>
    public AppDataSnapshot()
    {
        _tempUploadsDir = Path.Combine(AppContext.BaseDirectory, "App_Data", "temp", "uploads");
        _tempConvertedDir = Path.Combine(AppContext.BaseDirectory, "App_Data", "temp", "converted");
        _profilesDir = Path.Combine(AppContext.BaseDirectory, "App_Data", "libreoffice-profiles");
        _uploadsBaseline = CountSubdirectories(_tempUploadsDir);
        _convertedBaseline = CountSubdirectories(_tempConvertedDir);
        _profilesBaseline = CountSubdirectories(_profilesDir);
    }

    /// <summary>
    /// Asserts each tracked directory has the same subdirectory count as it did at construction.
    /// On failure, reports all three deltas in one message so the operator does not have to
    /// re-run after fixing the first one to see whether the others also leaked.
    /// </summary>
    public void Dispose()
    {
        var uploadsNow = CountSubdirectories(_tempUploadsDir);
        var convertedNow = CountSubdirectories(_tempConvertedDir);
        var profilesNow = CountSubdirectories(_profilesDir);

        if (uploadsNow != _uploadsBaseline
            || convertedNow != _convertedBaseline
            || profilesNow != _profilesBaseline)
        {
            Assert.Fail(
                "Operation directory leak: " +
                $"uploads {_uploadsBaseline} -> {uploadsNow}, " +
                $"converted {_convertedBaseline} -> {convertedNow}, " +
                $"profiles {_profilesBaseline} -> {profilesNow}.");
        }
    }

    private static int CountSubdirectories(string path)
        => Directory.Exists(path) ? Directory.GetDirectories(path).Length : 0;
}

/// <summary>
/// xUnit collection definition that binds <see cref="AppDataSnapshot"/> as a shared
/// collection fixture. Test classes that exercise the real engines join this collection via
/// <c>[Collection("AppDataSnapshot")]</c>; they run serialized so the per-class snapshot
/// asserts a stable baseline. Test classes that use only fakes do NOT join the collection
/// and run in parallel.
/// </summary>
[CollectionDefinition("AppDataSnapshot")]
public sealed class AppDataSnapshotCollection : ICollectionFixture<AppDataSnapshot>
{
}
