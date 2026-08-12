using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;

// This class implements just enough of IPolicySource to work with PolFile.ApplyDifference
// It is not a valid policy source for any policy loader
public class RegFile : IPolicySource
{
    private const string RegSignature = "Windows Registry Editor Version 5.00";
    private string Prefix; // REG files require fully-rooted key paths, while other policy sources disallow them
    private string SourceSubtree; // Accept only writes under this policy path (not needed if only going to use Apply)
    private List<RegFileKey> Keys = new();

    // Escape quotes and slashes for a value name or string data
    private static string EscapeValue(string Text)
    {
        var sb = new StringBuilder();
        for (int n = 0; n <= Text.Length - 1; n++)
        {
            char character = Text[n];
            if (character == '"' || character == '\\') sb.Append('\\');
            sb.Append(character);
        }
        return sb.ToString();
    }

    // The reverse of EscapeValue
    private static string UnescapeValue(string Text)
    {
        var sb = new StringBuilder();
        bool escaping = false;
        for (int n = 0; n <= Text.Length - 1; n++)
        {
            if (escaping)
            {
                sb.Append(Text[n]);
                escaping = false;
            }
            else if (Text[n] == '\\')
            {
                escaping = true;
            }
            else
            {
                sb.Append(Text[n]);
            }
        }
        return sb.ToString();
    }

    private static string ReadNonCommentingLine(StreamReader Reader, char? StopAt = null)
    {
        while (true)
        {
            if (Reader.EndOfStream) return null;
            if (StopAt.HasValue && Reader.Peek() == StopAt.Value) return null;
            string line = Reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line) || line.Trim().StartsWith(";")) continue;
            return line;
        }
    }

    public static RegFile Load(StreamReader Reader, string Prefix)
    {
        if (Reader.ReadLine() != RegSignature) throw new InvalidDataException("Incorrect REG signature");
        var reg = new RegFile();
        reg.SetPrefix(Prefix);
        while (true) // Read all the keys
        {
            string keyHeader = ReadNonCommentingLine(Reader);
            if (keyHeader is null) break;
            string keyName = keyHeader.Substring(1, keyHeader.Length - 2); // Remove the brackets
            if (keyName.StartsWith("-"))
            {
                // It's a deleter
                var deleterKey = new RegFileKey { Name = keyName.Substring(1), IsDeleter = true };
                reg.Keys.Add(deleterKey);
            }
            else
            {
                var key = new RegFileKey { Name = keyName };
                while (true) // Read all the values
                {
                    string valueLine = ReadNonCommentingLine(Reader, '[');
                    if (valueLine is null) break;
                    string valueName = "";
                    string data;
                    if (valueLine.StartsWith("@"))
                    {
                        data = valueLine.Substring(2);
                    }
                    else
                    {
                        var parts = valueLine.Split(new[] { "\"=" }, 2, StringSplitOptions.None);
                        valueName = UnescapeValue(parts[0].Substring(1));
                        data = parts[1];
                    }
                    var value = new RegFileValue { Name = valueName };
                    if (data == "-")
                    {
                        value.IsDeleter = true;
                    }
                    else if (data.StartsWith("\""))
                    {
                        value.Kind = RegistryValueKind.String;
                        value.Data = UnescapeValue(data.Substring(1, data.Length - 2));
                    }
                    else if (data.StartsWith("dword:"))
                    {
                        value.Kind = RegistryValueKind.DWord;
                        value.Data = uint.Parse(data.Substring(6), NumberStyles.HexNumber);
                    }
                    else if (data.StartsWith("hex"))
                    {
                        int indexOfClosingParen = data.IndexOf(')');
                        string curHexLine;
                        if (indexOfClosingParen != -1)
                        {
                            value.Kind = (RegistryValueKind)int.Parse(data.Substring(4, indexOfClosingParen - 4), NumberStyles.HexNumber);
                            curHexLine = data.Substring(indexOfClosingParen + 2);
                        }
                        else
                        {
                            value.Kind = RegistryValueKind.Binary;
                            curHexLine = data.Substring(4);
                        }
                        var allDehexedBytes = new List<byte>();
                        while (true) // Read all the hex lines
                        {
                            var hexBytes = curHexLine.Trim().TrimEnd('\\', ',').Split(',').Where(s => s != "");
                            foreach (var b in hexBytes)
                            {
                                allDehexedBytes.Add(byte.Parse(b, NumberStyles.HexNumber));
                            }
                            if (curHexLine.EndsWith("\\"))
                            {
                                curHexLine = Reader.ReadLine();
                            }
                            else
                            {
                                break;
                            }
                        }
                        value.Data = PolFile.BytesToObject(allDehexedBytes.ToArray(), value.Kind);
                    }
                    key.Values.Add(value);
                }
                reg.Keys.Add(key);
            }
        }
        return reg;
    }

    public static RegFile Load(string File, string Prefix)
    {
        using var reader = new StreamReader(File);
        return Load(reader, Prefix);
    }

    public void Save(StreamWriter Writer)
    {
        Writer.WriteLine(RegSignature);
        Writer.WriteLine();
        foreach (var key in Keys)
        {
            if (key.IsDeleter)
            {
                Writer.WriteLine("[-" + key.Name + "]");
            }
            else
            {
                Writer.WriteLine("[" + key.Name + "]");
                foreach (var value in key.Values)
                {
                    int posInRow = 0; // To split hex across lines
                    if (value.Name == "")
                    {
                        Writer.Write("@");
                        posInRow = 1;
                    }
                    else
                    {
                        string quotedName = "\"" + EscapeValue(value.Name) + "\"";
                        Writer.Write(quotedName);
                        posInRow = quotedName.Length;
                    }
                    Writer.Write("=");
                    posInRow += 1;
                    if (value.IsDeleter)
                    {
                        Writer.WriteLine("-");
                    }
                    else
                    {
                        switch (value.Kind)
                        {
                            case RegistryValueKind.String:
                                Writer.Write("\"");
                                Writer.Write(EscapeValue((string)value.Data));
                                Writer.WriteLine("\"");
                                break;
                            case RegistryValueKind.DWord:
                                Writer.Write("dword:");
                                Writer.WriteLine(Convert.ToString((long)Convert.ToUInt32(value.Data), 16).PadLeft(8, '0'));
                                break;
                            default:
                                Writer.Write("hex");
                                posInRow += 3;
                                if (value.Kind != RegistryValueKind.Binary)
                                {
                                    Writer.Write("(");
                                    Writer.Write(Convert.ToString((int)value.Kind, 16));
                                    Writer.Write(")");
                                    posInRow += 3;
                                }
                                Writer.Write(":");
                                posInRow += 1;
                                var bytes = PolFile.ObjectToBytes(value.Data, value.Kind);
                                for (int n = 0; n <= bytes.Length - 2; n++)
                                {
                                    Writer.Write(Convert.ToString((int)bytes[n], 16).PadLeft(2, '0'));
                                    Writer.Write(",");
                                    posInRow += 3;
                                    if (posInRow >= 78)
                                    {
                                        Writer.WriteLine("\\");
                                        Writer.Write("  ");
                                        posInRow = 2;
                                    }
                                }
                                if (bytes.Length > 0)
                                {
                                    Writer.WriteLine(Convert.ToString((int)bytes[bytes.Length - 1], 16).PadLeft(2, '0'));
                                }
                                else
                                {
                                    Writer.WriteLine();
                                }
                                break;
                        }
                    }
                }
            }
            Writer.WriteLine();
        }
    }

    public void Save(string File)
    {
        using var writer = new StreamWriter(File, false);
        Save(writer);
    }

    private string UnprefixKeyName(string Name)
    {
        return Name.StartsWith(Prefix, StringComparison.InvariantCultureIgnoreCase) ? Name.Substring(Prefix.Length) : Name;
    }

    private string PrefixKeyName(string Name)
    {
        return Prefix + Name;
    }

    private RegFileKey GetKey(string Name)
    {
        return Keys.FirstOrDefault(k => k.Name.Equals(Name, StringComparison.InvariantCultureIgnoreCase));
    }

    private RegFileKey GetKeyByUnprefixedName(string Name)
    {
        return GetKey(PrefixKeyName(Name));
    }

    private RegFileKey GetOrCreateKey(string Name)
    {
        var key = GetKey(Name);
        if (key is null)
        {
            key = new RegFileKey { Name = Name };
            Keys.Add(key);
        }
        return key;
    }

    private RegFileKey GetNonDeleterKey(string Name)
    {
        return Keys.FirstOrDefault(k => !k.IsDeleter && k.Name.Equals(Name, StringComparison.InvariantCultureIgnoreCase));
    }

    private bool IsSourceKeyAcceptable(string Key)
    {
        return string.IsNullOrEmpty(SourceSubtree) || Key.Equals(SourceSubtree, StringComparison.InvariantCultureIgnoreCase) ||
            Key.StartsWith(SourceSubtree + "\\", StringComparison.InvariantCultureIgnoreCase);
    }

    public bool ContainsValue(string Key, string Value) => throw new NotImplementedException();

    public object GetValue(string Key, string Value) => throw new NotImplementedException();

    public bool WillDeleteValue(string Key, string Value) => throw new NotImplementedException();

    public List<string> GetValueNames(string Key) => throw new NotImplementedException();

    public void SetValue(string Key, string Value, object Data, RegistryValueKind DataType)
    {
        if (!IsSourceKeyAcceptable(Key)) return;
        string fullKeyName = PrefixKeyName(Key);
        var keyRecord = GetNonDeleterKey(fullKeyName);
        if (keyRecord is null)
        {
            keyRecord = new RegFileKey { Name = fullKeyName };
            Keys.Add(keyRecord);
        }
        keyRecord.Values.Remove(keyRecord.GetValue(Value));
        keyRecord.Values.Add(new RegFileValue { Name = Value, Kind = DataType, Data = Data });
    }

    public void ForgetValue(string Key, string Value) => throw new NotImplementedException();

    public void DeleteValue(string Key, string Value)
    {
        if (!IsSourceKeyAcceptable(Key)) return;
        string fullKeyName = PrefixKeyName(Key);
        var keyRecord = GetOrCreateKey(fullKeyName);
        if (keyRecord.IsDeleter) return;
        keyRecord.Values.Remove(keyRecord.GetValue(Value));
        keyRecord.Values.Add(new RegFileValue { Name = Value, IsDeleter = true });
    }

    public void ClearKey(string Key)
    {
        if (!IsSourceKeyAcceptable(Key)) return;
        string fullName = PrefixKeyName(Key);
        Keys.Remove(GetKey(fullName));
        Keys.Add(new RegFileKey { Name = fullName, IsDeleter = true });
    }

    public void ForgetKeyClearance(string Key)
    {
        if (!IsSourceKeyAcceptable(Key)) return;
        var keyRecord = GetKeyByUnprefixedName(Key);
        if (keyRecord is null) return;
        if (keyRecord.IsDeleter) Keys.Remove(keyRecord);
    }

    public void Apply(IPolicySource Target)
    {
        foreach (var key in Keys)
        {
            string unprefixedKeyName = UnprefixKeyName(key.Name);
            if (key.IsDeleter)
            {
                Target.ClearKey(unprefixedKeyName);
            }
            else
            {
                foreach (var value in key.Values)
                {
                    if (value.IsDeleter)
                    {
                        Target.DeleteValue(unprefixedKeyName, value.Name);
                    }
                    else
                    {
                        Target.SetValue(unprefixedKeyName, value.Name, value.Data, value.Kind);
                    }
                }
            }
        }
    }

    public void SetPrefix(string Prefix)
    {
        if (!Prefix.EndsWith("\\")) Prefix += "\\";
        this.Prefix = Prefix;
    }

    public void SetSourceBranch(string Branch)
    {
        if (Branch.EndsWith("\\")) Branch = Branch.TrimEnd('\\');
        SourceSubtree = Branch;
    }

    // Try to determine a reasonable prefix from the data present
    public string GuessPrefix()
    {
        if (Keys.Count == 0) return "HKEY_LOCAL_MACHINE\\"; // Can't do much without any data
        string firstKeyName = Keys[0].Name;
        if (firstKeyName.StartsWith("HKEY_USERS\\"))
        {
            // The user SID should be part of the prefix
            int secondSlashPos = firstKeyName.IndexOf("\\", 11);
            return firstKeyName.Substring(0, secondSlashPos + 1);
        }
        else
        {
            // Other hives should be just fine
            int firstSlashPos = firstKeyName.IndexOf("\\");
            return firstKeyName.Substring(0, firstSlashPos + 1);
        }
    }

    public bool HasDefaultValues()
    {
        return Keys.Any(k => k.Values.Any(v => string.IsNullOrEmpty(v.Name)));
    }

    private class RegFileKey
    {
        public string Name;
        public bool IsDeleter;
        public List<RegFileValue> Values = new();

        public RegFileValue GetValue(string Value)
        {
            return Values.FirstOrDefault(v => v.Name.Equals(Value, StringComparison.InvariantCultureIgnoreCase));
        }
    }

    private class RegFileValue
    {
        public string Name;
        public object Data;
        public RegistryValueKind Kind;
        public bool IsDeleter;
    }
}
