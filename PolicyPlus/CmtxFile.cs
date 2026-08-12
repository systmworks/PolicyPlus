using System;
using System.Collections.Generic;
using System.Xml;

public class CmtxFile
{
    public Dictionary<string, string> Prefixes = new();
    public Dictionary<string, string> Comments = new();
    public Dictionary<string, string> Strings = new();
    public string SourceFile;

    // CMTX documentation: https://msdn.microsoft.com/en-us/library/dn605929(v=vs.85).aspx
    public static CmtxFile Load(string File)
    {
        var cmtx = new CmtxFile { SourceFile = File };
        var xmlDoc = new XmlDocument();
        xmlDoc.Load(File);
        XmlNode policyComments = xmlDoc.GetElementsByTagName("policyComments")[0];
        foreach (XmlNode child in policyComments.ChildNodes)
        {
            switch (child.LocalName)
            {
                case "policyNamespaces": // ADMX file prefixes
                    foreach (XmlNode usingElement in child.ChildNodes)
                    {
                        if (usingElement.LocalName != "using") continue;
                        string prefix = usingElement.AttributeOrNull("prefix");
                        string ns = usingElement.AttributeOrNull("namespace");
                        cmtx.Prefixes.Add(prefix, ns);
                    }
                    break;
                case "comments": // Policy to comment ID mapping
                    foreach (XmlNode admTemplateElement in child.ChildNodes)
                    {
                        if (admTemplateElement.LocalName != "admTemplate") continue;
                        foreach (XmlNode commentElement in admTemplateElement.ChildNodes)
                        {
                            if (commentElement.LocalName != "comment") continue;
                            string policy = commentElement.AttributeOrNull("policyRef");
                            string text = commentElement.AttributeOrNull("commentText");
                            cmtx.Comments.Add(policy, text);
                        }
                    }
                    break;
                case "resources": // The actual comment text
                    foreach (XmlNode stringTable in child.ChildNodes)
                    {
                        if (stringTable.LocalName != "stringTable") continue;
                        foreach (XmlNode stringElement in stringTable.ChildNodes)
                        {
                            if (stringElement.LocalName != "string") continue;
                            string id = stringElement.AttributeOrNull("id");
                            string text = stringElement.InnerText;
                            cmtx.Strings.Add(id, text);
                        }
                    }
                    break;
            }
        }
        return cmtx;
    }

    // Create CMTX structures from a simple policy-to-comment-text mapping
    public static CmtxFile FromCommentTable(Dictionary<string, string> Table)
    {
        var cmtx = new CmtxFile();
        int resNum = 0; // A counter to make sure names are unique
        var revPrefixes = new Dictionary<string, string>(); // Opposite-direction prefix lookup
        foreach (var kv in Table)
        {
            var idParts = kv.Key.Split(new[] { ":" }, 2, StringSplitOptions.None);
            if (!revPrefixes.ContainsKey(idParts[0]))
            {
                string prefixId = idParts[0].Replace('.', '_') + "__" + resNum;
                revPrefixes.Add(idParts[0], prefixId);
                cmtx.Prefixes.Add(prefixId, idParts[0]);
            }
            string resourceId = idParts[0].Replace('.', '_') + "__" + idParts[1] + "__" + resNum;
            cmtx.Strings.Add(resourceId, kv.Value);
            cmtx.Comments.Add(revPrefixes[idParts[0]] + ":" + idParts[1], "$(resource." + resourceId + ")");
            resNum += 1;
        }
        return cmtx;
    }

    // Create a convenient policy-to-comment-text mapping
    public Dictionary<string, string> ToCommentTable()
    {
        var commentTable = new Dictionary<string, string>();
        foreach (var comment in Comments)
        {
            var refParts = comment.Key.Split(new[] { ":" }, 2, StringSplitOptions.None);
            string polNamespace = Prefixes[refParts[0]];
            string stringRef = comment.Value;
            string stringId = stringRef.Substring(11, stringRef.Length - 12); // "$(resource." is 11 characters long
            commentTable.Add(polNamespace + ":" + refParts[1], Strings[stringId]);
        }
        return commentTable;
    }

    public void Save() => Save(SourceFile);

    // Save the CMTX data to a fully compliant XML document
    public void Save(string File)
    {
        var xml = new XmlDocument();
        var declaration = xml.CreateXmlDeclaration("1.0", "utf-8", "");
        xml.AppendChild(declaration);
        var policyComments = xml.CreateElement("policyComments");
        policyComments.SetAttribute("xmlns:xsd", "http://www.w3.org/2001/XMLSchema");
        policyComments.SetAttribute("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
        policyComments.SetAttribute("revision", "1.0");
        policyComments.SetAttribute("schemaVersion", "1.0");
        policyComments.SetAttribute("xmlns", "http://www.microsoft.com/GroupPolicy/CommentDefinitions");
        var policyNamespaces = xml.CreateElement("policyNamespaces");
        foreach (var prefix in Prefixes)
        {
            XmlElement usingElem = xml.CreateElement("using");
            usingElem.SetAttribute("prefix", prefix.Key);
            usingElem.SetAttribute("namespace", prefix.Value);
            policyNamespaces.AppendChild(usingElem);
        }
        policyComments.AppendChild(policyNamespaces);
        var commentsElem = xml.CreateElement("comments");
        var admTemplate = xml.CreateElement("admTemplate");
        foreach (var comment in Comments)
        {
            var commentElem = xml.CreateElement("comment");
            commentElem.SetAttribute("policyRef", comment.Key);
            commentElem.SetAttribute("commentText", comment.Value);
            admTemplate.AppendChild(commentElem);
        }
        commentsElem.AppendChild(admTemplate);
        policyComments.AppendChild(commentsElem);
        var resources = xml.CreateElement("resources");
        resources.SetAttribute("minRequiredRevision", "1.0");
        var stringTable = xml.CreateElement("stringTable");
        foreach (var textval in Strings)
        {
            var stringElem = xml.CreateElement("string");
            stringElem.SetAttribute("id", textval.Key);
            stringElem.InnerText = textval.Value;
            stringTable.AppendChild(stringElem);
        }
        resources.AppendChild(stringTable);
        policyComments.AppendChild(resources);
        xml.AppendChild(policyComments);
        xml.Save(File);
    }
}
