using System.Threading.Tasks;

namespace FileConversionApi.Services;

/// <summary>
/// Resolves the path to the Node.js runtime that drives the bundled PDF-&gt;HTML engine.
/// Mirrors the LibreOffice path resolver: the bundled runtime is preferred and validated
/// (name + containment) before use.
/// </summary>
public interface INodeEnginePathResolver
{
    /// <summary>
    /// Resolve the node.exe path used to run the bundled engine script.
    /// </summary>
    Task<string> GetNodePathAsync();
}
