namespace IDADRS.NativeSearch;

/// <summary>
/// Defines the managed contract for native-accelerated document search operations.
/// Implementations may delegate to the native C library or fall back to managed
/// equivalents when the library is unavailable.
/// </summary>
public interface INativeSearchService
{
    /// <summary>
    /// Searches for the first occurrence of <paramref name="pattern"/> in
    /// <paramref name="text"/> using a case-insensitive Boyer-Moore-Horspool algorithm.
    /// </summary>
    /// <param name="text">The haystack string to search within.</param>
    /// <param name="pattern">The needle string to find.</param>
    /// <returns>
    /// Zero-based character index of the first match, or -1 if not found.
    /// </returns>
    int BmSearch(string text, string pattern);

    /// <summary>
    /// Scores <paramref name="document"/> against <paramref name="query"/> using
    /// a simplified TF-IDF algorithm.
    /// </summary>
    /// <param name="query">User search query.</param>
    /// <param name="document">Full document text content to score.</param>
    /// <returns>Relevance score normalised to [0.0, 1.0].</returns>
    double ScoreDocument(string query, string document);

    /// <summary>
    /// Gets a value indicating whether the native library was loaded successfully.
    /// When <see langword="false"/>, all methods use managed fallback implementations.
    /// </summary>
    bool IsNativeAvailable { get; }
}
