/*
 * search_engine.c — IDADRS High-Performance Native Search Engine
 * ---------------------------------------------------------------
 * Implements:
 *   1. bm_search()       — Boyer-Moore-Horspool (case-insensitive)
 *   2. score_document()  — Simplified TF-IDF, normalised [0.0–1.0]
 *   3. tokenize()        — Whitespace/punctuation split, lowercase, stop-word removal
 *   4. free_tokens()     — Paired deallocator for tokenize() output
 *
 * C standard : C11
 * Compile    : gcc -O2 -Wall -std=c11 -shared -fPIC -o libsearch_engine.so search_engine.c
 */

#define SEARCH_ENGINE_EXPORTS   /* activate dllexport on Windows */

#include "search_engine.h"

#include <ctype.h>
#include <math.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>


/* ═══════════════════════════════════════════════════════════════════════════
 * Internal helpers
 * ═══════════════════════════════════════════════════════════════════════════ */

/**
 * safe_len — returns strlen(s) if s is non-NULL and hint == -1,
 *            returns (size_t)hint when the caller already knows the length,
 *            returns 0 for NULL.
 */
static inline size_t safe_len(const char *s, int hint)
{
    if (!s) return 0;
    return (hint < 0) ? strlen(s) : (size_t)hint;
}

/**
 * ascii_lower — maps an ASCII byte to its lowercase equivalent.
 * Does NOT call the locale-dependent tolower() to keep behaviour
 * deterministic across all platforms.
 */
static inline unsigned char ascii_lower(unsigned char c)
{
    return (c >= 'A' && c <= 'Z') ? (unsigned char)(c + 32) : c;
}

/**
 * is_delimiter — returns non-zero if the character is a word boundary
 * (whitespace or ASCII punctuation).
 */
static inline int is_delimiter(unsigned char c)
{
    /* Treat NUL, control chars, space, and printable punctuation as delimiters.
     * Alphanumeric characters are NOT delimiters. */
    if (c == '\0') return 1;
    if (c <= ' ')  return 1;           /* space, tab, CR, LF, other controls */
    /* ASCII punctuation ranges: 33-47, 58-64, 91-96, 123-126 */
    if (c >= 33 && c <= 47)  return 1;
    if (c >= 58 && c <= 64)  return 1;
    if (c >= 91 && c <= 96)  return 1;
    if (c >= 123 && c <= 126) return 1;
    return 0;
}

/* ── English stop words ─────────────────────────────────────────────────── */
static const char * const STOP_WORDS[] = {
    "a", "an", "the",
    "is", "it", "its",
    "in", "of", "to", "at", "by", "as",
    "and", "or", "for", "nor", "but",
    "with", "on", "into", "from", "be", "was", "are",
    NULL   /* sentinel */
};

static int is_stop_word(const char *token)
{
    for (int i = 0; STOP_WORDS[i] != NULL; i++) {
        if (strcmp(token, STOP_WORDS[i]) == 0) return 1;
    }
    return 0;
}


/* ═══════════════════════════════════════════════════════════════════════════
 * 1. Boyer-Moore-Horspool substring search
 * ═══════════════════════════════════════════════════════════════════════════ */

/**
 * build_shift_table — fills the BMH bad-character shift table.
 *
 * shift[c] = distance to shift the pattern window when character c is found
 *            at the current alignment point and does not match.
 *
 * @param pattern    Lowercase pattern bytes.
 * @param m          Pattern length.
 * @param shift      Output array of size SE_BMH_ALPHABET.
 */
static void build_shift_table(const unsigned char *pattern,
                               size_t m,
                               size_t shift[SE_BMH_ALPHABET])
{
    /* Default: shift by the full pattern length. */
    for (int i = 0; i < SE_BMH_ALPHABET; i++) {
        shift[i] = m;
    }
    /* For each character that appears in the pattern (except the last),
     * record the distance from the last occurrence to the right edge. */
    for (size_t i = 0; i < m - 1; i++) {
        shift[pattern[i]] = m - 1 - i;
    }
}

/*
 * bm_search — see header for full documentation.
 *
 * Implementation notes:
 *   • We copy both strings into heap buffers, lowercase them, then run BMH.
 *   • Heap allocation keeps the caller's strings immutable and avoids VLAs
 *     (which are optional in C11 and can blow the stack for large inputs).
 *   • For very short patterns (length 1) we fall back to a linear scan which
 *     is faster than BMH setup overhead.
 */
IDADRS_API int bm_search(const char *text,    int text_len,
                          const char *pattern, int pattern_len)
{
    if (!text || !pattern) return -1;

    size_t n = safe_len(text,    text_len);
    size_t m = safe_len(pattern, pattern_len);

    if (m == 0) return 0;    /* empty pattern matches at position 0 */
    if (m > n)  return -1;

    /* ── Lowercase copies ─────────────────────────────────────────────────── */
    unsigned char *lc_text    = (unsigned char *)malloc(n);
    unsigned char *lc_pattern = (unsigned char *)malloc(m);

    if (!lc_text || !lc_pattern) {
        free(lc_text);
        free(lc_pattern);
        return -1;  /* OOM — gracefully report no match */
    }

    for (size_t i = 0; i < n; i++) lc_text[i]    = ascii_lower((unsigned char)text[i]);
    for (size_t i = 0; i < m; i++) lc_pattern[i] = ascii_lower((unsigned char)pattern[i]);

    /* ── Fast path: single-character pattern ─────────────────────────────── */
    if (m == 1) {
        for (size_t i = 0; i < n; i++) {
            if (lc_text[i] == lc_pattern[0]) {
                free(lc_text);
                free(lc_pattern);
                return (int)i;
            }
        }
        free(lc_text);
        free(lc_pattern);
        return -1;
    }

    /* ── Build shift table ───────────────────────────────────────────────── */
    size_t shift[SE_BMH_ALPHABET];
    build_shift_table(lc_pattern, m, shift);

    /* ── BMH scan ────────────────────────────────────────────────────────── */
    size_t i = 0;
    while (i <= n - m) {
        size_t j = m - 1;

        /* Match from right to left */
        while (j < m && lc_text[i + j] == lc_pattern[j]) {
            if (j == 0) {
                free(lc_text);
                free(lc_pattern);
                return (int)i;   /* full match at position i */
            }
            j--;
        }

        /* Shift window using bad-character heuristic on the rightmost character */
        i += shift[lc_text[i + m - 1]];
    }

    free(lc_text);
    free(lc_pattern);
    return -1;
}


/* ═══════════════════════════════════════════════════════════════════════════
 * 3. Tokeniser  (declared before score_document which depends on it)
 * ═══════════════════════════════════════════════════════════════════════════ */

/*
 * tokenize — see header for full documentation.
 *
 * Implementation:
 *   • Walks the input byte-by-byte, identifying word spans bounded by
 *     is_delimiter() characters.
 *   • Each candidate token is lowercased into a fixed 64-byte stack buffer,
 *     then checked against the stop-word list.
 *   • Accepted tokens are heap-allocated (strdup) and stored in tokens_out.
 */
IDADRS_API int tokenize(const char *text, int len,
                         char **tokens_out, int max_tokens)
{
    if (!text || !tokens_out || max_tokens <= 0) return 0;

    size_t n      = safe_len(text, len);
    int    count  = 0;
    size_t i      = 0;

    while (i < n && count < max_tokens) {
        /* Skip delimiter bytes */
        while (i < n && is_delimiter((unsigned char)text[i])) i++;
        if (i >= n) break;

        /* Find end of this word span */
        size_t word_start = i;
        while (i < n && !is_delimiter((unsigned char)text[i])) i++;
        size_t word_len = i - word_start;

        /* Truncate to SE_MAX_TOKEN_LEN - 1 to avoid overflow */
        if (word_len >= SE_MAX_TOKEN_LEN) {
            word_len = SE_MAX_TOKEN_LEN - 1;
        }

        /* Lowercase into stack buffer */
        char buf[SE_MAX_TOKEN_LEN];
        for (size_t k = 0; k < word_len; k++) {
            buf[k] = (char)ascii_lower((unsigned char)text[word_start + k]);
        }
        buf[word_len] = '\0';

        /* Skip stop words and single-character tokens */
        if (word_len < 2 || is_stop_word(buf)) continue;

        /* Heap-allocate and store */
        char *tok = (char *)malloc(word_len + 1);
        if (!tok) break;   /* OOM — return what we have */
        memcpy(tok, buf, word_len + 1);
        tokens_out[count++] = tok;
    }

    return count;
}


/*
 * free_tokens — see header for full documentation.
 */
IDADRS_API void free_tokens(char **tokens, int count)
{
    if (!tokens) return;
    for (int i = 0; i < count; i++) {
        free(tokens[i]);
        tokens[i] = NULL;
    }
}


/* ═══════════════════════════════════════════════════════════════════════════
 * 2. Simplified TF-IDF document scorer
 * ═══════════════════════════════════════════════════════════════════════════ */

/*
 * count_occurrences — counts how many times the lowercase token @p term
 * appears as a substring in the lowercase document @p doc of length @p doc_len.
 *
 * Uses bm_search() for each search pass, advancing past each found occurrence.
 */
static int count_occurrences(const char *doc, int doc_len,
                              const char *term, int term_len)
{
    int count  = 0;
    int offset = 0;
    int remaining = doc_len;

    while (remaining > 0) {
        int pos = bm_search(doc + offset, remaining, term, term_len);
        if (pos < 0) break;
        count++;
        /* Advance past this occurrence */
        int advance = pos + term_len;
        offset    += advance;
        remaining -= advance;
    }

    return count;
}

/*
 * score_document — see header for full documentation.
 *
 * Scoring formula:
 *   For each query term t:
 *     tf(t)  = count of t in document / total_doc_tokens  (frequency ratio)
 *     idf(t) = log(1 + doc_len / (1 + tf_raw))            (length-adjusted IDF)
 *     contrib(t) = tf(t) * idf(t)
 *
 *   raw_score = sum(contrib(t)) / num_query_terms
 *
 *   normalised_score = raw_score / (raw_score + 1.0)     (squash to [0,1))
 *
 * The normalisation via x/(x+1) guarantees the output is always in [0.0, 1.0).
 * 1.0 is approached asymptotically for extremely relevant documents.
 */
IDADRS_API double score_document(const char *query,    int query_len,
                                  const char *document, int doc_len)
{
    if (!query || !document) return 0.0;

    size_t n_doc   = safe_len(document, doc_len);
    size_t n_query = safe_len(query,    query_len);

    if (n_doc == 0 || n_query == 0) return 0.0;

    /* ── Tokenise query ──────────────────────────────────────────────────── */
    char *q_tokens[SE_MAX_QUERY_TERMS];
    int   n_qterms = tokenize(query, (int)n_query, q_tokens, SE_MAX_QUERY_TERMS);
    if (n_qterms == 0) return 0.0;

    /* ── Estimate total doc tokens (used as denominator for TF) ─────────── */
    /*    Rough approximation: count whitespace-separated spans.             */
    int total_doc_tokens = 0;
    int in_word = 0;
    for (size_t i = 0; i < n_doc; i++) {
        if (!is_delimiter((unsigned char)document[i])) {
            if (!in_word) { total_doc_tokens++; in_word = 1; }
        } else {
            in_word = 0;
        }
    }
    if (total_doc_tokens == 0) total_doc_tokens = 1;

    /* ── Compute TF-IDF sum ──────────────────────────────────────────────── */
    double score_sum = 0.0;

    for (int t = 0; t < n_qterms; t++) {
        int   term_len = (int)strlen(q_tokens[t]);
        int   tf_raw   = count_occurrences(document, (int)n_doc,
                                            q_tokens[t], term_len);

        if (tf_raw == 0) continue;

        /* Term frequency ratio */
        double tf = (double)tf_raw / (double)total_doc_tokens;

        /* IDF approximation: longer documents should not dominate simply
         * because they contain the word more times.
         *
         *   idf = log2( 1.0 + (double)n_doc / (1.0 + tf_raw) )
         *
         * This penalises very short documents with a single hit and
         * rewards documents with a high hit density relative to length.
         */
        double idf = log2(1.0 + (double)n_doc / (1.0 + (double)tf_raw));

        score_sum += tf * idf;
    }

    free_tokens(q_tokens, n_qterms);

    if (score_sum <= 0.0) return 0.0;

    /* ── Normalise per query term, then squash to [0.0, 1.0) ─────────────── */
    double raw = score_sum / (double)n_qterms;

    /* x / (x + 1): monotonically increasing, bounded in [0, 1). */
    double normalised = raw / (raw + 1.0);

    /* Clamp for floating-point safety */
    if (normalised < 0.0) normalised = 0.0;
    if (normalised > 1.0) normalised = 1.0;

    return normalised;
}
