using System;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace IDADRS.NativeSearch;

/// <summary>
/// Production implementation of <see cref="INativeSearchService"/>.
/// Delegates to the native <c>search_engine</c> C shared library via P/Invoke.
///
/// Graceful degradation strategy:
///   • On first call, a probe P/Invoke is attempted.
///   • If <see cref="DllNotFoundException"/>, <see cref="EntryPointNotFoundException"/>,
///     or <see cref="SEHException"/> is caught, <see cref="_nativeAvailable"/> is set
///     to <see langword="false"/> and every subsequent call uses the managed fallback.
///   • The fallback is correct but slower:
///       BmSearch     → <see cref="string.IndexOf"/> with OrdinalIgnoreCase
///       ScoreDocument → simple term-frequency counter (no IDF weighting)
/// </summary>
public sealed class NativeSearchService : INativeSearchService
{
    private readonly ILogger<NativeSearchService>? _logger;

    // Set once during construction; never flipped back to true.
    private bool _nativeAvailable;

    /// <inheritdoc />
    public bool IsNativeAvailable => _nativeAvailable;

    // ─── Constructor ─────────────────────────────────────────────────────────

    /// <summary>
    /// Initialises the service and probes for native library availability.
    /// </summary>
    /// <param name="logger">Optional logger; pass <see langword="null"/> in tests.</param>
    public NativeSearchService(ILogger<NativeSearchService>? logger = null)
    {
        _logger = logger;
        _nativeAvailable = ProbeNativeLibrary();
    }

    // ─── Public API ──────────────────────────────────────────────────────────

    /// <inheritdoc />
    /// <remarks>
    /// Calls the native BMH implementation when available.
    /// Falls back to <see cref="string.IndexOf"/> with
    /// <see cref="StringComparison.OrdinalIgnoreCase"/> otherwise.
    /// </remarks>
    public int BmSearch(string text, string pattern)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern))
            return -1;

        if (_nativeAvailable)
        {
            try
            {
                return NativeSearchInterop.BmSearch(
                    text,    text.Length,
                    pattern, pattern.Length);
            }
            catch (Exception ex) when (IsPInvokeException(ex))
            {
                HandleNativeFault(ex, nameof(BmSearch));
            }
        }

        // Managed fallback
        return text.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Calls the native TF-IDF scorer when available.
    /// The managed fallback counts term occurrences (TF only, no IDF weighting)
    /// and normalises by document length as a simple relevance proxy.
    /// </remarks>
    public double ScoreDocument(string query, string document)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(document))
            return 0.0;

        if (_nativeAvailable)
        {
            try
            {
                return NativeSearchInterop.ScoreDocument(
                    query,    query.Length,
                    document, document.Length);
            }
            catch (Exception ex) when (IsPInvokeException(ex))
            {
                HandleNativeFault(ex, nameof(ScoreDocument));
            }
        }

        // Managed fallback — simple TF
        return ManagedScoreDocument(query, document);
    }

    // ─── Probe ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Performs a cheap test P/Invoke call to detect whether the native library
    /// is loadable and the entry points exist.  Returns <see langword="true"/>
    /// if the probe succeeds without throwing.
    /// </summary>
    private bool ProbeNativeLibrary()
    {
        try
        {
            // bm_search("a", 1, "b", 1) should return -1 without side effects.
            int probe = NativeSearchInterop.BmSearch("a", 1, "b", 1);
            _ = probe; // suppress unused warning

            _logger?.LogInformation(
                "[NativeSearch] Native search_engine library loaded successfully.");

            return true;
        }
        catch (DllNotFoundException ex)
        {
            _logger?.LogWarning(
                ex,
                "[NativeSearch] search_engine library not found. " +
                "Falling back to managed implementation. " +
                "Copy libsearch_engine.so / search_engine.dll " +
                "next to the application binary to enable native search.");
        }
        catch (EntryPointNotFoundException ex)
        {
            _logger?.LogWarning(
                ex,
                "[NativeSearch] Entry point not found in search_engine library. " +
                "The library may be an incompatible version.");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(
                ex,
                "[NativeSearch] Unexpected error probing native library.");
        }

        return false;
    }

    // ─── Fault handler ───────────────────────────────────────────────────────

    /// <summary>
    /// Handles a mid-operation P/Invoke failure: disables native calls for the
    /// remainder of the process lifetime and logs a warning.
    /// </summary>
    private void HandleNativeFault(Exception ex, string operation)
    {
        _nativeAvailable = false;

        _logger?.LogError(
            ex,
            "[NativeSearch] Native call failed during {Operation}. " +
            "Disabling native library and switching to managed fallback.",
            operation);
    }

    // ─── Exception classification ─────────────────────────────────────────────

    /// <summary>
    /// Returns <see langword="true"/> for exceptions that indicate a P/Invoke
    /// infrastructure failure rather than a logic error.
    /// </summary>
    private static bool IsPInvokeException(Exception ex) =>
        ex is DllNotFoundException        or
              EntryPointNotFoundException or
              SEHException                or
              BadImageFormatException;

    // ─── Managed fallback ─────────────────────────────────────────────────────

    // Common English stop words — mirrors the native C list.
    private static readonly string[] ManagedStopWords =
    [
        "a", "an", "the", "is", "it", "its", "in", "of",
        "to", "at", "by", "as", "and", "or", "for", "nor",
        "but", "with", "on", "into", "from", "be", "was", "are"
    ];

    /// <summary>
    /// Managed TF-only document scorer used when the native library is unavailable.
    /// </summary>
    private static double ManagedScoreDocument(string query, string document)
    {
        var docLower   = document.ToLowerInvariant();
        var queryTerms = query
            .ToLowerInvariant()
            .Split([' ', '\t', '\n', '\r', ',', '.', '!', '?', ';', ':', '-'],
                   StringSplitOptions.RemoveEmptyEntries);

        int   hitCount      = 0;
        int   uniqueTerms   = 0;
        int   docWordCount  = docLower.Split(' ',
                                  StringSplitOptions.RemoveEmptyEntries).Length;
        if (docWordCount == 0) docWordCount = 1;

        foreach (var raw in queryTerms)
        {
            if (raw.Length < 2 || Array.IndexOf(ManagedStopWords, raw) >= 0)
                continue;

            uniqueTerms++;
            int offset = 0;
            while (true)
            {
                int idx = docLower.IndexOf(raw, offset, StringComparison.Ordinal);
                if (idx < 0) break;
                hitCount++;
                offset = idx + raw.Length;
            }
        }

        if (uniqueTerms == 0 || hitCount == 0) return 0.0;

        double tf = (double)hitCount / (double)docWordCount;
        return Math.Min(1.0, tf / (tf + 1.0));
    }
}
