using System.IO;
using System.Linq;
using System.Xml;

namespace PolicyPlus.Tests;

// Baseline regression tests capturing AdmxFile.Load's CURRENT behavior, written before
// splitting its single ~360-line method into per-section private static methods. No tests
// existed for ADMX parsing before - a failing test after the split means real behavior
// changed, not just where the code lives.
public class AdmxFileTests
{
    // Covers every top-level section (policyNamespaces, supersededAdm, resources,
    // supportedOn definitions + a 3-level product hierarchy, categories with a parent
    // reference, and a policy exercising every <elements> child type plus the policy's own
    // enabledValue/disabledValue) in one document, since Load parses them all from a single
    // pass over the same <policyDefinitions> children.
    private const string FullFixture = """
        <?xml version="1.0" encoding="utf-8"?>
        <policyDefinitions revision="1.0" schemaVersion="1.0">
          <policyNamespaces>
            <target prefix="test" namespace="Test.Policies.Sample" />
            <using prefix="windows" namespace="Microsoft.Policies.Windows" />
          </policyNamespaces>
          <supersededAdm fileName="sample.adm" />
          <resources minRequiredRevision="1.5" />
          <supportedOn>
            <definitions>
              <definition name="SUPPORTED_TestOr" displayName="$(string.SUPPORTED_TestOr)">
                <or>
                  <reference ref="Product1" />
                  <reference ref="Product2" />
                </or>
              </definition>
              <definition name="SUPPORTED_TestAnd" displayName="$(string.SUPPORTED_TestAnd)">
                <and>
                  <range ref="Product1" minVersionIndex="1" maxVersionIndex="2" />
                </and>
              </definition>
            </definitions>
            <products>
              <product name="Product1" displayName="$(string.Product1)">
                <majorVersion name="Product1V1" displayName="$(string.Product1V1)" versionIndex="1">
                  <minorVersion name="Product1V1M0" displayName="$(string.Product1V1M0)" versionIndex="0" />
                </majorVersion>
              </product>
            </products>
          </supportedOn>
          <categories>
            <category name="RootCat" displayName="$(string.RootCat)" />
            <category name="ChildCat" displayName="$(string.ChildCat)" explainText="$(string.ChildCat_Explain)">
              <parentCategory ref="RootCat" />
            </category>
          </categories>
          <policies>
            <policy name="TestPolicy" class="Both" displayName="$(string.TestPolicy)" explainText="$(string.TestPolicy_Explain)" presentation="$(presentation.TestPolicy)" key="Software\Policies\TestVendor\TestApp" valueName="EnabledFlag">
              <parentCategory ref="ChildCat" />
              <supportedOn ref="SUPPORTED_TestOr" />
              <enabledValue><decimal value="1" /></enabledValue>
              <disabledValue><decimal value="0" /></disabledValue>
              <elements>
                <decimal id="DecimalElem" key="Software\Policies\TestVendor\TestApp" valueName="DecimalVal" minValue="0" maxValue="100" />
                <boolean id="BoolElem" key="Software\Policies\TestVendor\TestApp" valueName="BoolVal">
                  <trueValue><decimal value="1" /></trueValue>
                  <falseValue><decimal value="0" /></falseValue>
                </boolean>
                <text id="TextElem" key="Software\Policies\TestVendor\TestApp" valueName="TextVal" maxLength="100" required="true" />
                <list id="ListElem" key="Software\Policies\TestVendor\TestApp" valuePrefix="ListVal" />
                <enum id="EnumElem" key="Software\Policies\TestVendor\TestApp" valueName="EnumVal" required="true">
                  <item displayName="$(string.EnumItem1)">
                    <value><decimal value="1" /></value>
                  </item>
                  <item displayName="$(string.EnumItem2)">
                    <value><decimal value="2" /></value>
                    <valueList>
                      <item key="Software\Policies\TestVendor\TestApp\SubKey" valueName="SubVal"><value><string>Hello</string></value></item>
                    </valueList>
                  </item>
                </enum>
                <multiText id="MultiTextElem" key="Software\Policies\TestVendor\TestApp" valueName="MultiTextVal" />
              </elements>
            </policy>
            <policy name="MinimalPolicy" class="SomethingElse" displayName="$(string.MinimalPolicy)" key="Software\Policies\TestVendor\TestApp">
            </policy>
          </policies>
        </policyDefinitions>
        """;

    private const string MissingRequiredAttributeFixture = """
        <?xml version="1.0" encoding="utf-8"?>
        <policyDefinitions revision="1.0" schemaVersion="1.0">
          <categories>
            <category displayName="$(string.Bad)" />
          </categories>
        </policyDefinitions>
        """;

    private const string NotWellFormedFixture = """
        <?xml version="1.0" encoding="utf-8"?>
        <policyDefinitions revision="1.0" schemaVersion="1.0">
          <categories>
        </policyDefinitions>
        """;

    private static AdmxFile LoadFixture(string xml)
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".admx");
        File.WriteAllText(path, xml);
        try
        {
            return AdmxFile.Load(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_ParsesNamespacesSupersededAdmAndResources()
    {
        var admx = LoadFixture(FullFixture);

        Assert.Equal("Test.Policies.Sample", admx.AdmxNamespace);
        Assert.Equal("Test.Policies.Sample", admx.Prefixes["test"]);
        Assert.Equal("Microsoft.Policies.Windows", admx.Prefixes["windows"]);
        Assert.Equal("sample.adm", admx.SupersededAdm);
        Assert.Equal(1.5m, admx.MinAdmlVersion);
    }

    [Fact]
    public void Load_ParsesSupportedOnDefinitionsAndProductHierarchy()
    {
        var admx = LoadFixture(FullFixture);

        var orDef = admx.SupportedOnDefinitions.Single(d => d.ID == "SUPPORTED_TestOr");
        Assert.Equal(AdmxSupportLogicType.AnyOf, orDef.Logic);
        Assert.Equal(2, orDef.Entries.Count);
        Assert.False(orDef.Entries[0].IsRange);
        Assert.Equal("Product1", orDef.Entries[0].ProductID);
        Assert.Same(admx, orDef.DefinedIn);

        var andDef = admx.SupportedOnDefinitions.Single(d => d.ID == "SUPPORTED_TestAnd");
        Assert.Equal(AdmxSupportLogicType.AllOf, andDef.Logic);
        var rangeEntry = Assert.Single(andDef.Entries);
        Assert.True(rangeEntry.IsRange);
        Assert.Equal(1, rangeEntry.MinVersion);
        Assert.Equal(2, rangeEntry.MaxVersion);

        Assert.Equal(3, admx.Products.Count);
        var product = admx.Products.Single(p => p.ID == "Product1");
        Assert.Equal(AdmxProductType.Product, product.Type);
        Assert.Null(product.Parent);
        var major = admx.Products.Single(p => p.ID == "Product1V1");
        Assert.Equal(AdmxProductType.MajorRevision, major.Type);
        Assert.Equal(1, major.Version);
        Assert.Same(product, major.Parent);
        var minor = admx.Products.Single(p => p.ID == "Product1V1M0");
        Assert.Equal(AdmxProductType.MinorRevision, minor.Type);
        Assert.Equal(0, minor.Version);
        Assert.Same(major, minor.Parent);
    }

    [Fact]
    public void Load_ParsesCategoriesWithParentReference()
    {
        var admx = LoadFixture(FullFixture);

        var root = admx.Categories.Single(c => c.ID == "RootCat");
        Assert.Null(root.ParentID);
        Assert.Null(root.ExplainCode);

        var child = admx.Categories.Single(c => c.ID == "ChildCat");
        Assert.Equal("RootCat", child.ParentID);
        Assert.Equal("$(string.ChildCat_Explain)", child.ExplainCode);
    }

    [Fact]
    public void Load_ParsesPolicyWithAllElementTypes()
    {
        var admx = LoadFixture(FullFixture);
        var policy = admx.Policies.Single(p => p.ID == "TestPolicy");

        Assert.Equal(AdmxPolicySection.Both, policy.Section);
        Assert.Equal("ChildCat", policy.CategoryID);
        Assert.Equal("SUPPORTED_TestOr", policy.SupportedCode);
        Assert.Equal(@"Software\Policies\TestVendor\TestApp", policy.RegistryKey);
        Assert.Equal("EnabledFlag", policy.RegistryValue);

        Assert.Equal(PolicyRegistryValueType.Numeric, policy.AffectedValues.OnValue.RegistryType);
        Assert.Equal(1u, policy.AffectedValues.OnValue.NumberValue);
        Assert.Equal(0u, policy.AffectedValues.OffValue.NumberValue);

        Assert.Equal(6, policy.Elements.Count);

        var dec = Assert.IsType<DecimalPolicyElement>(policy.Elements.Single(e => e.ID == "DecimalElem"));
        Assert.Equal(0u, dec.Minimum);
        Assert.Equal(100u, dec.Maximum);

        var boolElem = Assert.IsType<BooleanPolicyElement>(policy.Elements.Single(e => e.ID == "BoolElem"));
        Assert.Equal(1u, boolElem.AffectedRegistry.OnValue.NumberValue);
        Assert.Equal(0u, boolElem.AffectedRegistry.OffValue.NumberValue);

        var text = Assert.IsType<TextPolicyElement>(policy.Elements.Single(e => e.ID == "TextElem"));
        Assert.Equal(100, text.MaxLength);
        Assert.True(text.Required);

        var list = Assert.IsType<ListPolicyElement>(policy.Elements.Single(e => e.ID == "ListElem"));
        Assert.True(list.HasPrefix);
        Assert.Equal("ListVal", list.RegistryValue);

        var enumElem = Assert.IsType<EnumPolicyElement>(policy.Elements.Single(e => e.ID == "EnumElem"));
        Assert.True(enumElem.Required);
        Assert.Equal(2, enumElem.Items.Count);
        Assert.Equal(1u, enumElem.Items[0].Value.NumberValue);
        Assert.Null(enumElem.Items[0].ValueList);
        Assert.Equal(2u, enumElem.Items[1].Value.NumberValue);
        var subEntry = Assert.Single(enumElem.Items[1].ValueList.AffectedValues);
        Assert.Equal("SubVal", subEntry.RegistryValue);
        Assert.Equal(PolicyRegistryValueType.Text, subEntry.Value.RegistryType);
        Assert.Equal("Hello", subEntry.Value.StringValue);

        var multiText = Assert.IsType<MultiTextPolicyElement>(policy.Elements.Single(e => e.ID == "MultiTextElem"));
        Assert.Equal("MultiTextVal", multiText.RegistryValue);
    }

    [Fact]
    public void Load_UnrecognizedClassAttribute_DefaultsSectionToBoth()
    {
        var admx = LoadFixture(FullFixture);
        var policy = admx.Policies.Single(p => p.ID == "MinimalPolicy");

        Assert.Equal(AdmxPolicySection.Both, policy.Section);
        Assert.Null(policy.CategoryID);
        Assert.Null(policy.SupportedCode);
        Assert.Null(policy.Elements);
    }

    [Fact]
    public void Load_MissingRequiredAttribute_ThrowsNullReferenceException()
    {
        // AdmxBundle.AddSingleAdmx relies on this being a plain NullReferenceException (mapped
        // to AdmxLoadFailType.BadAdmx), distinct from the XmlException a not-well-formed
        // document throws (mapped to BadAdmxParse) - preserve both exception types exactly.
        Assert.Throws<NullReferenceException>(() => LoadFixture(MissingRequiredAttributeFixture));
    }

    [Fact]
    public void Load_NotWellFormedXml_ThrowsXmlException()
    {
        Assert.Throws<XmlException>(() => LoadFixture(NotWellFormedFixture));
    }
}
