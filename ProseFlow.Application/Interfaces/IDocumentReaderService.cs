namespace ProseFlow.Application.Interfaces;

/// <summary>
/// Defines a contract for a service that can extract text content from various document types.
/// </summary>
public interface IDocumentReaderService
{
    /// <summary>
    /// Asynchronously reads the text content from a file at the specified path.
    /// </summary>
    /// <param name="filePath">The absolute path to the file.</param>
    /// <returns>A string containing the extracted text content of the file.</returns>
    /// <exception cref="System.IO.FileNotFoundException">Thrown if the file does not exist.</exception>
    /// <exception cref="System.NotSupportedException">Thrown if the file type is not supported.</exception>
    Task<string> ReadTextAsync(string filePath);
}