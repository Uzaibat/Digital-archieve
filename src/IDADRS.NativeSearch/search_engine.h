/*
 * search_engine.h — IDADRS High-Performance Native Search Engine
 * ---------------------------------------------------------------
 * Provides Boyer-Moore-Horspool substring search, simplified TF-IDF
 * document scoring, and text tokenisation.
 *
 * Compile targets:
 *   Linux/macOS : libsearch_engine.so
 *   Windows     : search_engine.dll
 *
 * C standard : C11
 */

#ifndef SEARCH_ENGINE_H
#define SEARCH_ENGINE_H

#ifdef __cplusplus
extern "C" {
#endif

/* ── Export macro ────────────────────────────────────────────────────────── */
#if defined(_WIN32) || defined(_WIN64)
  #ifdef SEARCH_ENGINE_EXPORTS
    #define IDADRS_API __declspec(dllexport)
  #else
    #define IDADRS_API __declspec(dllimport)
  #endif
#else
  /* GCC / Clang — mark symbol as publicly visible */
  #define IDADRS_API __attribute__((visibility("default")))
#endif

/* ── Constants ───────────────────────────────────────────────────────────── */

/** Maximum length of a single token produced by tokenize(). */
#define SE_MAX_TOKEN_LEN   64

/** Size of the Boyer-Moore-Horspool bad-character shift table (full ASCII). */
#define SE_BMH_ALPHABET    256

/** Maximum number of unique query terms scored by score_document(). */
#define SE_MAX_QUERY_TERMS 32

/* ── Function declarations ───────────────────────────────────────────────── */

/**
 * bm_search — Boyer-Moore-Horspool substring search (case-insensitive).
 *
 * Searches for the first occurrence of @p pattern inside @p text using the
 * BMH bad-character heuristic.  Both strings are lowercased internally;
 * the caller's buffers are never modified.
 *
 * @param text        Haystack string (need not be NUL-terminated if text_len
 *                    is provided correctly).
 * @param text_len    Length of @p text in bytes.  Pass -1 to use strlen().
 * @param pattern     Needle string.
 * @param pattern_len Length of @p pattern in bytes.  Pass -1 to use strlen().
 *
 * @return  Zero-based byte offset of the first match, or -1 if not found.
 *          Returns -1 on NULL input or if pattern_len > text_len.
 *
 * Complexity: O(n) average, O(nm) worst case (n = text, m = pattern).
 */
IDADRS_API int bm_search(const char *text,    int text_len,
                          const char *pattern, int pattern_len);


/**
 * score_document — Simplified TF-IDF relevance score.
 *
 * Tokenises @p query into individual terms, counts their occurrences in
 * @p document (term frequency), weights by an IDF approximation derived
 * from document length, and returns a score normalised to [0.0, 1.0].
 *
 * @param query     User search query string.
 * @param document  Document content to score against.
 * @param query_len Length of @p query.  Pass -1 to use strlen().
 * @param doc_len   Length of @p document.  Pass -1 to use strlen().
 *
 * @return  Relevance score in [0.0, 1.0].  Returns 0.0 on NULL input or
 *          empty document.
 */
IDADRS_API double score_document(const char *query,    int query_len,
                                  const char *document, int doc_len);


/**
 * tokenize — Split text into lowercase, stop-word-filtered tokens.
 *
 * Splits @p text on whitespace and ASCII punctuation, lowercases each token,
 * and removes common English stop words.  Tokens are written as newly
 * heap-allocated C strings into @p tokens_out.  The caller must free the
 * returned tokens with free_tokens().
 *
 * @param text       Input text to tokenise.
 * @param len        Length of @p text.  Pass -1 to use strlen().
 * @param tokens_out Pre-allocated array of (char*) with capacity @p max_tokens.
 * @param max_tokens Maximum number of token pointers @p tokens_out can hold.
 *
 * @return  Number of tokens written to @p tokens_out (always <= max_tokens).
 *          Returns 0 on NULL input.
 */
IDADRS_API int tokenize(const char *text, int len,
                         char **tokens_out, int max_tokens);


/**
 * free_tokens — Release memory allocated by tokenize().
 *
 * @param tokens  Array of token pointers returned by tokenize().
 * @param count   Number of valid pointers in @p tokens.
 */
IDADRS_API void free_tokens(char **tokens, int count);


#ifdef __cplusplus
}
#endif

#endif /* SEARCH_ENGINE_H */
