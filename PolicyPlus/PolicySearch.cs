using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

// Shared policy text-search matcher, used by both the modal Find dialog (FindByText.cs)
// and Main's always-visible live search box.
public static class PolicySearch
{
    // Rewrites a plain-text query into one where every bare word becomes a *substring* wildcard
    // match (e.g. "desk" -> "*desk*"), leaving already-quoted phrases and already-wildcarded
    // words untouched. BuildMatcher's plain-word matching requires a whole-word exact match,
    // which is right for the deliberate modal Find dialog but wrong for the toolbar search box -
    // typing "desk" should find "Desktop" without the user having to type a wildcard themselves.
    public static string ToSubstringQuery(string RawQuery)
    {
        var tokens = RawQuery.Split(' ');
        var result = new List<string>();
        bool inQuote = false;
        foreach (var token in tokens)
        {
            if (token.Length == 0) continue;
            if (inQuote)
            {
                result.Add(token);
                if (token.EndsWith("\"")) inQuote = false;
            }
            else if (token.StartsWith("\""))
            {
                result.Add(token);
                if (!token.EndsWith("\"") || token.Length == 1) inQuote = true;
            }
            else if (token.Contains('*') || token.Contains('?'))
            {
                result.Add(token);
            }
            else
            {
                result.Add("*" + token + "*");
            }
        }
        return string.Join(" ", result);
    }

    // Translates a VB "Like"-style wildcard pattern ("*" = any run, "?" = any single char, "#" =
    // any single digit) into an anchored Regex matching the whole string, ordinal/no culture
    // handling (matching VB Like's CompareMethod.Binary this replaces). Query text is already
    // stripped of "[]!" by cleanupStr below before reaching here, so [charlist]/[!charlist]
    // syntax never actually appears in a pattern this sees and isn't translated.
    private static Regex LikePatternToRegex(string pattern)
    {
        var sb = new StringBuilder("^");
        foreach (char c in pattern)
        {
            switch (c)
            {
                case '*': sb.Append(".*"); break;
                case '?': sb.Append('.'); break;
                case '#': sb.Append("[0-9]"); break;
                default: sb.Append(Regex.Escape(c.ToString())); break;
            }
        }
        sb.Append('$');
        return new Regex(sb.ToString(), RegexOptions.Singleline);
    }

    public static Func<PolicyPlusPolicy, bool> BuildMatcher(string QueryText, bool CheckTitle, bool CheckDesc, bool CheckComment, bool CheckID, bool CheckRegistry, params Dictionary<string, string>[] CommentSources)
    {
        var validCommentSources = CommentSources.Where(d => d is not null).ToArray();
        string cleanupStr(string RawText) => new string(RawText.ToLowerInvariant().Trim().Where(c => !".,'\";/!(){}[]".Contains(c)).ToArray());
        // Parse the query string for wildcards or quoted strings - done once here rather than
        // per policy, since the query itself doesn't change across the thousands of policies
        // this matcher gets run against during a single search
        string[] rawSplitted = QueryText.Split(' ');
        var simpleWords = new List<string>();
        var wildcards = new List<string>();
        var wildcardRegexes = new List<Regex>();
        var quotedStrings = new List<string>();
        string partialQuotedString = "";
        for (int n = 0, loopTo = rawSplitted.Length - 1; n <= loopTo; n++)
        {
            string curString = rawSplitted[n];
            if (!string.IsNullOrEmpty(partialQuotedString))
            {
                partialQuotedString += curString + " ";
                if (curString.EndsWith("\""))
                {
                    quotedStrings.Add(cleanupStr(partialQuotedString));
                    partialQuotedString = "";
                }
            }
            else if (curString.StartsWith("\""))
            {
                partialQuotedString = curString + " ";
            }
            else if (curString.Contains("*") | curString.Contains("?"))
            {
                string cleaned = cleanupStr(curString);
                wildcards.Add(cleaned);
                wildcardRegexes.Add(LikePatternToRegex(cleaned));
            }
            else
            {
                simpleWords.Add(cleanupStr(curString));
            }
        }
        return (Policy) =>
        {
            // Do the searching
            bool isStringAHit(string SearchedText)
            {
                string cleanText = cleanupStr(SearchedText);
                string[] wordsInText = cleanText.Split(' ', '\r', '\n');
                return simpleWords.All(w => wordsInText.Contains(w)) & wildcardRegexes.All(re => wordsInText.Any(wit => re.IsMatch(wit))) & quotedStrings.All(w => cleanText.Contains(" " + w + " ") | cleanText.StartsWith(w + " ") | cleanText.EndsWith(" " + w) | (cleanText ?? "") == (w ?? "")); // Plain search terms
                                                                                                                                                                                                                                                                                                                                     // Wildcards
                                                                                                                                                                                                                                                                                                                                     // Quoted strings
            };
            // Policy IDs are a single unbroken token (e.g. "Namespace:PolicyName") with no internal
            // whitespace, so isStringAHit's word-tokenized whole-word matching would wrongly require
            // the query to match the *entire* ID. Match query terms as substrings of the whole ID
            // instead, so a partial ID like "AutoUpdateCfg" is actually found.
            bool isIdAHit(string SearchedText)
            {
                string cleanText = cleanupStr(SearchedText);
                return simpleWords.All(w => cleanText.Contains(w)) & wildcardRegexes.All(re => re.IsMatch(cleanText)) & quotedStrings.All(w => cleanText.Contains(w));
            };
            if (CheckTitle)
            {
                if (isStringAHit(Policy.DisplayName))
                    return true;
            }
            if (CheckDesc)
            {
                if (isStringAHit(Policy.DisplayExplanation))
                    return true;
            }
            if (CheckComment)
            {
                if (validCommentSources.Any((Source) => Source.ContainsKey(Policy.UniqueID) && isStringAHit(Source[Policy.UniqueID])))
                    return true;
            }
            if (CheckID)
            {
                if (isIdAHit(Policy.UniqueID))
                    return true;
            }
            if (CheckRegistry)
            {
                // Same whole-string substring matching as CheckID - a registry key/value name is
                // also a single unbroken token, not word-tokenized text
                if (PolicyProcessing.GetReferencedRegistryValues(Policy).Any(rkvp => isIdAHit(rkvp.Key + @"\" + rkvp.Value)))
                    return true;
            }
            return false;
        };
    }
}
