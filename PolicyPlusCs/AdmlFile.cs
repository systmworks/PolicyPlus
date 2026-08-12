using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

public class AdmlFile
{
    public string SourceFile;
    public decimal Revision;
    public string DisplayName;
    public string Description;
    public Dictionary<string, string> StringTable = new();
    public Dictionary<string, Presentation> PresentationTable = new();

    private AdmlFile() { }

    // ADML documentation: https://technet.microsoft.com/en-us/library/cc772050(v=ws.10).aspx
    public static AdmlFile Load(string File)
    {
        var adml = new AdmlFile { SourceFile = File };
        var xmlDoc = new XmlDocument();
        xmlDoc.Load(File);

        // Load ADML metadata
        XmlNode policyDefinitionResources = xmlDoc.GetElementsByTagName("policyDefinitionResources")[0];
        adml.Revision = decimal.Parse(policyDefinitionResources.Attributes["revision"].Value, CultureInfo.InvariantCulture);
        foreach (XmlNode child in policyDefinitionResources.ChildNodes)
        {
            switch (child.LocalName)
            {
                case "displayName":
                    adml.DisplayName = child.InnerText;
                    break;
                case "description":
                    adml.Description = child.InnerText;
                    break;
            }
        }

        // Load localized strings
        var stringTableList = xmlDoc.GetElementsByTagName("stringTable");
        if (stringTableList.Count > 0)
        {
            XmlNode stringTable = stringTableList[0];
            foreach (XmlNode stringElement in stringTable.ChildNodes)
            {
                if (stringElement.LocalName != "string") continue;
                string key = stringElement.Attributes["id"].Value;
                string value = stringElement.InnerText;
                adml.StringTable.Add(key, value);
            }
        }

        // Load presentations (UI arrangements)
        var presTableList = xmlDoc.GetElementsByTagName("presentationTable");
        if (presTableList.Count > 0)
        {
            XmlNode presTable = presTableList[0];
            foreach (XmlNode presElement in presTable.ChildNodes)
            {
                if (presElement.LocalName != "presentation") continue;
                var presentation = new Presentation
                {
                    Name = presElement.Attributes["id"].Value
                };
                foreach (XmlNode uiElement in presElement.ChildNodes)
                {
                    PresentationElement presPart = null;
                    switch (uiElement.LocalName)
                    {
                        case "text":
                            presPart = new LabelPresentationElement { Text = uiElement.InnerText };
                            break;
                        case "decimalTextBox":
                            presPart = new NumericBoxPresentationElement
                            {
                                DefaultValue = Convert.ToUInt32(uiElement.AttributeOrDefault("defaultValue", 1)),
                                HasSpinner = Convert.ToBoolean(uiElement.AttributeOrDefault("spin", true)),
                                SpinnerIncrement = Convert.ToUInt32(uiElement.AttributeOrDefault("spinStep", 1)),
                                Label = uiElement.InnerText
                            };
                            break;
                        case "textBox":
                            {
                                var textPart = new TextBoxPresentationElement();
                                foreach (XmlNode textboxInfo in uiElement.ChildNodes)
                                {
                                    switch (textboxInfo.LocalName)
                                    {
                                        case "label":
                                            textPart.Label = textboxInfo.InnerText;
                                            break;
                                        case "defaultValue":
                                            textPart.DefaultValue = textboxInfo.InnerText;
                                            break;
                                    }
                                }
                                presPart = textPart;
                            }
                            break;
                        case "checkBox":
                            presPart = new CheckBoxPresentationElement
                            {
                                DefaultState = Convert.ToBoolean(uiElement.AttributeOrDefault("defaultChecked", false)),
                                Text = uiElement.InnerText
                            };
                            break;
                        case "comboBox":
                            {
                                var comboPart = new ComboBoxPresentationElement
                                {
                                    NoSort = Convert.ToBoolean(uiElement.AttributeOrDefault("noSort", false))
                                };
                                foreach (XmlNode comboInfo in uiElement.ChildNodes)
                                {
                                    switch (comboInfo.LocalName)
                                    {
                                        case "label":
                                            comboPart.Label = comboInfo.InnerText;
                                            break;
                                        case "default":
                                            comboPart.DefaultText = comboInfo.InnerText;
                                            break;
                                        case "suggestion":
                                            comboPart.Suggestions.Add(comboInfo.InnerText);
                                            break;
                                    }
                                }
                                presPart = comboPart;
                            }
                            break;
                        case "dropdownList":
                            {
                                string defaultItem = uiElement.AttributeOrNull("defaultItem");
                                presPart = new DropDownPresentationElement
                                {
                                    NoSort = Convert.ToBoolean(uiElement.AttributeOrDefault("noSort", false)),
                                    DefaultItemID = defaultItem is null ? (int?)null : int.Parse(defaultItem, CultureInfo.InvariantCulture),
                                    Label = uiElement.InnerText
                                };
                            }
                            break;
                        case "listBox":
                            presPart = new ListPresentationElement { Label = uiElement.InnerText };
                            break;
                        case "multiTextBox":
                            presPart = new MultiTextPresentationElement { Label = uiElement.InnerText };
                            break;
                    }
                    if (presPart is not null)
                    {
                        if (uiElement.Attributes["refId"] is not null) presPart.ID = uiElement.Attributes["refId"].Value;
                        presPart.ElementType = uiElement.LocalName;
                        presentation.Elements.Add(presPart);
                    }
                }
                adml.PresentationTable.Add(presentation.Name, presentation);
            }
        }
        return adml;
    }
}
