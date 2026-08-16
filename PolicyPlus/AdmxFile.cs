using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

public class AdmxFile
{
    public string SourceFile;
    public string AdmxNamespace;
    public string SupersededAdm;
    public decimal MinAdmlVersion;
    public Dictionary<string, string> Prefixes = new();
    public List<AdmxProduct> Products = new();
    public List<AdmxSupportDefinition> SupportedOnDefinitions = new();
    public List<AdmxCategory> Categories = new();
    public List<AdmxPolicy> Policies = new();

    private AdmxFile() { }

    // ADMX documentation: https://technet.microsoft.com/en-us/library/cc772138(v=ws.10).aspx
    //
    // Load dispatches each top-level <policyDefinitions> child to one of the section loaders
    // below in a single pass. The sections are independent - none reads state written by an
    // earlier one (cross-references like a policy's category/supportedOn ID are resolved later,
    // in AdmxBundle.BuildStructures) - but keep this as one pass over policyDefinitions.ChildNodes
    // rather than independent per-section queries (e.g. GetElementsByTagName), which search the
    // whole subtree instead of just immediate children and could pick up unexpected nested
    // elements. Also don't add null-guards or try/catch anywhere in here or the loaders below:
    // AdmxBundle.AddSingleAdmx distinguishes a malformed-XML XmlException (BadAdmxParse) from a
    // missing-required-attribute NullReferenceException (BadAdmx) - swallowing or wrapping either
    // would silently change which failure type a malformed ADMX reports.
    public static AdmxFile Load(string File)
    {
        var admx = new AdmxFile { SourceFile = File };
        var xmlDoc = new XmlDocument();
        xmlDoc.Load(File);
        XmlNode policyDefinitions = xmlDoc.GetElementsByTagName("policyDefinitions")[0];

        foreach (XmlNode child in policyDefinitions.ChildNodes)
        {
            switch (child.LocalName)
            {
                case "policyNamespaces": // Referenced namespaces and current namespace
                    LoadNamespaces(child, admx);
                    break;

                case "supersededAdm": // The ADM file that this ADMX supersedes
                    admx.SupersededAdm = child.Attributes["fileName"].Value;
                    break;

                case "resources": // Minimum required version
                    admx.MinAdmlVersion = decimal.Parse(child.Attributes["minRequiredRevision"].Value, CultureInfo.InvariantCulture);
                    break;

                case "supportedOn": // Support definitions
                    LoadSupportedOn(child, admx);
                    break;

                case "categories": // Categories
                    LoadCategories(child, admx);
                    break;

                case "policies": // Policy settings
                    LoadPolicies(child, admx);
                    break;
            }
        }
        return admx;
    }

    private static void LoadNamespaces(XmlNode policyNamespacesNode, AdmxFile admx)
    {
        foreach (XmlNode policyNamespace in policyNamespacesNode.ChildNodes)
        {
            string prefix = policyNamespace.Attributes["prefix"].Value;
            string fqNamespace = policyNamespace.Attributes["namespace"].Value;
            if (policyNamespace.LocalName == "target") admx.AdmxNamespace = fqNamespace;
            admx.Prefixes.Add(prefix, fqNamespace);
        }
    }

    // Dispatches to the "definitions" (support-definition) and "products" (product hierarchy)
    // sub-sections from a single pass over supportedOnNode.ChildNodes, same as Load's own
    // top-level dispatch - keep both driven from this one loop rather than two separate passes.
    private static void LoadSupportedOn(XmlNode supportedOnNode, AdmxFile admx)
    {
        foreach (XmlNode supportInfo in supportedOnNode.ChildNodes)
        {
            if (supportInfo.LocalName == "definitions")
            {
                LoadSupportedOnDefinitions(supportInfo, admx);
            }
            else if (supportInfo.LocalName == "products") // Product definitions
            {
                LoadProducts(supportInfo, "product", null, admx); // Start the recursive load
            }
        }
    }

    private static void LoadSupportedOnDefinitions(XmlNode definitionsNode, AdmxFile admx)
    {
        foreach (XmlNode supportDef in definitionsNode.ChildNodes)
        {
            if (supportDef.LocalName != "definition") continue;
            var definition = new AdmxSupportDefinition
            {
                ID = supportDef.Attributes["name"].Value,
                DisplayCode = supportDef.Attributes["displayName"].Value,
                Logic = AdmxSupportLogicType.Blank
            };
            foreach (XmlNode logicElement in supportDef.ChildNodes)
            {
                bool canLoad = true;
                if (logicElement.LocalName == "or")
                {
                    definition.Logic = AdmxSupportLogicType.AnyOf;
                }
                else if (logicElement.LocalName == "and")
                {
                    definition.Logic = AdmxSupportLogicType.AllOf;
                }
                else
                {
                    canLoad = false;
                }
                if (canLoad)
                {
                    definition.Entries = new List<AdmxSupportEntry>();
                    foreach (XmlNode conditionElement in logicElement.ChildNodes)
                    {
                        if (conditionElement.LocalName == "reference")
                        {
                            string product = conditionElement.Attributes["ref"].Value;
                            definition.Entries.Add(new AdmxSupportEntry { ProductID = product, IsRange = false });
                        }
                        else if (conditionElement.LocalName == "range")
                        {
                            var entry = new AdmxSupportEntry { IsRange = true, ProductID = conditionElement.Attributes["ref"].Value };
                            var maxVerAttr = conditionElement.Attributes["maxVersionIndex"];
                            if (maxVerAttr is not null) entry.MaxVersion = int.Parse(maxVerAttr.Value, CultureInfo.InvariantCulture);
                            var minVerAttr = conditionElement.Attributes["minVersionIndex"];
                            if (minVerAttr is not null) entry.MinVersion = int.Parse(minVerAttr.Value, CultureInfo.InvariantCulture);
                            definition.Entries.Add(entry);
                        }
                    }
                    break;
                }
            }
            definition.DefinedIn = admx;
            admx.SupportedOnDefinitions.Add(definition);
        }
    }

    // ChildTagName/Parent encode the fixed 3-level product/majorVersion/minorVersion hierarchy
    // the ADMX schema itself hard-codes - not a generic depth parameter to "improve".
    private static void LoadProducts(XmlNode Node, string ChildTagName, AdmxProduct Parent, AdmxFile admx)
    {
        foreach (XmlNode subproductElement in Node.ChildNodes)
        {
            if (subproductElement.LocalName != ChildTagName) continue;
            var product = new AdmxProduct
            {
                ID = subproductElement.Attributes["name"].Value,
                DisplayCode = subproductElement.Attributes["displayName"].Value
            };
            if (Parent is not null) product.Version = int.Parse(subproductElement.Attributes["versionIndex"].Value, CultureInfo.InvariantCulture);
            product.Parent = Parent;
            product.DefinedIn = admx;
            admx.Products.Add(product);
            if (Parent is null)
            {
                product.Type = AdmxProductType.Product;
                LoadProducts(subproductElement, "majorVersion", product, admx);
            }
            else if (Parent.Parent is null)
            {
                product.Type = AdmxProductType.MajorRevision;
                LoadProducts(subproductElement, "minorVersion", product, admx);
            }
            else
            {
                product.Type = AdmxProductType.MinorRevision;
            }
        }
    }

    private static void LoadCategories(XmlNode categoriesNode, AdmxFile admx)
    {
        foreach (XmlNode categoryElement in categoriesNode.ChildNodes)
        {
            if (categoryElement.LocalName != "category") continue;
            var category = new AdmxCategory
            {
                ID = categoryElement.Attributes["name"].Value,
                DisplayCode = categoryElement.Attributes["displayName"].Value,
                ExplainCode = categoryElement.AttributeOrNull("explainText")
            };
            if (categoryElement.HasChildNodes)
            {
                XmlElement parentCatElement = ((XmlElement)categoryElement)["parentCategory"];
                category.ParentID = parentCatElement.Attributes["ref"].Value;
            }
            category.DefinedIn = admx;
            admx.Categories.Add(category);
        }
    }

    private static void LoadPolicies(XmlNode policiesNode, AdmxFile admx)
    {
        foreach (XmlNode polElement in policiesNode.ChildNodes)
        {
            if (polElement.LocalName != "policy") continue;
            var policy = LoadPolicy(polElement);
            policy.DefinedIn = admx;
            admx.Policies.Add(policy);
        }
    }

    private static AdmxPolicy LoadPolicy(XmlNode polElement)
    {
        var policy = new AdmxPolicy
        {
            ID = polElement.Attributes["name"].Value,
            DisplayCode = polElement.Attributes["displayName"].Value,
            RegistryKey = polElement.Attributes["key"].Value
        };
        string polClass = polElement.Attributes["class"].Value;
        switch (polClass)
        {
            case "Machine":
                policy.Section = AdmxPolicySection.Machine;
                break;
            case "User":
                policy.Section = AdmxPolicySection.User;
                break;
            default:
                policy.Section = AdmxPolicySection.Both;
                break;
        }
        policy.ExplainCode = polElement.AttributeOrNull("explainText");
        policy.PresentationID = polElement.AttributeOrNull("presentation");
        policy.ClientExtension = polElement.AttributeOrNull("clientExtension");
        policy.RegistryValue = polElement.AttributeOrNull("valueName");
        policy.AffectedValues = LoadOnOffValueList("enabledValue", "disabledValue", "enabledList", "disabledList", polElement);
        foreach (XmlNode polInfo in polElement.ChildNodes)
        {
            switch (polInfo.LocalName)
            {
                case "parentCategory":
                    policy.CategoryID = polInfo.Attributes["ref"].Value;
                    break;
                case "supportedOn":
                    policy.SupportedCode = polInfo.Attributes["ref"].Value;
                    break;
                case "elements":
                    policy.Elements = LoadPolicyElements(polInfo);
                    break;
            }
        }
        return policy;
    }

    private static List<PolicyElement> LoadPolicyElements(XmlNode elementsNode)
    {
        var elements = new List<PolicyElement>();
        foreach (XmlNode uiElement in elementsNode.ChildNodes)
        {
            PolicyElement entry = null;
            switch (uiElement.LocalName)
            {
                case "decimal":
                    entry = new DecimalPolicyElement
                    {
                        Minimum = Convert.ToUInt32(uiElement.AttributeOrDefault("minValue", 0)),
                        Maximum = Convert.ToUInt32(uiElement.AttributeOrDefault("maxValue", uint.MaxValue)),
                        NoOverwrite = Convert.ToBoolean(uiElement.AttributeOrDefault("soft", false)),
                        StoreAsText = Convert.ToBoolean(uiElement.AttributeOrDefault("storeAsText", false))
                    };
                    break;
                case "boolean":
                    entry = new BooleanPolicyElement
                    {
                        AffectedRegistry = LoadOnOffValueList("trueValue", "falseValue", "trueList", "falseList", uiElement)
                    };
                    break;
                case "text":
                    entry = new TextPolicyElement
                    {
                        MaxLength = Convert.ToInt32(uiElement.AttributeOrDefault("maxLength", 255)),
                        Required = Convert.ToBoolean(uiElement.AttributeOrDefault("required", false)),
                        RegExpandSz = Convert.ToBoolean(uiElement.AttributeOrDefault("expandable", false)),
                        NoOverwrite = Convert.ToBoolean(uiElement.AttributeOrDefault("soft", false))
                    };
                    break;
                case "list":
                    entry = new ListPolicyElement
                    {
                        NoPurgeOthers = Convert.ToBoolean(uiElement.AttributeOrDefault("additive", false)),
                        RegExpandSz = Convert.ToBoolean(uiElement.AttributeOrDefault("expandable", false)),
                        UserProvidesNames = Convert.ToBoolean(uiElement.AttributeOrDefault("explicitValue", false)),
                        HasPrefix = uiElement.Attributes["valuePrefix"] is not null,
                        RegistryValue = uiElement.AttributeOrNull("valuePrefix")
                    };
                    break;
                case "enum":
                    {
                        var enumEntry = new EnumPolicyElement
                        {
                            Required = Convert.ToBoolean(uiElement.AttributeOrDefault("required", false)),
                            Items = new List<EnumPolicyElementItem>()
                        };
                        foreach (XmlNode itemElement in uiElement.ChildNodes)
                        {
                            if (itemElement.LocalName == "item")
                            {
                                var enumItem = new EnumPolicyElementItem
                                {
                                    DisplayCode = itemElement.Attributes["displayName"].Value
                                };
                                foreach (XmlNode valElement in itemElement.ChildNodes)
                                {
                                    if (valElement.LocalName == "value")
                                    {
                                        enumItem.Value = LoadRegistryValue(valElement);
                                    }
                                    else if (valElement.LocalName == "valueList")
                                    {
                                        enumItem.ValueList = LoadRegistrySingleList(valElement);
                                    }
                                }
                                enumEntry.Items.Add(enumItem);
                            }
                        }
                        entry = enumEntry;
                    }
                    break;
                case "multiText":
                    entry = new MultiTextPolicyElement();
                    break;
            }
            if (entry is not null)
            {
                entry.ClientExtension = uiElement.AttributeOrNull("clientExtension");
                entry.RegistryKey = uiElement.AttributeOrNull("key");
                if (string.IsNullOrEmpty(entry.RegistryValue)) entry.RegistryValue = uiElement.AttributeOrNull("valueName");
                entry.ID = uiElement.Attributes["id"].Value;
                entry.ElementType = uiElement.LocalName;
                elements.Add(entry);
            }
        }
        return elements;
    }

    private static PolicyRegistryList LoadOnOffValueList(string OnValueName, string OffValueName, string OnListName, string OffListName, XmlNode Node)
    {
        var regList = new PolicyRegistryList();
        foreach (XmlNode subElement in Node.ChildNodes)
        {
            if (subElement.LocalName == OnValueName)
            {
                regList.OnValue = LoadRegistryValue(subElement);
            }
            else if (subElement.LocalName == OffValueName)
            {
                regList.OffValue = LoadRegistryValue(subElement);
            }
            else if (subElement.LocalName == OnListName)
            {
                regList.OnValueList = LoadRegistrySingleList(subElement);
            }
            else if (subElement.LocalName == OffListName)
            {
                regList.OffValueList = LoadRegistrySingleList(subElement);
            }
        }
        return regList;
    }

    private static PolicyRegistrySingleList LoadRegistrySingleList(XmlNode Node)
    {
        var singleList = new PolicyRegistrySingleList
        {
            DefaultRegistryKey = Node.AttributeOrNull("defaultKey"),
            AffectedValues = new List<PolicyRegistryListEntry>()
        };
        foreach (XmlNode itemElement in Node.ChildNodes)
        {
            if (itemElement.LocalName != "item") continue;
            var listEntry = new PolicyRegistryListEntry
            {
                RegistryValue = itemElement.Attributes["valueName"].Value,
                RegistryKey = itemElement.AttributeOrNull("key")
            };
            foreach (XmlNode valElement in itemElement.ChildNodes)
            {
                if (valElement.LocalName == "value")
                {
                    listEntry.Value = LoadRegistryValue(valElement);
                    break;
                }
            }
            singleList.AffectedValues.Add(listEntry);
        }
        return singleList;
    }

    private static PolicyRegistryValue LoadRegistryValue(XmlNode Node)
    {
        var regItem = new PolicyRegistryValue();
        foreach (XmlNode subElement in Node.ChildNodes)
        {
            if (subElement.LocalName == "delete")
            {
                regItem.RegistryType = PolicyRegistryValueType.Delete;
                break;
            }
            else if (subElement.LocalName == "decimal")
            {
                regItem.RegistryType = PolicyRegistryValueType.Numeric;
                regItem.NumberValue = uint.Parse(subElement.Attributes["value"].Value, CultureInfo.InvariantCulture);
                break;
            }
            else if (subElement.LocalName == "string")
            {
                regItem.RegistryType = PolicyRegistryValueType.Text;
                regItem.StringValue = subElement.InnerText;
                break;
            }
        }
        return regItem;
    }
}
