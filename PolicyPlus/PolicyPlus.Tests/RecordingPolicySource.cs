using Microsoft.Win32;

namespace PolicyPlus.Tests;

// A minimal IPolicySource fake that records every call made to it and tracks
// resulting (Key, Value) -> Data state, so ApplyDifference tests can assert both
// "what got called, in what order" and "what the target ends up containing."
public class RecordingPolicySource : IPolicySource
{
    public readonly List<string> Calls = new();
    public readonly Dictionary<(string Key, string Value), object?> Values = new();

    public bool ContainsValue(string Key, string Value) => Values.ContainsKey((Key, Value));

    public object? GetValue(string Key, string Value) =>
        Values.TryGetValue((Key, Value), out var v) ? v : null;

    public bool WillDeleteValue(string Key, string Value) => false;

    public List<string> GetValueNames(string Key) =>
        Values.Keys.Where(k => k.Key == Key).Select(k => k.Value).ToList();

    public void SetValue(string Key, string Value, object Data, RegistryValueKind DataType)
    {
        Calls.Add($"Set:{Key}\\{Value}={Data}");
        Values[(Key, Value)] = Data;
    }

    public void ForgetValue(string Key, string Value)
    {
        Calls.Add($"Forget:{Key}\\{Value}");
        Values.Remove((Key, Value));
    }

    public void DeleteValue(string Key, string Value)
    {
        Calls.Add($"Delete:{Key}\\{Value}");
        Values.Remove((Key, Value));
    }

    public void ClearKey(string Key)
    {
        Calls.Add($"Clear:{Key}");
        foreach (var name in GetValueNames(Key))
            Values.Remove((Key, name));
    }

    public void ForgetKeyClearance(string Key)
    {
        Calls.Add($"ForgetClearance:{Key}");
    }
}
