using Microsoft.Win32;

namespace PolicyPlus.Tests;

// Baseline regression tests capturing PolFile's CURRENT behavior (PolicySource.cs),
// written before any redesign work starts. These exist so a future rewrite of PolFile's
// internal representation (replacing the SortedDictionary + sentinel-marker scheme with
// an explicit-state key tree) has something concrete to check itself against -- a failing
// test here should mean "the redesign changed real behavior," not "the registry got
// corrupted in a live GPO." See the plan file's Phase 1 for context.
public class PolFileTests
{
    private const string Key = @"software\policies\test1";

    [Fact]
    public void SetValue_ThenGetValue_RoundTrips()
    {
        var pol = new PolFile();
        pol.SetValue(Key, "Foo", 42u, RegistryValueKind.DWord);

        Assert.True(pol.ContainsValue(Key, "Foo"));
        Assert.Equal(42u, pol.GetValue(Key, "Foo"));
        Assert.False(pol.WillDeleteValue(Key, "Foo"));
    }

    [Fact]
    public void DeleteValue_MarksValueAsWillDelete()
    {
        var pol = new PolFile();
        pol.SetValue(Key, "Foo", 1u, RegistryValueKind.DWord);
        pol.DeleteValue(Key, "Foo");

        Assert.True(pol.WillDeleteValue(Key, "Foo"));
        Assert.False(pol.ContainsValue(Key, "Foo"));
    }

    [Fact]
    public void ForgetValue_RemovesAllKnowledgeOfValue()
    {
        var pol = new PolFile();
        pol.SetValue(Key, "Foo", 1u, RegistryValueKind.DWord);
        pol.DeleteValue(Key, "Foo");
        pol.ForgetValue(Key, "Foo");

        Assert.False(pol.WillDeleteValue(Key, "Foo"));
        Assert.False(pol.ContainsValue(Key, "Foo"));
    }

    // Burden (a) from the redesign plan: set a value, clear its key, then set the value
    // again -- all in one session, before any Save. The value must survive, not be
    // treated as deleted. Today's code gets this right only via the sort-order trick
    // ("**delvals." sorts before a literal value name because '*' < any letter/digit) --
    // this test is the one that must keep passing through any redesign.
    [Fact]
    public void ClearKey_ThenSetValueAgain_ValueSurvives()
    {
        var pol = new PolFile();
        pol.SetValue(Key, "Foo", "bar", RegistryValueKind.String);

        pol.ClearKey(Key);
        Assert.True(pol.WillDeleteValue(Key, "Foo"));
        Assert.False(pol.ContainsValue(Key, "Foo"));

        pol.SetValue(Key, "Foo", "baz", RegistryValueKind.String);

        Assert.False(pol.WillDeleteValue(Key, "Foo"));
        Assert.True(pol.ContainsValue(Key, "Foo"));
        Assert.Equal("baz", pol.GetValue(Key, "Foo"));
    }

    // Burden (c): several values under one key, ClearKey, then re-add only a subset.
    // Only the re-added ones should survive; the rest should read as will-delete.
    [Fact]
    public void ClearKey_ThenRestoreSubsetOfValues_OnlyRestoredValuesSurvive()
    {
        var pol = new PolFile();
        pol.SetValue(Key, "A", 1u, RegistryValueKind.DWord);
        pol.SetValue(Key, "B", 2u, RegistryValueKind.DWord);
        pol.SetValue(Key, "C", 3u, RegistryValueKind.DWord);

        pol.ClearKey(Key);
        pol.SetValue(Key, "A", 10u, RegistryValueKind.DWord);
        pol.SetValue(Key, "B", 20u, RegistryValueKind.DWord);
        // C intentionally not restored.

        Assert.True(pol.ContainsValue(Key, "A"));
        Assert.True(pol.ContainsValue(Key, "B"));
        Assert.False(pol.ContainsValue(Key, "C"));
        Assert.True(pol.WillDeleteValue(Key, "C"));
        Assert.Equal(10u, pol.GetValue(Key, "A"));
        Assert.Equal(20u, pol.GetValue(Key, "B"));

        var names = pol.GetValueNames(Key);
        Assert.Contains("A", names);
        Assert.Contains("B", names);
        Assert.DoesNotContain("C", names);
    }

    [Fact]
    public void ForgetKeyClearance_UndoesClearKey()
    {
        var pol = new PolFile();
        pol.SetValue(Key, "Foo", 1u, RegistryValueKind.DWord);
        pol.ClearKey(Key);
        Assert.True(pol.WillDeleteValue(Key, "AnythingElse"));

        pol.ForgetKeyClearance(Key);
        Assert.False(pol.WillDeleteValue(Key, "AnythingElse"));
    }

    // Burden (d): a Load->Save round trip must be lossless. Duplicate() is Save-then-Load
    // through an in-memory buffer, exercising exactly that path via the public API.
    [Fact]
    public void Duplicate_RoundTripsAllValueKinds()
    {
        var pol = new PolFile();
        pol.SetValue(Key, "DwordVal", 123u, RegistryValueKind.DWord);
        pol.SetValue(Key, "QwordVal", 123456789012UL, RegistryValueKind.QWord);
        pol.SetValue(Key, "StringVal", "hello world", RegistryValueKind.String);
        pol.SetValue(Key, "ExpandVal", "%windir%\\foo", RegistryValueKind.ExpandString);
        pol.SetValue(Key, "MultiVal", new[] { "one", "two", "three" }, RegistryValueKind.MultiString);
        pol.DeleteValue(Key, "SomeDeletedValue");

        var dup = pol.Duplicate();

        Assert.Equal(123u, dup.GetValue(Key, "DwordVal"));
        Assert.Equal(123456789012UL, dup.GetValue(Key, "QwordVal"));
        Assert.Equal("hello world", dup.GetValue(Key, "StringVal"));
        Assert.Equal("%windir%\\foo", dup.GetValue(Key, "ExpandVal"));
        Assert.Equal(new[] { "one", "two", "three" }, (string[])dup.GetValue(Key, "MultiVal")!);
        Assert.True(dup.WillDeleteValue(Key, "SomeDeletedValue"));
    }

    // Burden (b): ApplyDifference's actual job is a MINIMAL diff against OldVersion, not a
    // full replay -- covering value changed, value removed entirely with no trace
    // ("forgotten"), value explicitly deleted, and a whole key cleared and replaced.
    // Uses a key under a real recognized policy branch (RegistryPolicyProxy.PolicyKeys)
    // since ApplyDifference's "forgotten" pass filters through IsPolicyKey.
    [Fact]
    public void ApplyDifference_EmitsMinimalDiffAgainstOldVersion()
    {
        const string k1 = @"software\policies\test1";
        const string k2 = @"software\policies\test2";

        var oldPol = new PolFile();
        oldPol.SetValue(k1, "a", 1u, RegistryValueKind.DWord); // will change
        oldPol.SetValue(k1, "b", 2u, RegistryValueKind.DWord); // will vanish entirely (forgotten)
        oldPol.SetValue(k1, "c", 3u, RegistryValueKind.DWord); // will be explicitly deleted
        oldPol.SetValue(k2, "d", 4u, RegistryValueKind.DWord); // will be cleared away with its key

        var newPol = new PolFile();
        newPol.SetValue(k1, "a", 2u, RegistryValueKind.DWord);
        // "b" not mentioned at all.
        newPol.DeleteValue(k1, "c");
        newPol.ClearKey(k2);

        var target = new RecordingPolicySource();
        newPol.ApplyDifference(oldPol, target);

        // Final state: only the updated value should remain in the target.
        Assert.Equal(2u, target.GetValue(k1, "a"));
        Assert.False(target.ContainsValue(k1, "b"));
        Assert.False(target.ContainsValue(k1, "c"));
        Assert.False(target.ContainsValue(k2, "d"));

        // "a" was updated via SetValue, never forgotten.
        Assert.Contains(target.Calls, c => c.StartsWith($"Set:{k1}\\a="));
        Assert.DoesNotContain(target.Calls, c => c == $"Forget:{k1}\\a");

        // "b" was never touched by the new version at all -- pure "forgotten" path.
        Assert.Contains(target.Calls, c => c == $"Forget:{k1}\\b");

        // "c" was explicitly deleted. The pre-redesign PolFile ALSO redundantly called
        // Forget for it in the old-entries sweep (an exact-literal-match diff that didn't
        // know Pass 1 already handled it); the redesign's diff is explicit-state-aware and
        // deliberately skips that redundant call -- see the plan file and CHANGELOG for why
        // this narrowing was a conscious choice, not an accidental behavior change. Harmless
        // either way for the only real production Target (RegistryPolicyProxy, where
        // ForgetValue and DeleteValue collapse to the same registry write).
        Assert.Contains(target.Calls, c => c == $"Delete:{k1}\\c");
        Assert.DoesNotContain(target.Calls, c => c == $"Forget:{k1}\\c");

        // K2 was cleared as a whole key; "d" is covered by that clear, so it does NOT also
        // get a separate redundant Forget call (same narrowing as "c" above).
        Assert.Contains(target.Calls, c => c == $"Clear:{k2}");
        Assert.DoesNotContain(target.Calls, c => c == $"Forget:{k2}\\d");
    }

    [Fact]
    public void ApplyDifference_WithNullOldVersion_JustAppliesEverything()
    {
        var pol = new PolFile();
        pol.SetValue(Key, "Foo", 1u, RegistryValueKind.DWord);

        var target = new RecordingPolicySource();
        pol.Apply(target);

        Assert.Equal(1u, target.GetValue(Key, "Foo"));
    }

    // Regression test for a real latent bug in the pre-redesign PolFile: the old SetValue
    // never cleared a leftover "**del.<name>" marker left behind by an earlier DeleteValue
    // (only ForgetValue/DeleteValue itself did). That was invisible for ordinary value names
    // only because "*" (0x2A) sorts after "!"/space/etc. (0x20-0x29) but before letters and
    // digits -- so for a value name starting with one of those low characters, the *literal*
    // entry sorted BEFORE the leftover marker instead of after it, and WillDeleteValue's
    // scan-and-flip loop would process the marker LAST and incorrectly report the value as
    // deleted even after it had just been re-set. The redesign's single-slot-per-value
    // structure makes this class of bug structurally impossible: there is no separate marker
    // entry to leave behind, so SetValue's unconditional overwrite is always correct.
    [Fact]
    public void SetValue_AfterDeleteValue_WithoutForget_SurvivesEvenForLowAsciiNames()
    {
        var pol = new PolFile();
        const string trickyName = "!Special"; // '!' = 0x21, sorts before '*' = 0x2A

        pol.SetValue(Key, trickyName, 1u, RegistryValueKind.DWord);
        pol.DeleteValue(Key, trickyName); // Leaves a marker in the old design
        pol.SetValue(Key, trickyName, 2u, RegistryValueKind.DWord); // No ForgetValue in between

        Assert.True(pol.ContainsValue(Key, trickyName));
        Assert.False(pol.WillDeleteValue(Key, trickyName));
        Assert.Equal(2u, pol.GetValue(Key, trickyName));
    }

    // EditPol.cs's raw POL editor dialog reads literal sentinel records via
    // GetValueNames(Key, OnlyValues: false) to render "Delete value"/"Delete all values"
    // rows, and looks up their data via GetValue/GetValueKind with the literal sentinel
    // name. The redesign keeps the business-facing API sentinel-blind but must still
    // reproduce this raw view correctly through WireRecordsFor, or EditPol.cs breaks.
    [Fact]
    public void GetValueNames_RawView_SurfacesSentinelRecordsForEditPolCompatibility()
    {
        var pol = new PolFile();
        pol.SetValue(Key, "Foo", 1u, RegistryValueKind.DWord);
        pol.DeleteValue(Key, "Bar");

        var rawNames = pol.GetValueNames(Key, false);
        Assert.Contains("Foo", rawNames);
        Assert.Contains("**del.Bar", rawNames);

        // Business-facing view must NOT show the sentinel record.
        var businessNames = pol.GetValueNames(Key, true);
        Assert.Contains("Foo", businessNames);
        Assert.DoesNotContain("**del.Bar", businessNames);

        // EditPol.cs also resolves the marker's own data via GetValue/GetValueKind with
        // the literal sentinel name.
        Assert.Equal(32u, pol.GetValue(Key, "**del.Bar"));
        Assert.Equal(RegistryValueKind.DWord, pol.GetValueKind(Key, "**del.Bar"));
    }

    [Fact]
    public void GetValueNames_RawView_SurfacesClearedKeyMarker()
    {
        var pol = new PolFile();
        pol.ClearKey(Key);

        var rawNames = pol.GetValueNames(Key, false);
        Assert.Contains("**delvals.", rawNames);
    }
}
