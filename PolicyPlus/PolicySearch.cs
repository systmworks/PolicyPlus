using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;

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

    public static Func<PolicyPlusPolicy, bool> BuildMatcher(string QueryText, bool CheckTitle, bool CheckDesc, bool CheckComment, params Dictionary<string, string>[] CommentSources)
    {
        var validCommentSources = CommentSources.Where(d => d is not null).ToArray();
        string cleanupStr(string RawText) => new string(Strings.Trim(RawText.ToLowerInvariant()).Where(c => !".,'\";/!(){}[]".Contains(c)).ToArray());
        // Parse the query string for wildcards or quoted strings - done once here rather than
        // per policy, since the query itself doesn't change across the thousands of policies
        // this matcher gets run against during a single search
        string[] rawSplitted = Strings.Split(QueryText);
        var simpleWords = new List<string>();
        var wildcards = new List<string>();
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
                wildcards.Add(cleanupStr(curString));
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
                string[] wordsInText = cleanText.Split(' ', ControlChars.Cr, ControlChars.Lf);
                return simpleWords.All(w => wordsInText.Contains(w)) & wildcards.All(w => wordsInText.Any(wit => LikeOperator.LikeString(wit, w, CompareMethod.Binary))) & quotedStrings.All(w => cleanText.Contains(" " + w + " ") | cleanText.StartsWith(w + " ") | cleanText.EndsWith(" " + w) | (cleanText ?? "") == (w ?? "")); // Plain search terms
                                                                                                                                                                                                                                                                                                                                     // Wildcards
                                                                                                                                                                                                                                                                                                                                     // Quoted strings
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
            return false;
        };
    }
}
