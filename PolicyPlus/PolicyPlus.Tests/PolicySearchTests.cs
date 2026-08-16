namespace PolicyPlus.Tests;

// Baseline regression tests capturing PolicySearch.BuildMatcher's CURRENT behavior, written
// before replacing its Microsoft.VisualBasic dependency (Strings.Trim/Split, ControlChars,
// LikeOperator.LikeString) with plain C#. A failing test after that change means real behavior
// changed, not just the implementation. No tests existed for this file before.
public class PolicySearchTests
{
    private static PolicyPlusPolicy Policy(string id, string title, string desc = "") => new()
    {
        UniqueID = id,
        DisplayName = title,
        DisplayExplanation = desc,
    };

    private static bool Matches(PolicyPlusPolicy policy, string query, bool checkTitle = true, bool checkDesc = false, bool checkId = false) =>
        PolicySearch.BuildMatcher(query, checkTitle, checkDesc, false, checkId, false)(policy);

    [Fact]
    public void PlainWord_MatchesWholeWordCaseInsensitively()
    {
        var p = Policy("id1", "Turn off Desktop Gadgets");
        Assert.True(Matches(p, "desktop"));
        Assert.True(Matches(p, "DESKTOP"));
        Assert.False(Matches(p, "top")); // whole-word match, not substring
    }

    [Fact]
    public void MultipleWords_RequireAllToMatch_AnyOrder()
    {
        var p = Policy("id1", "Turn off Desktop Gadgets");
        Assert.True(Matches(p, "off desktop"));
        Assert.True(Matches(p, "desktop off"));
        Assert.False(Matches(p, "off server"));
    }

    [Fact]
    public void StarWildcard_MatchesSubstringWithinAWord()
    {
        var p = Policy("id1", "Turn off Desktop Gadgets");
        Assert.True(Matches(p, "*top*"));
        Assert.True(Matches(p, "desk*"));
        Assert.False(Matches(p, "*xyz*"));
    }

    [Fact]
    public void QuestionMarkWildcard_MatchesExactlyOneCharacter()
    {
        var p = Policy("id1", "Cat");
        Assert.True(Matches(p, "C?t"));
        Assert.True(Matches(p, "Ca?"));
        Assert.False(Matches(p, "Ca??")); // one char too many
        Assert.False(Matches(p, "?at?")); // one char too few
    }

    [Fact]
    public void HashWildcard_MatchesExactlyOneDigit()
    {
        // A bare "#" with no "*"/"?" anywhere in the same token never gets classified as a
        // wildcard query at all (only "*"/"?" trigger that), so "#" is matched as a literal
        // character instead - its VB Like meaning only activates in a token that also has a
        // "*"/"?", which is why "?" is used here just to force wildcard classification.
        var p = Policy("Policy1", "");
        Assert.True(Matches(p, "P?licy#", checkId: true, checkTitle: false));
        Assert.False(Matches(p, "P?licy##", checkId: true, checkTitle: false)); // needs 2 digits, only has 1
        var p2 = Policy("Policy12", "");
        Assert.True(Matches(p2, "P?licy##", checkId: true, checkTitle: false));
        Assert.False(Matches(p2, "P?licy#", checkId: true, checkTitle: false)); // # needs exactly one digit; whole-string anchor leaves "2" unmatched
    }

    [Fact]
    public void QuotedPhrase_MatchesMultiWordLiteralAsSubstring()
    {
        var p = Policy("id1", "Turn off Desktop Gadgets entirely");
        Assert.True(Matches(p, "\"off Desktop\""));
        Assert.False(Matches(p, "\"Desktop off\"")); // wrong order
    }

    [Fact]
    public void IdMatch_PlainWordIsSubstring_ButWildcardPatternIsWholeStringAnchored()
    {
        // A plain (non-wildcarded) query word is substring-matched against the ID - this is the
        // literal point of ID search, so a partial ID like "AutoUpdateCfg" is found inside the
        // full "Namespace:AutoUpdateCfg". A *wildcard* pattern, though, is matched against the
        // WHOLE cleaned ID via VB Like's whole-string anchor, so it needs its own "*" to reach
        // across the "namespace:" prefix - this asymmetry is real existing behavior to preserve.
        var p = Policy("Namespace:AutoUpdateCfg", "");
        Assert.True(Matches(p, "AutoUpdateCfg", checkId: true, checkTitle: false));
        Assert.True(Matches(p, "*AutoUpdateCfg*", checkId: true, checkTitle: false));
        Assert.False(Matches(p, "AutoUpdateCfg*", checkId: true, checkTitle: false)); // anchored at start too - doesn't match the "namespace:" prefix
    }

    [Fact]
    public void CleanupStrips_SquareBracketsAndExclamation_BeforeMatching()
    {
        // cleanupStr strips ".,'\";/!(){}[]" from BOTH the query and the searched text before
        // any matching happens - so VB Like's [charlist]/[!charlist] syntax, which LikeOperator
        // itself supports, never actually reaches it intact through this API: the brackets are
        // gone before LikeString ever sees them. This test documents that as existing, load-bearing
        // behavior (not a bug to preserve/fix) so the C# replacement doesn't accidentally start
        // supporting charlist syntax and change behavior.
        var p = Policy("PolicyAB", "");
        // A query written as if "[AB]" were a charlist actually becomes the literal "ab" after
        // stripping - matches by coincidental literal-substring equality, not charlist semantics.
        Assert.True(Matches(p, "Policy[AB]", checkId: true, checkTitle: false));
        var p2 = Policy("PolicyC", "");
        Assert.False(Matches(p2, "Policy[AB]", checkId: true, checkTitle: false));
    }

    [Fact]
    public void ToSubstringQuery_WrapsPlainWordsButLeavesWildcardsAndQuotesAlone()
    {
        Assert.Equal("*desk*", PolicySearch.ToSubstringQuery("desk"));
        Assert.Equal("*desk* *top*", PolicySearch.ToSubstringQuery("desk top"));
        Assert.Equal("desk*", PolicySearch.ToSubstringQuery("desk*"));
        Assert.Equal("\"desk top\"", PolicySearch.ToSubstringQuery("\"desk top\""));
    }
}
