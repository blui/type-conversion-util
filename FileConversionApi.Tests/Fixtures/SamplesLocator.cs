namespace FileConversionApi.Tests.Fixtures;

/// <summary>
/// Locates repository-level test inputs (the <c>samples/</c> directory committed at the
/// solution root) and reports whether the bundled conversion-engine binaries are staged
/// alongside the test assembly. Real-engine tests gate their assertions on
/// <see cref="BundledEnginesPresent"/> and early-return when the gate is false, keeping
/// clean CI runners green without skip noise.
/// </summary>
internal static class SamplesLocator
{
    private const int MaxAscents = 8;
    private const string SamplesDirectoryName = "samples";
    private const string TemplateDocxFileName = "template.docx";

    /// <summary>
    /// True when all three bundled-engine artifacts resolve against <see cref="AppContext.BaseDirectory"/>:
    /// LibreOffice <c>soffice.exe</c>, the bundled <c>node.exe</c>, and the <c>pdf-to-html.mjs</c> payload.
    /// Evaluated once at type initialization; tests read it as a hot constant.
    /// </summary>
    public static bool BundledEnginesPresent { get; } =
        File.Exists(Path.Combine(AppContext.BaseDirectory, "LibreOffice", "program", "soffice.exe"))
        && File.Exists(Path.Combine(AppContext.BaseDirectory, "engine", "node", "node.exe"))
        && File.Exists(Path.Combine(AppContext.BaseDirectory, "engine", "pdf-to-html.mjs"));

    /// <summary>
    /// Walks parent directories upward from <see cref="AppContext.BaseDirectory"/> until a
    /// solution file is found, then returns that directory's path. The ascent is bounded by
    /// <see cref="MaxAscents"/> so a misconfigured layout fails fast rather than spinning.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no <c>*.sln</c> file is found within <see cref="MaxAscents"/> parent directories.
    /// </exception>
    public static string SolutionRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        for (int ascents = 0; ascents < MaxAscents && current is not null; ascents++)
        {
            if (Directory.EnumerateFiles(current.FullName, "*.sln").Any())
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new InvalidOperationException(
            "Solution root not found from " + AppContext.BaseDirectory);
    }

    /// <summary>
    /// Returns the absolute path to the tracked <c>samples/template.docx</c> fixture.
    /// </summary>
    public static string TemplateDocxPath()
        => Path.Combine(SolutionRoot(), SamplesDirectoryName, TemplateDocxFileName);
}
