using System.Collections.Generic;

// These structures hold information on the behavior of policies and policy elements
public class PolicyRegistryList
{
    public PolicyRegistryValue OnValue;
    public PolicyRegistrySingleList OnValueList;
    public PolicyRegistryValue OffValue;
    public PolicyRegistrySingleList OffValueList;
}

public class PolicyRegistrySingleList
{
    public string DefaultRegistryKey;
    public List<PolicyRegistryListEntry> AffectedValues;
}

// <value>
public class PolicyRegistryValue
{
    public PolicyRegistryValueType RegistryType;
    public string StringValue;
    public uint NumberValue;
}

// <item>
public class PolicyRegistryListEntry
{
    public string RegistryValue;
    public string RegistryKey;
    public PolicyRegistryValue Value;
}

public enum PolicyRegistryValueType
{
    Delete,
    Numeric,
    Text
}

public abstract class PolicyElement
{
    public string ID;
    public string ClientExtension;
    public string RegistryKey;
    public string RegistryValue;
    public string ElementType;
}

// <decimal>
public class DecimalPolicyElement : PolicyElement
{
    public bool Required;
    public uint Minimum;
    public uint Maximum = uint.MaxValue;
    public bool StoreAsText;
    public bool NoOverwrite;
}

// <boolean>
public class BooleanPolicyElement : PolicyElement
{
    public PolicyRegistryList AffectedRegistry;
}

// <text>
public class TextPolicyElement : PolicyElement
{
    public bool Required;
    public int MaxLength;
    public bool RegExpandSz;
    public bool NoOverwrite;
}

// <list>
public class ListPolicyElement : PolicyElement
{
    public bool HasPrefix;
    public bool NoPurgeOthers;
    public bool RegExpandSz;
    public bool UserProvidesNames;
}

// <enum>
public class EnumPolicyElement : PolicyElement
{
    public bool Required;
    public List<EnumPolicyElementItem> Items;
}

// <item>
public class EnumPolicyElementItem
{
    public string DisplayCode;
    public PolicyRegistryValue Value;
    public PolicyRegistrySingleList ValueList; // <valueList>
}

public class MultiTextPolicyElement : PolicyElement
{
    // This is undocumented, so it's unknown whether there can be other options for it
}
