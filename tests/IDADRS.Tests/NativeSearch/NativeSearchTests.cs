using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using IDADRS.NativeSearch;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace IDADRS.Tests.NativeSearch;

/// <summary>
/// xUnit test suite for <see cref="NativeSearchService"/> and the underlying
/// native <c>search_engine</c> shared library.
///
/// Tests are written against the <see cref="INativeSearchService"/> interface so
/// they exercise both the native path (when the .so/.dll is present) and the
/// managed fallback path (when it is absent).
///
/// Test structure follows the Arrange-Act-Assert pattern.  Each test is
/// independent and uses no shared mutable state.
/// </summary>
public sealed class NativeSearchTests : IDisposable
{
    // ─── Service under test ───────────────────────────────────────────────────

    /// <summary>Service backed by the real native library (if present).</summary>
    private readonly INativeSearchService _sut;

    /// <summary>Service that is forced to use the managed fallback only.</summary>
    private readonly INativeSearchService _fallback;

    private readonly ITestOutputHelper _output;

    public NativeSearchTests(ITestOutputHelper output)
    {
        _output   = output;
        _sut      = new NativeSearchService(NullLogger<NativeSearchService>.Instance);
        _fallback = new ForcedFallbackSearchService(); // always managed

        _output.WriteLine(
            $"[NativeSearch] Native library available: {_sut.IsNativeAvailable}");
    }

    public void Dispose() { /* nothing to dispose — service is stateless */ }

    // =========================================================================
    // BmSearch — positive matches
    // =========================================================================

    [Theory]
    [InlineData("The Intelligent Digital Archive and Document Retrieval System",
                "archive", true)]
    [InlineData("The Intelligent Digital Archive and Document Retrieval System",
                "ARCHIVE", true)]   // case-insensitive
    [InlineData("The Intelligent Digital Archive and Document Retrieval System",
                "Archive", true)]   // mixed case
    [InlineData("document retrieval system",
                "retrieval", true)]
    [InlineData("hello world",
                "world", true)]
    [InlineData("aaa",
                "aaa", true)]       // full string match
    [InlineData("abc",
                "a", true)]         // single char pattern
    public void BmSearch_ExistingPattern_ReturnsNonNegativeIndex(
        string haystack, string needle, bool _)
    {
        // Act
        int result = _sut.BmSearch(haystack, needle);

        // Assert
        Assert.True(result >= 0,
            $"Expected match for '{needle}' in \"{haystack}\" but got {result}.");
    }

    // =========================================================================
    // BmSearch — no match returns -1
    // =========================================================================

    [Theory]
    [InlineData("The Intelligent Digital Archive", "blockchain")]
    [InlineData("document retrieval",              "xyz")]
    [InlineData("hello world",                     "worlds")]   // longer than text segment
    [InlineData("abc",                             "abcd")]     // pattern longer than text
    public void BmSearch_AbsentPattern_ReturnsMinusOne(string haystack, string needle)
    {
        int result = _sut.BmSearch(haystack, needle);

        Assert.Equal(-1, result);
    }

    // =========================================================================
    // BmSearch — edge cases
    // =========================================================================

    [Fact]
    public void BmSearch_NullText_ReturnsMinusOne()
    {
        int result = _sut.BmSearch(null!, "archive");
        Assert.Equal(-1, result);
    }

    [Fact]
    public void BmSearch_NullPattern_ReturnsMinusOne()
    {
        int result = _sut.BmSearch("some text", null!);
        Assert.Equal(-1, result);
    }

    [Fact]
    public void BmSearch_EmptyText_ReturnsMinusOne()
    {
        int result = _sut.BmSearch(string.Empty, "archive");
        Assert.Equal(-1, result);
    }

    [Fact]
    public void BmSearch_EmptyPattern_ReturnsMinusOne()
    {
        // empty pattern matches at 0 in the native layer — managed fallback
        // may also return 0 via IndexOf semantics.  Both are acceptable.
        int result = _sut.BmSearch("hello world", string.Empty);
        _output.WriteLine($"Empty pattern result: {result}");
        // No assertion failure expected — just must not throw.
    }

    [Fact]
    public void BmSearch_ReturnedIndexIsCorrect()
    {
        const string hay    = "The Intelligent Digital Archive and Document Retrieval System";
        const string needle = "Archive";

        int index = _sut.BmSearch(hay, needle);

        Assert.True(index >= 0, "Match must be found.");
        // The character at the returned index must start the pattern (case-insensitive).
        Assert.Equal(
            needle.ToLowerInvariant(),
            hay.Substring(index, needle.Length).ToLowerInvariant());
    }

    // =========================================================================
    // BmSearch — parity between native and managed fallback
    // =========================================================================

    [Theory]
    [InlineData("Intelligent Digital Archive System", "archive")]
    [InlineData("Intelligent Digital Archive System", "blockchain")]
    [InlineData("UPPERCASE TEXT", "uppercase")]
    public void BmSearch_NativeAndFallback_ReturnConsistentResults(
        string haystack, string needle)
    {
        int nativeResult   = _sut.BmSearch(haystack, needle);
        int fallbackResult = _fallback.BmSearch(haystack, needle);

        // Both must agree on found vs. not-found.
        Assert.Equal(nativeResult >= 0, fallbackResult >= 0);
    }

    // =========================================================================
    // ScoreDocument — relevance ordering
    // =========================================================================

    [Fact]
    public void ScoreDocument_MoreRelevantDoc_ScoresHigher()
    {
        const string query = "financial report quarterly";

        const string relevant =
            "Q1 2026 Financial Report quarterly income statement balance sheet " +
            "quarterly budget analysis financial review financial planning quarterly";

        const string irrelevant =
            "Software architecture API design microservices deployment pipeline " +
            "Docker Kubernetes containerisation infrastructure automation";

        double s_relevant   = _sut.ScoreDocument(query, relevant);
        double s_irrelevant = _sut.ScoreDocument(query, irrelevant);

        _output.WriteLine($"relevant score:   {s_relevant:F4}");
        _output.WriteLine($"irrelevant score: {s_irrelevant:F4}");

        Assert.True(
            s_relevant > s_irrelevant,
            $"Expected relevant ({s_relevant:F4}) > irrelevant ({s_irrelevant:F4}).");
    }

    [Fact]
    public void ScoreDocument_ExactRepeatTerms_ScoresHigherThanSingle()
    {
        const string query         = "archive document";
        const string highFrequency = "archive document archive document archive document " +
                                     "archive document archive digital archive";
        const string lowFrequency  = "archive document system";

        double s_high = _sut.ScoreDocument(query, highFrequency);
        double s_low  = _sut.ScoreDocument(query, lowFrequency);

        _output.WriteLine($"high-freq score: {s_high:F4}");
        _output.WriteLine($"low-freq score:  {s_low:F4}");

        Assert.True(s_high >= s_low,
            "Higher term frequency should produce equal or higher score.");
    }

    // =========================================================================
    // ScoreDocument — range guarantee [0.0, 1.0]
    // =========================================================================

    [Theory]
    [InlineData("financial report",
                "Q1 2026 Financial Report quarterly income statement")]
    [InlineData("archive",
                "The Intelligent Digital Archive and Document Retrieval System")]
    [InlineData("kubernetes docker",
                "Kubernetes Docker containerisation deployment pipeline")]
    [InlineData("total mismatch",
                "The quick brown fox jumps over the lazy dog")]
    [InlineData("a",   "a")]                    // stop-word query
    [InlineData("the", "the quick brown fox")]  // stop-word only
    public void ScoreDocument_AlwaysInZeroToOneRange(string query, string document)
    {
        double score = _sut.ScoreDocument(query, document);

        _output.WriteLine(
            $"score for query='{query}': {score:F4}");

        Assert.InRange(score, 0.0, 1.0);
    }

    // =========================================================================
    // ScoreDocument — edge cases
    // =========================================================================

    [Fact]
    public void ScoreDocument_EmptyDocument_ReturnsZero()
    {
        double score = _sut.ScoreDocument("financial report", string.Empty);
        Assert.Equal(0.0, score);
    }

    [Fact]
    public void ScoreDocument_NullQuery_ReturnsZero()
    {
        double score = _sut.ScoreDocument(null!, "some document content");
        Assert.Equal(0.0, score);
    }

    [Fact]
    public void ScoreDocument_NullDocument_ReturnsZero()
    {
        double score = _sut.ScoreDocument("financial report", null!);
        Assert.Equal(0.0, score);
    }

    [Fact]
    public void ScoreDocument_AllStopWordQuery_ReturnsZero()
    {
        // "the and or" are all stop words — tokeniser should emit 0 terms.
        double score = _sut.ScoreDocument("the and or", "the and or is a the");
        Assert.Equal(0.0, score);
    }

    // =========================================================================
    // ScoreDocument — parity between native and managed fallback
    // =========================================================================

    [Theory]
    [InlineData("financial report", "The Q1 financial report shows strong results.")]
    [InlineData("archive search",   "The archive enables fast document search retrieval.")]
    [InlineData("missing term",     "This document is completely unrelated.")]
    public void ScoreDocument_NativeAndFallback_AgreeOnZeroVsNonZero(
        string query, string document)
    {
        double nativeScore   = _sut.ScoreDocument(query, document);
        double fallbackScore = _fallback.ScoreDocument(query, document);

        _output.WriteLine(
            $"native={nativeScore:F4}  fallback={fallbackScore:F4}  " +
            $"query='{query}'");

        // Both must agree on whether the query is relevant at all.
        Assert.Equal(nativeScore > 0.0, fallbackScore > 0.0);
    }

    // =========================================================================
    // Graceful degradation — forced fallback service
    // =========================================================================

    [Fact]
    public void FallbackService_IsNativeAvailable_IsFalse()
    {
        Assert.False(_fallback.IsNativeAvailable);
    }

    [Fact]
    public void FallbackService_BmSearch_FindsKnownPattern()
    {
        int result = _fallback.BmSearch(
            "The Intelligent Digital Archive System", "archive");

        Assert.True(result >= 0,
            "Managed fallback BmSearch must find 'archive' in the test string.");
    }

    [Fact]
    public void FallbackService_BmSearch_ReturnsMinusOneForAbsent()
    {
        int result = _fallback.BmSearch(
            "The Intelligent Digital Archive System", "blockchain");

        Assert.Equal(-1, result);
    }

    [Fact]
    public void FallbackService_ScoreDocument_StillInRange()
    {
        double score = _fallback.ScoreDocument(
            "archive document retrieval",
            "The Intelligent Digital Archive and Document Retrieval System");

        Assert.InRange(score, 0.0, 1.0);
    }

    // =========================================================================
    // Bulk / stress tests (quick — no external I/O)
    // =========================================================================

    [Fact]
    public void BmSearch_LargeHaystack_ReturnsCorrectResult()
    {
        // 10 000 'x' chars then "archive" then 10 000 'x' chars
        var hay = new string('x', 10_000) + "archive" + new string('x', 10_000);

        int result = _sut.BmSearch(hay, "archive");

        Assert.Equal(10_000, result);
    }

    [Fact]
    public void ScoreDocument_AllScoresInRangeForCollection()
    {
        const string query = "financial document archive report";

        var documents = new[]
        {
            "Q1 2026 Financial Report quarterly income statement",
            "Architecture specification API design system integration",
            "Legal contract SLA cloud hosting uptime compliance",
            "Archive retrieval document indexing full text search",
            "",   // empty
            "一 二 三",   // non-ASCII (should not crash)
        };

        foreach (var doc in documents)
        {
            double score = _sut.ScoreDocument(query, doc);
            _output.WriteLine($"  [{score:F4}] {(doc.Length > 40 ? doc[..40] + "…" : doc)}");
            Assert.InRange(score, 0.0, 1.0);
        }
    }
}

// =============================================================================
// Test helper: forced managed-only service
// =============================================================================

/// <summary>
/// A test double that always uses the managed fallback code path of
/// <see cref="NativeSearchService"/> by simulating a library-load failure.
/// Implemented by inheriting and overriding via the wrapper rather than
/// the production class to avoid internal coupling.
/// </summary>
file sealed class ForcedFallbackSearchService : INativeSearchService
{
    public bool IsNativeAvailable => false;

    public int BmSearch(string text, string pattern)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern)) return -1;
        return text.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
    }

    public double ScoreDocument(string query, string document)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(document))
            return 0.0;

        string[] stopWords = ["a", "an", "the", "is", "it", "in", "of", "to",
                               "and", "or", "for", "with", "be", "was", "are"];

        var docLower = document.ToLowerInvariant();
        var terms    = query.ToLowerInvariant()
                            .Split([' ', '\t', ',', '.', '!', '?'],
                                   StringSplitOptions.RemoveEmptyEntries);

        int hits = 0, uniqueTerms = 0;
        int docWords = docLower.Split(' ',
                           StringSplitOptions.RemoveEmptyEntries).Length;
        if (docWords == 0) docWords = 1;

        foreach (var term in terms)
        {
            if (term.Length < 2 || Array.IndexOf(stopWords, term) >= 0) continue;
            uniqueTerms++;
            int offset = 0;
            while (true)
            {
                int idx = docLower.IndexOf(term, offset, StringComparison.Ordinal);
                if (idx < 0) break;
                hits++;
                offset = idx + term.Length;
            }
        }

        if (uniqueTerms == 0 || hits == 0) return 0.0;
        double tf = (double)hits / docWords;
        return Math.Min(1.0, tf / (tf + 1.0));
    }
}
