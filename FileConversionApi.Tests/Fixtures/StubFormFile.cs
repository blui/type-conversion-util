using Microsoft.AspNetCore.Http;

namespace FileConversionApi.Tests.Fixtures;

/// <summary>
/// Minimal <see cref="IFormFile"/> stub backed by an in-memory byte array. Used by
/// direct unit tests of <c>InputValidator</c> so the validator's contract can be exercised
/// without standing up the ASP.NET Core model binder or the multipart parser.
/// </summary>
internal sealed class StubFormFile : IFormFile
{
    private readonly byte[] _bytes;

    /// <summary>
    /// Captures the file's bytes, filename, and optional content type.
    /// </summary>
    /// <param name="bytes">The file payload. Must not be null. Empty arrays are accepted.</param>
    /// <param name="fileName">The client-supplied filename. Must not be null.</param>
    /// <param name="contentType">The MIME type. Defaults to <c>application/octet-stream</c>.</param>
    public StubFormFile(byte[] bytes, string fileName, string contentType = "application/octet-stream")
    {
        _bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
        FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
        ContentType = contentType;
    }

    /// <inheritdoc/>
    public string ContentType { get; }

    /// <inheritdoc/>
    public string ContentDisposition => string.Empty;

    /// <inheritdoc/>
    public IHeaderDictionary Headers => new HeaderDictionary();

    /// <inheritdoc/>
    public long Length => _bytes.Length;

    /// <inheritdoc/>
    public string Name => "file";

    /// <inheritdoc/>
    public string FileName { get; }

    /// <inheritdoc/>
    public Stream OpenReadStream() => new MemoryStream(_bytes);

    /// <inheritdoc/>
    public void CopyTo(Stream target) => target.Write(_bytes, 0, _bytes.Length);

    /// <inheritdoc/>
    public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
        => target.WriteAsync(_bytes, 0, _bytes.Length, cancellationToken);
}
