using System.Net.Http.Headers;

namespace FileConversionApi.Tests.Fixtures;

/// <summary>
/// Builds the multipart/form-data shape POST /api/convert expects (one "file" part and one
/// "targetFormat" string field). The integration tests previously hand-rolled this in four
/// places with two subtly different signatures; this is the single owner.
/// </summary>
internal static class MultipartForms
{
    /// <summary>
    /// Builds a multipart form with a file part named "file" carrying
    /// <paramref name="fileBytes"/> at <paramref name="fileName"/> with
    /// <paramref name="contentType"/>, and a "targetFormat" string field set to
    /// <paramref name="targetFormat"/>.
    /// </summary>
    public static MultipartFormDataContent BuildConvertForm(
        byte[] fileBytes,
        string fileName,
        string contentType,
        string targetFormat)
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
        form.Add(new StringContent(targetFormat), "targetFormat");
        return form;
    }
}
