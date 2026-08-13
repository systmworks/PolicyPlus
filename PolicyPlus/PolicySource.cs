using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;

public interface IPolicySource
{
    bool ContainsValue(string Key, string Value);
    object GetValue(string Key, string Value);
    bool WillDeleteValue(string Key, string Value);
    List<string> GetValueNames(string Key);
    void SetValue(string Key, string Value, object Data, RegistryValueKind DataType);
    void ForgetValue(string Key, string Value); // Stop keeping track of a value
    void DeleteValue(string Key, string Value); // Mark a value as queued for deletion
    void ClearKey(string Key); // Destroy all values in a key
    void ForgetKeyClearance(string Key); // Unmark a key as cleared
}

// Represents a parsed .pol file. Internally this is an explicit-state key tree: each
// registry key path is a node carrying its own values (each with a Deleted flag) and a
// Cleared flag for "delete everything else under this key". The .pol wire format has no
// native delete operation -- Microsoft's real convention represents deletions as specially
// named sentinel records (**del.<name>, **delvals., **deletevalues, **deletekeys) mixed in
// with real value records. That wire convention is confined to Load/Save (and the shared
// WireRecordsFor helper, also used to keep EditPol.cs's raw record view working) -- nothing
// else in this class needs to know it exists. See CHANGELOG.md for the prior sort-order-
// dependent design this replaced, and the latent bug that motivated the change.
public class PolFile : IPolicySource
{
    private sealed class ValueEntry
    {
        public string Name; // Original-case value name
        public bool Deleted; // Pending "delete this value" (was **del.<name> / a **deletevalues member)
        public PolEntryData Data; // Real payload; unused when Deleted
    }

    private sealed class KeyNode
    {
        public KeyNode Parent;
        public string OwnName;
        public bool Cleared; // "**delvals." for this key (also where **deletekeys entries normalize to)
        public readonly Dictionary<string, ValueEntry> Values = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, KeyNode> Children = new(StringComparer.OrdinalIgnoreCase);
    }

    private readonly KeyNode _root = new() { OwnName = "" };

    private static string[] SplitPath(string Key) =>
        string.IsNullOrEmpty(Key) ? Array.Empty<string>() : Key.Split('\\');

    private KeyNode FindNode(string Key)
    {
        var node = _root;
        foreach (var seg in SplitPath(Key))
            if (!node.Children.TryGetValue(seg, out node)) return null;
        return node;
    }

    private KeyNode GetOrCreateNode(string Key)
    {
        var node = _root;
        foreach (var seg in SplitPath(Key))
        {
            if (node.Children.TryGetValue(seg, out var child))
                child.OwnName = seg; // Most-recent-casing-wins
            else
            {
                child = new KeyNode { Parent = node, OwnName = seg };
                node.Children[seg] = child;
            }
            node = child;
        }
        return node;
    }

    // A node with no content of its own is pruned so ForgetValue/ForgetKeyClearance don't
    // leave behind empty placeholder nodes that would show up in GetKeyNames.
    private static void PruneIfEmpty(KeyNode node)
    {
        while (node.Parent is not null && node.Values.Count == 0 && !node.Cleared && node.Children.Count == 0)
        {
            var parent = node.Parent;
            parent.Children.Remove(node.OwnName);
            node = parent;
        }
    }

    private static void WalkTree(KeyNode node, string path, Action<string, KeyNode> visit)
    {
        visit(path, node);
        foreach (var child in node.Children.Values)
            WalkTree(child, string.IsNullOrEmpty(path) ? child.OwnName : path + @"\" + child.OwnName, visit);
    }

    public static PolFile Load(string File)
    {
        using var fPol = new FileStream(File, FileMode.Open, FileAccess.Read);
        using var binary = new BinaryReader(fPol);
        return Load(binary);
    }

    public static PolFile Load(BinaryReader Stream)
    {
        var pol = new PolFile();
        if (Stream.ReadUInt32() != 0x67655250) throw new InvalidDataException("Missing PReg signature");
        if (Stream.ReadUInt32() != 1) throw new InvalidDataException("Unknown (newer) version of POL format");

        // Read a null-terminated UTF-16LE string
        string readSz()
        {
            var sb = new StringBuilder();
            while (true)
            {
                int charCode = Stream.ReadUInt16();
                if (charCode == 0) break;
                sb.Append((char)charCode);
            }
            return sb.ToString();
        }

        while (Stream.BaseStream.Position != Stream.BaseStream.Length)
        {
            Stream.BaseStream.Position += 2; // Skip the "[" character
            string key = readSz();
            Stream.BaseStream.Position += 2; // Skip ";"
            string value = readSz();
            if (Stream.ReadUInt16() != (ushort)';') Stream.BaseStream.Position += 2; // MS documentation indicates there might be an extra null before the ";" after the value name
            var kind = (RegistryValueKind)Stream.ReadInt32();
            Stream.BaseStream.Position += 2; // ";"
            uint length = Stream.ReadUInt32();
            Stream.BaseStream.Position += 2; // ";"
            var data = new byte[length];
            Stream.Read(data, 0, (int)length);
            Stream.BaseStream.Position += 2; // "]"
            pol.IngestRawRecord(key, value, new PolEntryData { Kind = kind, Data = data });
        }
        return pol;
    }

    // The only place besides Save/WireRecordsFor that knows the sentinel conventions.
    // A literal value record always wins over a marker for the same name, regardless of
    // which physically appears first in the file -- this matches (and makes explicit) what
    // the old sort-order-dependent design always actually resolved to, since "**"-prefixed
    // markers were guaranteed to sort, and therefore be superseded, before a same-named
    // literal entry.
    private void IngestRawRecord(string keyPath, string valueName, PolEntryData ped)
    {
        var node = GetOrCreateNode(keyPath);
        if (valueName.StartsWith("**del.", StringComparison.OrdinalIgnoreCase))
        {
            string target = valueName.Substring(6);
            if (!(node.Values.TryGetValue(target, out var existing) && !existing.Deleted))
                node.Values[target] = new ValueEntry { Name = target, Deleted = true };
        }
        else if (valueName.StartsWith("**delvals", StringComparison.OrdinalIgnoreCase))
        {
            node.Cleared = true;
        }
        else if (valueName.Equals("**deletevalues", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var name in ped.AsString().Split(';'))
                if (name.Length > 0 && !(node.Values.TryGetValue(name, out var existing) && !existing.Deleted))
                    node.Values[name] = new ValueEntry { Name = name, Deleted = true };
        }
        else if (valueName.StartsWith("**deletekeys", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var sub in ped.AsString().Split(';'))
                if (sub.Length > 0)
                    GetOrCreateNode(string.IsNullOrEmpty(keyPath) ? sub : keyPath + @"\" + sub).Cleared = true;
        }
        else
        {
            node.Values[valueName] = new ValueEntry { Name = valueName, Deleted = false, Data = ped };
        }
    }

    public void Save(string File)
    {
        using var fPol = new FileStream(File, FileMode.Create);
        using var binary = new BinaryWriter(fPol, Encoding.Unicode);
        Save(binary);
    }

    public void Save(BinaryWriter Writer)
    {
        void writeSz(string Text)
        {
            foreach (char c in Text)
            {
                Writer.Write(c);
            }
            Writer.Write((short)0);
        }
        void writeRecord(string key, string val, RegistryValueKind kind, byte[] data)
        {
            Writer.Write('[');
            writeSz(key);
            Writer.Write(';');
            writeSz(val);
            Writer.Write(';');
            Writer.Write((int)kind);
            Writer.Write(';');
            Writer.Write(data.Length);
            Writer.Write(';');
            Writer.Write(data);
            Writer.Write(']');
        }

        Writer.Write(0x67655250U);
        Writer.Write(1);

        void writeKey(KeyNode node, string path)
        {
            foreach (var (name, data) in WireRecordsFor(node))
                writeRecord(path, name, data.Kind, data.Data);
            foreach (var child in node.Children.Values.OrderBy(c => c.OwnName, StringComparer.OrdinalIgnoreCase))
                writeKey(child, string.IsNullOrEmpty(path) ? child.OwnName : path + @"\" + child.OwnName);
        }
        writeKey(_root, "");
    }

    // Single source of truth for "what would Save() write for this key's direct values" --
    // used by Save itself and by GetValueNames(Key, OnlyValues: false)/GetValue/GetValueKind
    // when addressed with a literal sentinel name, which is what EditPol.cs's raw POL editor
    // does to render "Delete value"/"Delete all values" rows. Deletions are always expanded
    // to individual **del.<name> records rather than a compact **deletevalues list -- exact
    // byte fidelity to whatever produced the original file was never a real constraint here
    // (**deletekeys was already never written by this codebase either), only well-formed
    // output real Group Policy tooling can read.
    private static IEnumerable<(string Name, PolEntryData Data)> WireRecordsFor(KeyNode node)
    {
        if (node.Cleared)
            yield return ("**delvals.", PolEntryData.FromString(" "));
        foreach (var v in node.Values.Values.OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase))
            yield return v.Deleted
                ? ("**del." + v.Name, PolEntryData.FromDword(32)) // It's what Microsoft does
                : (v.Name, v.Data);
    }

    private static bool TryResolveRawEntry(KeyNode node, string value, out PolEntryData data)
    {
        foreach (var r in WireRecordsFor(node))
        {
            if (r.Name.Equals(value, StringComparison.OrdinalIgnoreCase))
            {
                data = r.Data;
                return true;
            }
        }
        data = null;
        return false;
    }

    public void DeleteValue(string Key, string Value)
    {
        var node = GetOrCreateNode(Key);
        bool alreadyCovered = node.Cleared; // Whole-key clear already implies this deletion
        node.Values.Remove(Value);
        if (!alreadyCovered)
            node.Values[Value] = new ValueEntry { Name = Value, Deleted = true };
    }

    public void ForgetValue(string Key, string Value)
    {
        var node = FindNode(Key);
        if (node is null) return;
        if (node.Values.Remove(Value)) PruneIfEmpty(node);
    }

    public void SetValue(string Key, string Value, object Data, RegistryValueKind DataType)
    {
        var node = GetOrCreateNode(Key);
        node.Values[Value] = new ValueEntry { Name = Value, Deleted = false, Data = PolEntryData.FromArbitrary(Data, DataType) };
    }

    public bool ContainsValue(string Key, string Value)
    {
        var node = FindNode(Key);
        return node is not null && node.Values.TryGetValue(Value, out var e) && !e.Deleted;
    }

    public object GetValue(string Key, string Value)
    {
        var node = FindNode(Key);
        if (node is null) return null;
        if (node.Values.TryGetValue(Value, out var e) && !e.Deleted) return e.Data.AsArbitrary();
        return TryResolveRawEntry(node, Value, out var raw) ? raw.AsArbitrary() : null;
    }

    public bool WillDeleteValue(string Key, string Value)
    {
        var node = FindNode(Key);
        if (node is null) return false;
        if (node.Values.TryGetValue(Value, out var e)) return e.Deleted; // Explicit knowledge always wins
        return node.Cleared; // Else defer to the key-level clearance
    }

    public List<string> GetValueNames(string Key) => GetValueNames(Key, true);

    public List<string> GetValueNames(string Key, bool OnlyValues)
    {
        var node = FindNode(Key);
        if (node is null) return new List<string>();
        if (OnlyValues)
            return node.Values.Values.Where(v => !v.Deleted && !v.Name.StartsWith("**", StringComparison.Ordinal))
                .Select(v => v.Name).ToList();
        return WireRecordsFor(node).Select(r => r.Name).ToList();
    }

    // Figure out which values have changed and commit only the changes
    public void ApplyDifference(PolFile OldVersion, IPolicySource Target)
    {
        OldVersion ??= new PolFile();

        // Pass 1: replay the new state. A key's Cleared marker is always emitted before its
        // own values in the same visit, by construction -- no cross-key ordering guarantee
        // is needed, since different keys' target writes never interact.
        WalkTree(_root, "", (path, node) =>
        {
            if (node.Cleared) Target.ClearKey(path);
            foreach (var v in node.Values.Values)
            {
                if (v.Name == "") continue; // Empty-name placeholder (EditPol.cs "Add Key"), never a real registry write
                if (v.Deleted) Target.DeleteValue(path, v.Name);
                else Target.SetValue(path, v.Name, v.Data.AsArbitrary(), v.Data.Kind);
            }
        });

        // Pass 2: forget old values the new state has zero explicit knowledge of at all --
        // no real value, no explicit per-value delete, and not covered by a whole-key clear.
        // Deliberately narrower than a literal-match diff: a value Pass 1 already
        // DeleteValue'd, or that's covered by a ClearKey Pass 1 already emitted, does NOT
        // also get a redundant ForgetValue here. Harmless for the only real production
        // Target (RegistryPolicyProxy, where ForgetValue and DeleteValue collapse to the
        // same registry write) but a real behavior narrowing versus the prior design, which
        // is why it's called out in the CHANGELOG rather than left as a silent difference.
        WalkTree(OldVersion._root, "", (path, node) =>
        {
            if (!RegistryPolicyProxy.IsPolicyKey(path)) return;
            var newNode = FindNode(path);
            foreach (var v in node.Values.Values)
            {
                if (v.Deleted || v.Name == "") continue;
                bool knownInNew = newNode is not null && (newNode.Values.ContainsKey(v.Name) || newNode.Cleared);
                if (!knownInNew) Target.ForgetValue(path, v.Name);
            }
        });
    }

    // Apply all the values to the policy source
    public void Apply(IPolicySource Target) => ApplyDifference(null, Target);

    public void ClearKey(string Key)
    {
        var node = GetOrCreateNode(Key);
        node.Values.Clear(); // Forget every value AND every pending per-value delete at this key
        node.Cleared = true;
    }

    public void ForgetKeyClearance(string Key)
    {
        var node = FindNode(Key);
        if (node is null) return;
        node.Cleared = false;
        PruneIfEmpty(node);
    }

    public List<string> GetKeyNames(string Key)
    {
        var node = FindNode(Key);
        return node is null ? new List<string>() : node.Children.Values.Select(c => c.OwnName).ToList();
    }

    public RegistryValueKind GetValueKind(string Key, string Value)
    {
        var node = FindNode(Key) ?? throw new KeyNotFoundException($@"No key ""{Key}"".");
        if (node.Values.TryGetValue(Value, out var e) && !e.Deleted) return e.Data.Kind;
        if (TryResolveRawEntry(node, Value, out var raw)) return raw.Kind;
        throw new KeyNotFoundException($@"No value or marker ""{Value}"" under ""{Key}"".");
    }

    public PolFile Duplicate()
    {
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, Encoding.Unicode, true))
        {
            Save(writer);
        }
        ms.Position = 0;
        using var reader = new BinaryReader(ms, Encoding.Unicode);
        return Load(reader);
    }

    public static byte[] ObjectToBytes(object Data, RegistryValueKind Kind) => PolEntryData.FromArbitrary(Data, Kind).Data;

    public static object BytesToObject(byte[] Data, RegistryValueKind Kind) => new PolEntryData { Data = Data, Kind = Kind }.AsArbitrary();

    private class PolEntryData // Represents one record in a POL file
    {
        public RegistryValueKind Kind;
        public byte[] Data;

        public string AsString() // Get a UTF-16LE string
        {
            var sb = new StringBuilder();
            for (int x = 0; x <= (Data.Length / 2) - 1; x++)
            {
                int charCode = Data[x * 2] + (Data[(x * 2) + 1] << 8);
                if (charCode == 0) break;
                sb.Append((char)charCode);
            }
            return sb.ToString();
        }

        public static PolEntryData FromString(string Text, bool Expand = false) // Save a UTF-16LE string
        {
            var ped = new PolEntryData { Kind = Expand ? RegistryValueKind.ExpandString : RegistryValueKind.String };
            var data = new byte[(Text.Length * 2) + 2];
            for (int x = 0; x <= Text.Length - 1; x++)
            {
                int charCode = Text[x];
                data[x * 2] = (byte)(charCode & 0xFF);
                data[(x * 2) + 1] = (byte)(charCode >> 8);
            }
            ped.Data = data;
            return ped;
        }

        public uint AsDword()
        {
            return (uint)Data[0] + ((uint)Data[1] << 8) + ((uint)Data[2] << 16) + ((uint)Data[3] << 24);
        }

        public static PolEntryData FromDword(uint Dword)
        {
            var ped = new PolEntryData { Kind = RegistryValueKind.DWord };
            var data = new byte[4];
            data[0] = (byte)(Dword & 0xFFU);
            data[1] = (byte)((Dword >> 8) & 0xFFU);
            data[2] = (byte)((Dword >> 16) & 0xFFU);
            data[3] = (byte)(Dword >> 24);
            ped.Data = data;
            return ped;
        }

        public ulong AsQword()
        {
            ulong value = 0;
            for (int n = 0; n <= 7; n++)
            {
                value += (ulong)Data[n] << (n * 8);
            }
            return value;
        }

        public static PolEntryData FromQword(ulong Qword)
        {
            var ped = new PolEntryData { Kind = RegistryValueKind.QWord };
            var data = new byte[8];
            for (int n = 0; n <= 7; n++)
            {
                data[n] = (byte)((Qword >> (n * 8)) & 0xFFUL);
            }
            ped.Data = data;
            return ped;
        }

        public string[] AsMultiString()
        {
            var strings = new List<string>();
            var sb = new StringBuilder();
            for (int n = 0; n <= (Data.Length / 2) - 1; n++)
            {
                int charCode = Data[n * 2] + (Data[(n * 2) + 1] << 8);
                if (charCode == 0)
                {
                    if (sb.Length == 0) break;
                    strings.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append((char)charCode);
                }
            }
            return strings.ToArray();
        }

        public static PolEntryData FromMultiString(string[] Strings)
        {
            var ped = new PolEntryData { Kind = RegistryValueKind.MultiString };
            var data = new byte[(Strings.Sum(s => s.Length + 1) + 1) * 2];
            int n = 0;
            foreach (var s in Strings)
            {
                foreach (char c in s)
                {
                    int charCode = c;
                    data[n] = (byte)(charCode & 0xFF);
                    data[n + 1] = (byte)(charCode >> 8);
                    n += 2;
                }
                n += 2; // Leave two null bytes after each string
            }
            ped.Data = data;
            return ped;
        }

        public byte[] AsBinary() => (byte[])Data.Clone();

        public static PolEntryData FromBinary(byte[] Binary, RegistryValueKind Kind = RegistryValueKind.Binary)
        {
            var ped = new PolEntryData { Kind = Kind };
            ped.Data = (byte[])Binary.Clone();
            return ped;
        }

        // Get the data in the best .NET type for it
        public object AsArbitrary()
        {
            switch (Kind)
            {
                case RegistryValueKind.String:
                    return AsString();
                case RegistryValueKind.DWord:
                    return AsDword();
                case RegistryValueKind.ExpandString:
                    return AsString();
                case RegistryValueKind.QWord:
                    return AsQword();
                case RegistryValueKind.MultiString:
                    return AsMultiString();
                default:
                    return AsBinary();
            }
        }

        // Take an arbitrary .NET object and turn it into bytes
        public static PolEntryData FromArbitrary(object Data, RegistryValueKind Kind)
        {
            switch (Kind)
            {
                case RegistryValueKind.String:
                    return FromString((string)Data);
                case RegistryValueKind.DWord:
                    return FromDword(Convert.ToUInt32(Data));
                case RegistryValueKind.ExpandString:
                    return FromString((string)Data, true);
                case RegistryValueKind.QWord:
                    return FromQword(Convert.ToUInt64(Data));
                case RegistryValueKind.MultiString:
                    return FromMultiString((string[])Data);
                default:
                    return FromBinary((byte[])Data, Kind);
            }
        }
    }
}

public class RegistryPolicyProxy : IPolicySource // Pass operations through to the real Registry
{
    private RegistryKey RootKey;

    public static RegistryPolicyProxy EncapsulateKey(RegistryKey Key) => new() { RootKey = Key };

    public static RegistryPolicyProxy EncapsulateKey(RegistryHive Key) => EncapsulateKey(RegistryKey.OpenBaseKey(Key, RegistryView.Default));

    public void DeleteValue(string Key, string Value)
    {
        using var regKey = RootKey.OpenSubKey(Key, true);
        if (regKey is null) return;
        regKey.DeleteValue(Value, false);
    }

    public void ForgetValue(string Key, string Value) => DeleteValue(Key, Value); // The Registry has no concept of "will delete this when I see it"

    public void SetValue(string Key, string Value, object Data, RegistryValueKind DataType)
    {
        if (Data is uint u)
        {
            Data = new ReinterpretableDword { Unsigned = u }.Signed;
        }
        else if (Data is ulong ul)
        {
            Data = new ReinterpretableQword { Unsigned = ul }.Signed;
        }
        using var regKey = RootKey.CreateSubKey(Key);
        regKey.SetValue(Value, Data, DataType);
    }

    public bool ContainsValue(string Key, string Value)
    {
        using var regKey = RootKey.OpenSubKey(Key);
        if (regKey is null) return false;
        if (string.IsNullOrEmpty(Value)) return true;
        return regKey.GetValueNames().Any(s => s.Equals(Value, StringComparison.InvariantCultureIgnoreCase));
    }

    public object GetValue(string Key, string Value)
    {
        using var regKey = RootKey.OpenSubKey(Key, false);
        if (regKey is null) return null;
        object data = regKey.GetValue(Value, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (data is int i)
        {
            return new ReinterpretableDword { Signed = i }.Unsigned;
        }
        else if (data is long l)
        {
            return new ReinterpretableQword { Signed = l }.Unsigned;
        }
        else
        {
            return data;
        }
    }

    public List<string> GetValueNames(string Key)
    {
        using var regKey = RootKey.OpenSubKey(Key);
        return regKey is null ? new List<string>() : regKey.GetValueNames().ToList();
    }

    public bool WillDeleteValue(string Key, string Value) => false;

    public static bool IsPolicyKey(string KeyPath)
    {
        return PolicyKeys.Any(pk => KeyPath.StartsWith(pk + @"\", StringComparison.InvariantCultureIgnoreCase) ||
                                     KeyPath.Equals(pk, StringComparison.InvariantCultureIgnoreCase));
    }

    public void ClearKey(string Key)
    {
        foreach (var value in GetValueNames(Key))
        {
            ForgetValue(Key, value);
        }
    }

    public void ForgetKeyClearance(string Key)
    {
        // Does nothing
    }

    public RegistryKey EncapsulatedRegistry => RootKey;

    // Values outside these branches are not tracked by PolFile.ApplyDifference
    public static IEnumerable<string> PolicyKeys => new[] { @"software\policies", @"software\microsoft\windows\currentversion\policies", @"system\currentcontrolset\policies" };
}
