using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class SpolFile
{
    public List<SpolPolicyState> Policies = new();
    private int ParserLine = 0;

    public static SpolFile FromText(string Text)
    {
        var spol = new SpolFile();
        try
        {
            spol.LoadFromText(Text);
            return spol;
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message + " (Error found on line " + spol.ParserLine + ".)");
        }
    }

    // Load a SPOL script into policy states
    private void LoadFromText(string Text)
    {
        var allLines = Text.Split(new[] { "\r\n" }, StringSplitOptions.None);
        string line = "";

        string nextLine()
        {
            ParserLine += 1;
            line = allLines[ParserLine - 1]; // For human-readability in errors
            return line;
        }
        bool atEnd() => ParserLine >= allLines.Length;
        string peekLine() => allLines[ParserLine]; // +1 for next, -1 for array

        List<string> getAllStrings(string Splittable, char Delimiter)
        {
            var list = new List<string>();
            StringBuilder sb = null;
            for (int n = 0; n <= Splittable.Length - 1; n++)
            {
                if (Splittable[n] == Delimiter)
                {
                    if (sb is null)
                    {
                        sb = new StringBuilder();
                    }
                    else
                    {
                        if (n + 1 < Splittable.Length - 1 && Splittable[n + 1] == Delimiter)
                        {
                            sb.Append(Delimiter);
                            n += 1;
                        }
                        else
                        {
                            list.Add(sb.ToString());
                            sb = null;
                        }
                    }
                }
                else if (sb is not null)
                {
                    sb.Append(Splittable[n]);
                }
            }
            return list;
        }

        if (nextLine() != "Policy Plus Semantic Policy") throw new Exception("Incorrect signature.");
        while (!atEnd())
        {
            if (nextLine().Trim() == "") continue;
            var policyHeaderParts = line.Split(new[] { " " }, 2, StringSplitOptions.None); // Section and policy ID
            var singlePolicy = new SpolPolicyState { UniqueID = policyHeaderParts[1] };
            singlePolicy.Section = policyHeaderParts[0] == "U" ? AdmxPolicySection.User : AdmxPolicySection.Machine;
            const string commentPrefix = "comment: ";
            if (peekLine().TrimStart().ToLowerInvariant().StartsWith(commentPrefix))
            {
                string escapedCommentText = nextLine().TrimStart().Substring(commentPrefix.Length);
                var commentText = new StringBuilder();
                for (int n = 0; n <= escapedCommentText.Length - 1; n++)
                {
                    if (escapedCommentText[n] == '\\')
                    {
                        if (n == escapedCommentText.Length - 1) throw new Exception("Escape sequence started at end of comment.");
                        switch (escapedCommentText[n + 1])
                        {
                            case '\\':
                                commentText.Append('\\');
                                break;
                            case 'n':
                                commentText.Append("\r\n");
                                break;
                            default:
                                throw new Exception("Unknown comment escape sequence \\" + escapedCommentText[n + 1] + ".");
                        }
                        n += 1;
                    }
                    else
                    {
                        commentText.Append(escapedCommentText[n]);
                    }
                }
                singlePolicy.Comment = commentText.ToString();
            }
            switch (nextLine().Trim().ToLowerInvariant())
            {
                case "not configured":
                    singlePolicy.BasicState = PolicyState.NotConfigured;
                    break;
                case "enabled":
                    singlePolicy.BasicState = PolicyState.Enabled;
                    break;
                case "disabled":
                    singlePolicy.BasicState = PolicyState.Disabled;
                    break;
                default:
                    throw new Exception("Unknown policy state.");
            }
            if (singlePolicy.BasicState == PolicyState.Enabled)
            {
                while (!atEnd() && peekLine().Trim() != "")
                {
                    var optionParts = nextLine().Trim().Split(new[] { ": " }, 2, StringSplitOptions.None); // Name and value
                    string valueText = optionParts[1];
                    object newObj;
                    if (valueText.StartsWith("#"))
                    {
                        newObj = int.Parse(valueText.Substring(1));
                    }
                    else if (uint.TryParse(valueText, out _))
                    {
                        newObj = uint.Parse(valueText);
                    }
                    else if (bool.TryParse(valueText, out _))
                    {
                        newObj = bool.Parse(valueText);
                    }
                    else if (valueText.StartsWith("'") && valueText.EndsWith("'"))
                    {
                        newObj = valueText.Substring(1, valueText.Length - 2);
                    }
                    else if (valueText.StartsWith("\"") && valueText.EndsWith("\""))
                    {
                        newObj = getAllStrings(valueText, '"').ToArray();
                    }
                    else if (valueText == "None")
                    {
                        newObj = Array.CreateInstance(typeof(string), 0);
                    }
                    else if (valueText == "[")
                    {
                        var entries = new List<List<string>>();
                        while (peekLine().Trim() != "]")
                        {
                            entries.Add(getAllStrings(nextLine(), '"'));
                        }
                        nextLine(); // Skip the closing bracket
                        if (entries.Count == 0)
                        {
                            newObj = null; // PolicyProcessing will ignore an empty list element
                        }
                        else if (entries[0].Count == 1)
                        {
                            newObj = entries.Select(l => l[0]).ToList();
                        }
                        else
                        {
                            newObj = entries.ToDictionary(l => l[0], l => l[1]);
                        }
                    }
                    else
                    {
                        throw new Exception("Unknown option data format.");
                    }
                    singlePolicy.ExtraOptions.Add(optionParts[0], newObj);
                }
            }
            Policies.Add(singlePolicy);
        }
    }

    // Create a SPOL text fragment from the given policy state
    public static string GetFragment(SpolPolicyState State)
    {
        var sb = new StringBuilder();
        sb.Append(State.Section == AdmxPolicySection.Machine ? "C " : "U ");
        sb.AppendLine(State.UniqueID);
        if (!string.IsNullOrEmpty(State.Comment))
        {
            // Escape newlines and backslashes in the comment so it can fit on one SPOL line
            sb.AppendLine(" Comment: " + State.Comment.Replace(@"\", @"\\").Replace("\r\n", "\\n"));
        }
        switch (State.BasicState)
        {
            case PolicyState.NotConfigured:
                sb.AppendLine(" Not Configured");
                break;
            case PolicyState.Enabled:
                sb.AppendLine(" Enabled");
                break;
            case PolicyState.Disabled:
                sb.AppendLine(" Disabled");
                break;
        }
        string doubleQuoteString(string Text) => "\"" + Text.Replace("\"", "\"\"") + "\"";

        if (State.BasicState == PolicyState.Enabled && State.ExtraOptions is not null)
        {
            foreach (var kv in State.ExtraOptions)
            {
                sb.Append("  ");
                sb.Append(kv.Key);
                sb.Append(": ");
                var valueType = kv.Value.GetType();
                if (valueType == typeof(int))
                {
                    sb.Append("#");
                    sb.AppendLine(((int)kv.Value).ToString());
                }
                else if (valueType == typeof(uint))
                {
                    sb.AppendLine(((uint)kv.Value).ToString());
                }
                else if (valueType == typeof(bool))
                {
                    sb.AppendLine(((bool)kv.Value).ToString());
                }
                else if (valueType == typeof(string))
                {
                    sb.Append("'");
                    sb.Append((string)kv.Value);
                    sb.AppendLine("'");
                }
                else if (valueType == typeof(string[]))
                {
                    var stringArray = (string[])kv.Value;
                    if (stringArray.Length == 0) sb.AppendLine("None"); else sb.AppendLine(string.Join(", ", stringArray.Select(doubleQuoteString)));
                }
                else // List(Of String) or Dictionary(Of String, String)
                {
                    sb.AppendLine("[");
                    if (kv.Value is List<string> listValue)
                    {
                        foreach (var listEntry in listValue)
                        {
                            sb.Append("   ");
                            sb.AppendLine(doubleQuoteString(listEntry));
                        }
                    }
                    else
                    {
                        foreach (var listKv in (Dictionary<string, string>)kv.Value)
                        {
                            sb.Append("   ");
                            sb.Append(doubleQuoteString(listKv.Key));
                            sb.Append(": ");
                            sb.AppendLine(doubleQuoteString(listKv.Value));
                        }
                    }
                    sb.AppendLine("  ]");
                }
            }
        }
        return sb.ToString();
    }

    // Write the policy states to the policy sources
    public int ApplyAll(AdmxBundle AdmxWorkspace, IPolicySource UserSource, IPolicySource CompSource, Dictionary<string, string> UserComments, Dictionary<string, string> CompComments)
    {
        int failures = 0;
        foreach (var policy in Policies)
        {
            try
            {
                if (policy.Section == AdmxPolicySection.Machine)
                {
                    policy.Apply(CompSource, AdmxWorkspace, CompComments);
                }
                else
                {
                    policy.Apply(UserSource, AdmxWorkspace, UserComments);
                }
            }
            catch (Exception)
            {
                failures += 1;
            }
        }
        return failures;
    }
}

public class SpolPolicyState
{
    public string UniqueID;
    public AdmxPolicySection Section;
    public PolicyState BasicState;
    public string Comment;
    public Dictionary<string, object> ExtraOptions = new();

    public void Apply(IPolicySource PolicySource, AdmxBundle AdmxWorkspace, Dictionary<string, string> CommentsMap)
    {
        var pol = AdmxWorkspace.Policies[UniqueID];
        if (CommentsMap is not null && !string.IsNullOrEmpty(Comment)) CommentsMap[UniqueID] = Comment;
        PolicyProcessing.ForgetPolicy(PolicySource, pol);
        PolicyProcessing.SetPolicyState(PolicySource, pol, BasicState, ExtraOptions);
    }
}
