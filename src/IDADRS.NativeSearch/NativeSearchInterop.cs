using System;
using System.Runtime.InteropServices;

namespace IDADRS.NativeSearch;

/// <summary>
/// Raw P/Invoke declarations for the native <c>search_engine</c> shared library.
/// These are internal to the assembly; consumers use <see cref="INativeSearchService"/>.
/// </summary>
internal static class NativeSearchInterop
{
    // The DllImport name must match the compiled output:
    //   Linux   → libsearch_engine.so
    //   macOS   → libsearch_engine.dylib
    //   Windows → search_engine.dll
    // .NET resolves the platform prefix/suffix automatically when NativeLibrary
    // resolution paths are configured (see NativeLibraryLoader.cs).
    private const string LibName = "search_engine";

    /// <summary>
    /// Boyer-Moore-Horspool substring search (case-insensitive).
    /// </summary>
    /// <param name="text">Haystack string (ANSI).</param>
    /// <param name="textLen">Length in bytes, or -1 to use strlen().</param>
    /// <param name="pattern">Needle string (ANSI).</param>
    /// <param name="patternLen">Length in bytes, or -1 to use strlen().</param>
    /// <returns>Zero-based byte offset of first match, or -1 if not found.</returns>
    [DllImport(LibName,
               CallingConvention = CallingConvention.Cdecl,
               CharSet           = CharSet.Ansi,
               EntryPoint        = "bm_search",
               ExactSpelling     = true)]
    internal static extern int BmSearch(
        string text,    int textLen,
        string pattern, int patternLen);

    /// <summary>
    /// Simplified TF-IDF relevance scorer.
    /// </summary>
    /// <param name="query">User query string (ANSI).</param>
    /// <param name="document">Document content (ANSI).</param>
    /// <param name="queryLen">Length in bytes, or -1 to use strlen().</param>
    /// <param name="docLen">Length in bytes, or -1 to use strlen().</param>
    /// <returns>Relevance score in [0.0, 1.0].</returns>
    [DllImport(LibName,
               CallingConvention = CallingConvention.Cdecl,
               CharSet           = CharSet.Ansi,
               EntryPoint        = "score_document",
               ExactSpelling     = true)]
    internal static extern double ScoreDocument(
        string query,    int queryLen,
        string document, int docLen);

    // NOTE: tokenize() / free_tokens() are intentionally not exposed directly
    // through the managed service interface — token management is handled
    // inside score_document() in the native layer.  If direct tokenisation is
    // needed from managed code in the future, add a SafeTokenHandle wrapper
    // using SafeHandle before calling free_tokens().
}
