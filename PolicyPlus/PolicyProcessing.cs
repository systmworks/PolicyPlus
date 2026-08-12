using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

public class PolicyProcessing
{
    // Determine the basic state of a policy
    public static PolicyState GetPolicyState(IPolicySource PolicySource, PolicyPlusPolicy Policy)
    {
        decimal enabledEvidence = 0;
        decimal disabledEvidence = 0;
        var rawpol = Policy.RawPolicy;

        void checkOneVal(PolicyRegistryValue Value, string Key, string ValueName, ref decimal EvidenceVar)
        {
            if (Value is null) return;
            if (ValuePresent(Value, PolicySource, Key, ValueName)) EvidenceVar += 1;
        }

        void checkValList(PolicyRegistrySingleList ValList, string DefaultKey, ref decimal EvidenceVar)
        {
            if (ValList is null) return;
            string listKey = string.IsNullOrEmpty(ValList.DefaultRegistryKey) ? DefaultKey : ValList.DefaultRegistryKey;
            foreach (var regVal in ValList.AffectedValues)
            {
                string entryKey = string.IsNullOrEmpty(regVal.RegistryKey) ? listKey : regVal.RegistryKey;
                checkOneVal(regVal.Value, entryKey, regVal.RegistryValue, ref EvidenceVar);
            }
        }

        // Check the policy's standard Registry values
        if (!string.IsNullOrEmpty(rawpol.RegistryValue))
        {
            if (rawpol.AffectedValues.OnValue is null)
            {
                checkOneVal(new PolicyRegistryValue { NumberValue = 1U, RegistryType = PolicyRegistryValueType.Numeric }, rawpol.RegistryKey, rawpol.RegistryValue, ref enabledEvidence);
            }
            else
            {
                checkOneVal(rawpol.AffectedValues.OnValue, rawpol.RegistryKey, rawpol.RegistryValue, ref enabledEvidence);
            }
            if (rawpol.AffectedValues.OffValue is null)
            {
                checkOneVal(new PolicyRegistryValue { RegistryType = PolicyRegistryValueType.Delete }, rawpol.RegistryKey, rawpol.RegistryValue, ref disabledEvidence);
            }
            else
            {
                checkOneVal(rawpol.AffectedValues.OffValue, rawpol.RegistryKey, rawpol.RegistryValue, ref disabledEvidence);
            }
        }
        checkValList(rawpol.AffectedValues.OnValueList, rawpol.RegistryKey, ref enabledEvidence);
        checkValList(rawpol.AffectedValues.OffValueList, rawpol.RegistryKey, ref disabledEvidence);

        // Check the policy's elements
        if (rawpol.Elements is not null)
        {
            decimal deletedElements = 0;
            decimal presentElements = 0;
            foreach (var elem in rawpol.Elements)
            {
                string elemKey = string.IsNullOrEmpty(elem.RegistryKey) ? rawpol.RegistryKey : elem.RegistryKey;
                if (elem.ElementType == "list")
                {
                    int neededValues = 0;
                    if (PolicySource.WillDeleteValue(elemKey, ""))
                    {
                        deletedElements += 1;
                        neededValues = 1;
                    }
                    if (PolicySource.GetValueNames(elemKey).Count > 0)
                    {
                        deletedElements -= neededValues;
                        presentElements += 1;
                    }
                }
                else if (elem.ElementType == "boolean")
                {
                    var booleanElem = (BooleanPolicyElement)elem;
                    if (PolicySource.WillDeleteValue(elemKey, elem.RegistryValue))
                    {
                        deletedElements += 1; // Implicit checkboxes are deleted when the policy is disabled
                    }
                    else
                    {
                        decimal checkboxDisabled = 0;
                        checkOneVal(booleanElem.AffectedRegistry.OffValue, elemKey, elem.RegistryValue, ref checkboxDisabled);
                        checkValList(booleanElem.AffectedRegistry.OffValueList, elemKey, ref checkboxDisabled);
                        deletedElements += checkboxDisabled * 0.1M; // Checkboxes in the off state are weak evidence for the policy being disabled
                        checkOneVal(booleanElem.AffectedRegistry.OnValue, elemKey, elem.RegistryValue, ref presentElements);
                        checkValList(booleanElem.AffectedRegistry.OnValueList, elemKey, ref presentElements);
                    }
                }
                else
                {
                    if (PolicySource.WillDeleteValue(elemKey, elem.RegistryValue))
                    {
                        deletedElements += 1;
                    }
                    else if (PolicySource.ContainsValue(elemKey, elem.RegistryValue))
                    {
                        presentElements += 1;
                    }
                }
            }
            if (presentElements > 0)
            {
                enabledEvidence += presentElements;
            }
            else if (deletedElements > 0)
            {
                disabledEvidence += deletedElements;
            }
        }

        // Judge the evidence collected
        if (enabledEvidence > disabledEvidence)
        {
            return PolicyState.Enabled;
        }
        else if (disabledEvidence > enabledEvidence)
        {
            return PolicyState.Disabled;
        }
        else if (enabledEvidence == 0) // No evidence for either side
        {
            return PolicyState.NotConfigured;
        }
        else
        {
            return PolicyState.Unknown;
        }
    }

    // Determine whether the given value is found in the Registry
    private static bool ValuePresent(PolicyRegistryValue Value, IPolicySource Source, string Key, string ValueName)
    {
        switch (Value.RegistryType)
        {
            case PolicyRegistryValueType.Delete:
                return Source.WillDeleteValue(Key, ValueName);
            case PolicyRegistryValueType.Numeric:
                {
                    if (!Source.ContainsValue(Key, ValueName)) return false;
                    var sourceVal = Source.GetValue(Key, ValueName);
                    if (sourceVal is not uint && sourceVal is not int) return false;
                    return Convert.ToInt64(sourceVal) == Value.NumberValue;
                }
            case PolicyRegistryValueType.Text:
                {
                    if (!Source.ContainsValue(Key, ValueName)) return false;
                    var sourceVal = Source.GetValue(Key, ValueName);
                    if (sourceVal is not string) return false;
                    return (string)sourceVal == Value.StringValue;
                }
            default:
                throw new InvalidOperationException("Illegal value type");
        }
    }

    // Determine whether all the values in a value list are in the Registry data
    private static bool ValueListPresent(PolicyRegistrySingleList ValueList, IPolicySource Source, string Key, string ValueName)
    {
        string sublistKey = string.IsNullOrEmpty(ValueList.DefaultRegistryKey) ? Key : ValueList.DefaultRegistryKey;
        return ValueList.AffectedValues.All(e =>
        {
            string entryKey = string.IsNullOrEmpty(e.RegistryKey) ? sublistKey : e.RegistryKey;
            return ValuePresent(e.Value, Source, entryKey, e.RegistryValue);
        });
    }

    // Merge otherwise-identical pairs of user and computer policies into both-section policies
    public static int DeduplicatePolicies(AdmxBundle Workspace)
    {
        int dedupeCount = 0;
        foreach (var cat in Workspace.Policies.GroupBy(c => c.Value.Category))
        {
            foreach (var namegroup in cat.GroupBy(p => p.Value.DisplayName).Select(x => x.ToList()).ToList())
            {
                if (namegroup.Count != 2) continue;
                var a = namegroup[0].Value;
                var b = namegroup[1].Value;
                if ((int)a.RawPolicy.Section + (int)b.RawPolicy.Section != (int)AdmxPolicySection.Both) continue;
                if (a.DisplayExplanation != b.DisplayExplanation) continue;
                if (a.RawPolicy.RegistryKey != b.RawPolicy.RegistryKey) continue;
                a.Category.Policies.Remove(a);
                Workspace.Policies.Remove(a.UniqueID);
                b.RawPolicy.Section = AdmxPolicySection.Both;
                dedupeCount += 1;
            }
        }
        return dedupeCount;
    }

    // Get the element states of an enabled policy
    public static Dictionary<string, object> GetPolicyOptionStates(IPolicySource PolicySource, PolicyPlusPolicy Policy)
    {
        var state = new Dictionary<string, object>();
        if (Policy.RawPolicy.Elements is null) return state;
        foreach (var elem in Policy.RawPolicy.Elements)
        {
            string elemKey = string.IsNullOrEmpty(elem.RegistryKey) ? Policy.RawPolicy.RegistryKey : elem.RegistryKey;
            switch (elem.ElementType)
            {
                case "decimal":
                    state.Add(elem.ID, Convert.ToUInt32(PolicySource.GetValue(elemKey, elem.RegistryValue)));
                    break;
                case "boolean":
                    {
                        var booleanElem = (BooleanPolicyElement)elem;
                        state.Add(elem.ID, GetRegistryListState(PolicySource, booleanElem.AffectedRegistry, elemKey, elem.RegistryValue));
                    }
                    break;
                case "text":
                    state.Add(elem.ID, PolicySource.GetValue(elemKey, elem.RegistryValue));
                    break;
                case "list":
                    {
                        var listElem = (ListPolicyElement)elem;
                        if (listElem.UserProvidesNames) // Keys matter, use a dictionary
                        {
                            var entries = new Dictionary<string, string>();
                            foreach (var value in PolicySource.GetValueNames(elemKey))
                            {
                                entries.Add(value, Convert.ToString(PolicySource.GetValue(elemKey, value)));
                            }
                            state.Add(elem.ID, entries);
                        }
                        else // Keys don't matter, use a list
                        {
                            var entries = new List<string>();
                            if (listElem.HasPrefix)
                            {
                                int n = 1;
                                while (PolicySource.ContainsValue(elemKey, elem.RegistryValue + n))
                                {
                                    entries.Add(Convert.ToString(PolicySource.GetValue(elemKey, elem.RegistryValue + n)));
                                    n += 1;
                                }
                            }
                            else
                            {
                                foreach (var value in PolicySource.GetValueNames(elemKey))
                                {
                                    entries.Add(value);
                                }
                            }
                            state.Add(elem.ID, entries);
                        }
                    }
                    break;
                case "enum":
                    {
                        // Determine which option has results that match the Registry
                        var enumElem = (EnumPolicyElement)elem;
                        int selectedIndex = -1;
                        for (int n = 0; n <= enumElem.Items.Count - 1; n++)
                        {
                            var enumItem = enumElem.Items[n];
                            if (ValuePresent(enumItem.Value, PolicySource, elemKey, elem.RegistryValue))
                            {
                                if (enumItem.ValueList is null || ValueListPresent(enumItem.ValueList, PolicySource, elemKey, elem.RegistryValue))
                                {
                                    selectedIndex = n;
                                    break;
                                }
                            }
                        }
                        state.Add(elem.ID, selectedIndex);
                    }
                    break;
                case "multiText":
                    state.Add(elem.ID, PolicySource.GetValue(elemKey, elem.RegistryValue));
                    break;
            }
        }
        return state;
    }

    // Whether a list of Registry values is present
    private static bool GetRegistryListState(IPolicySource PolicySource, PolicyRegistryList RegList, string DefaultKey, string DefaultValueName)
    {
        bool isListAllPresent(PolicyRegistrySingleList l) => ValueListPresent(l, PolicySource, DefaultKey, DefaultValueName);

        if (RegList.OnValue is not null)
        {
            if (ValuePresent(RegList.OnValue, PolicySource, DefaultKey, DefaultValueName)) return true;
        }
        else if (RegList.OnValueList is not null)
        {
            if (isListAllPresent(RegList.OnValueList)) return true;
        }
        else
        {
            if (Convert.ToUInt32(PolicySource.GetValue(DefaultKey, DefaultValueName)) == 1U) return true;
        }
        if (RegList.OffValue is not null)
        {
            if (ValuePresent(RegList.OffValue, PolicySource, DefaultKey, DefaultValueName)) return false;
        }
        else if (RegList.OffValueList is not null)
        {
            if (isListAllPresent(RegList.OffValueList)) return false;
        }
        return false;
    }

    public static List<RegistryKeyValuePair> GetReferencedRegistryValues(PolicyPlusPolicy Policy)
    {
        return WalkPolicyRegistry(null, Policy, false);
    }

    public static void ForgetPolicy(IPolicySource PolicySource, PolicyPlusPolicy Policy)
    {
        WalkPolicyRegistry(PolicySource, Policy, true);
    }

    // This function handles both GetReferencedRegistryValues and ForgetPolicy because they require searching through the same things
    private static List<RegistryKeyValuePair> WalkPolicyRegistry(IPolicySource PolicySource, PolicyPlusPolicy Policy, bool Forget)
    {
        var entries = new HashSet<RegistryKeyValuePair>();
        void addReg(string Key, string Value)
        {
            entries.Add(new RegistryKeyValuePair { Key = Key, Value = Value });
        }

        // Get all Registry values affected by this policy
        var rawpol = Policy.RawPolicy;
        if (!string.IsNullOrEmpty(rawpol.RegistryValue)) addReg(rawpol.RegistryKey, rawpol.RegistryValue);

        void addSingleList(PolicyRegistrySingleList SingleList, string OverrideKey)
        {
            if (SingleList is null) return;
            string defaultKey = OverrideKey == "" ? rawpol.RegistryKey : OverrideKey;
            string listKey = string.IsNullOrEmpty(SingleList.DefaultRegistryKey) ? defaultKey : SingleList.DefaultRegistryKey;
            foreach (var e in SingleList.AffectedValues)
            {
                string entryKey = string.IsNullOrEmpty(e.RegistryKey) ? listKey : e.RegistryKey;
                addReg(entryKey, e.RegistryValue);
            }
        }

        addSingleList(rawpol.AffectedValues.OnValueList, "");
        addSingleList(rawpol.AffectedValues.OffValueList, "");
        if (rawpol.Elements is not null)
        {
            foreach (var elem in rawpol.Elements)
            {
                string elemKey = string.IsNullOrEmpty(elem.RegistryKey) ? rawpol.RegistryKey : elem.RegistryKey;
                if (elem.ElementType != "list") addReg(elemKey, elem.RegistryValue);
                switch (elem.ElementType)
                {
                    case "boolean":
                        {
                            var booleanElem = (BooleanPolicyElement)elem;
                            addSingleList(booleanElem.AffectedRegistry.OnValueList, elemKey);
                            addSingleList(booleanElem.AffectedRegistry.OffValueList, elemKey);
                        }
                        break;
                    case "enum":
                        {
                            var enumElem = (EnumPolicyElement)elem;
                            foreach (var e in enumElem.Items)
                            {
                                addSingleList(e.ValueList, elemKey);
                            }
                        }
                        break;
                    case "list":
                        if (Forget)
                        {
                            PolicySource.ClearKey(elemKey); // Delete all the values
                            PolicySource.ForgetKeyClearance(elemKey);
                        }
                        else
                        {
                            addReg(elemKey, "");
                        }
                        break;
                }
            }
        }
        if (Forget) // Remove them if forgetting
        {
            foreach (var e in entries)
            {
                PolicySource.ForgetValue(e.Key, e.Value);
            }
        }
        return entries.ToList();
    }

    // Write a full policy state to the policy source
    public static void SetPolicyState(IPolicySource PolicySource, PolicyPlusPolicy Policy, PolicyState State, Dictionary<string, object> Options)
    {
        void setValue(string Key, string ValueName, PolicyRegistryValue Value)
        {
            if (Value is null) return;
            switch (Value.RegistryType)
            {
                case PolicyRegistryValueType.Delete:
                    PolicySource.DeleteValue(Key, ValueName);
                    break;
                case PolicyRegistryValueType.Numeric:
                    PolicySource.SetValue(Key, ValueName, Value.NumberValue, RegistryValueKind.DWord);
                    break;
                case PolicyRegistryValueType.Text:
                    PolicySource.SetValue(Key, ValueName, Value.StringValue, RegistryValueKind.String);
                    break;
            }
        }

        void setSingleList(PolicyRegistrySingleList SingleList, string DefaultKey)
        {
            if (SingleList is null) return;
            string listKey = string.IsNullOrEmpty(SingleList.DefaultRegistryKey) ? DefaultKey : SingleList.DefaultRegistryKey;
            foreach (var e in SingleList.AffectedValues)
            {
                string itemKey = string.IsNullOrEmpty(e.RegistryKey) ? listKey : e.RegistryKey;
                setValue(itemKey, e.RegistryValue, e.Value);
            }
        }

        void setList(PolicyRegistryList List, string DefaultKey, string DefaultValue, bool IsOn)
        {
            if (List is null) return;
            if (IsOn)
            {
                setValue(DefaultKey, DefaultValue, List.OnValue);
                setSingleList(List.OnValueList, DefaultKey);
            }
            else
            {
                setValue(DefaultKey, DefaultValue, List.OffValue);
                setSingleList(List.OffValueList, DefaultKey);
            }
        }

        var rawpol = Policy.RawPolicy;
        switch (State)
        {
            case PolicyState.Enabled:
                if (rawpol.AffectedValues.OnValue is null && !string.IsNullOrEmpty(rawpol.RegistryValue)) PolicySource.SetValue(rawpol.RegistryKey, rawpol.RegistryValue, 1U, RegistryValueKind.DWord);
                setList(rawpol.AffectedValues, rawpol.RegistryKey, rawpol.RegistryValue, true);
                if (rawpol.Elements is not null) // Write the elements' states
                {
                    foreach (var elem in rawpol.Elements)
                    {
                        string elemKey = string.IsNullOrEmpty(elem.RegistryKey) ? rawpol.RegistryKey : elem.RegistryKey;
                        if (!Options.ContainsKey(elem.ID)) continue;
                        var optionData = Options[elem.ID];
                        switch (elem.ElementType)
                        {
                            case "decimal":
                                {
                                    var decimalElem = (DecimalPolicyElement)elem;
                                    if (decimalElem.StoreAsText)
                                    {
                                        PolicySource.SetValue(elemKey, elem.RegistryValue, Convert.ToString(optionData), RegistryValueKind.String);
                                    }
                                    else
                                    {
                                        PolicySource.SetValue(elemKey, elem.RegistryValue, Convert.ToUInt32(optionData), RegistryValueKind.DWord);
                                    }
                                }
                                break;
                            case "boolean":
                                {
                                    var booleanElem = (BooleanPolicyElement)elem;
                                    bool checkState = Convert.ToBoolean(optionData);
                                    if (booleanElem.AffectedRegistry.OnValue is null && checkState)
                                    {
                                        PolicySource.SetValue(elemKey, elem.RegistryValue, 1U, RegistryValueKind.DWord);
                                    }
                                    if (booleanElem.AffectedRegistry.OffValue is null && !checkState)
                                    {
                                        PolicySource.DeleteValue(elemKey, elem.RegistryValue);
                                    }
                                    setList(booleanElem.AffectedRegistry, elemKey, elem.RegistryValue, checkState);
                                }
                                break;
                            case "text":
                                {
                                    var textElem = (TextPolicyElement)elem;
                                    var regType = textElem.RegExpandSz ? RegistryValueKind.ExpandString : RegistryValueKind.String;
                                    PolicySource.SetValue(elemKey, elem.RegistryValue, optionData, regType);
                                }
                                break;
                            case "list":
                                {
                                    var listElem = (ListPolicyElement)elem;
                                    if (!listElem.NoPurgeOthers) PolicySource.ClearKey(elemKey);
                                    if (optionData is null) continue;
                                    var regType = listElem.RegExpandSz ? RegistryValueKind.ExpandString : RegistryValueKind.String;
                                    if (listElem.UserProvidesNames)
                                    {
                                        var items = (Dictionary<string, string>)optionData;
                                        foreach (var i in items)
                                        {
                                            PolicySource.SetValue(elemKey, i.Key, i.Value, regType);
                                        }
                                    }
                                    else
                                    {
                                        var items = (List<string>)optionData;
                                        int n = 1;
                                        while (n <= items.Count)
                                        {
                                            string valueName = listElem.HasPrefix ? listElem.RegistryValue + n : items[n - 1];
                                            PolicySource.SetValue(elemKey, valueName, items[n - 1], regType);
                                            n += 1;
                                        }
                                    }
                                }
                                break;
                            case "enum":
                                {
                                    var enumElem = (EnumPolicyElement)elem;
                                    var selItem = enumElem.Items[Convert.ToInt32(optionData)];
                                    setValue(elemKey, elem.RegistryValue, selItem.Value);
                                    setSingleList(selItem.ValueList, elemKey);
                                }
                                break;
                            case "multiText":
                                PolicySource.SetValue(elemKey, elem.RegistryValue, optionData, RegistryValueKind.MultiString);
                                break;
                        }
                    }
                }
                break;
            case PolicyState.Disabled:
                if (rawpol.AffectedValues.OffValue is null && !string.IsNullOrEmpty(rawpol.RegistryValue)) PolicySource.DeleteValue(rawpol.RegistryKey, rawpol.RegistryValue);
                setList(rawpol.AffectedValues, rawpol.RegistryKey, rawpol.RegistryValue, false);
                if (rawpol.Elements is not null) // Mark all the elements deleted
                {
                    foreach (var elem in rawpol.Elements)
                    {
                        string elemKey = string.IsNullOrEmpty(elem.RegistryKey) ? rawpol.RegistryKey : elem.RegistryKey;
                        if (elem.ElementType == "list")
                        {
                            PolicySource.ClearKey(elemKey);
                        }
                        else if (elem.ElementType == "boolean")
                        {
                            var booleanElem = (BooleanPolicyElement)elem;
                            if (booleanElem.AffectedRegistry.OffValue is not null || booleanElem.AffectedRegistry.OffValueList is not null)
                            {
                                // Non-implicit checkboxes get their "off" value set when the policy is disabled
                                setList(booleanElem.AffectedRegistry, elemKey, elem.RegistryValue, false);
                            }
                            else
                            {
                                PolicySource.DeleteValue(elemKey, elem.RegistryValue);
                            }
                        }
                        else
                        {
                            PolicySource.DeleteValue(elemKey, elem.RegistryValue);
                        }
                    }
                }
                break;
        }
    }

    // Whether a policy is supported on a computer with the given products
    public static bool IsPolicySupported(PolicyPlusPolicy Policy, List<PolicyPlusProduct> Products, bool AlwaysUseAny, bool ApproveLiterals)
    {
        if (Policy.SupportedOn is null || Policy.SupportedOn.RawSupport.Logic == AdmxSupportLogicType.Blank) return ApproveLiterals;

        // Only for products (not support definitions)
        bool supEntryMet(PolicyPlusSupportEntry SupportEntry)
        {
            if (SupportEntry.Product is null) return ApproveLiterals;
            if (Products.Contains(SupportEntry.Product) && !SupportEntry.RawSupportEntry.IsRange) return true;
            if (SupportEntry.Product.Children is null || SupportEntry.Product.Children.Count == 0) return false; // Ranges only apply to parent products
            int rangeMin = SupportEntry.RawSupportEntry.MinVersion ?? 0;
            int rangeMax = SupportEntry.RawSupportEntry.MaxVersion ?? SupportEntry.Product.Children.Max(p => p.RawProduct.Version);
            for (int v = rangeMin; v <= rangeMax; v++)
            {
                int version = v; // To suppress compiler warnings about iteration variable in lambdas
                var subproduct = SupportEntry.Product.Children.FirstOrDefault(p => p.RawProduct.Version == version);
                if (subproduct is null) continue;
                if (Products.Contains(subproduct)) return true;
                if (subproduct.Children is not null && subproduct.Children.Any(p => Products.Contains(p))) return true;
            }
            return false;
        }

        var entriesSeen = new List<PolicyPlusSupport>();
        bool supDefMet(PolicyPlusSupport Support)
        {
            if (entriesSeen.Contains(Support)) return false; // Cyclic dependencies
            entriesSeen.Add(Support);
            bool requireAll = AlwaysUseAny ? Support.RawSupport.Logic == AdmxSupportLogicType.AllOf : false;
            // It's much faster to check for plain products, so do that first
            foreach (var supElem in Support.Elements.Where(e => e.SupportDefinition is null))
            {
                bool isMet = supEntryMet(supElem);
                if (requireAll)
                {
                    if (!isMet) return false;
                }
                else
                {
                    if (isMet) return true;
                }
            }
            foreach (var subDef in Support.Elements.Where(e => e.SupportDefinition is not null))
            {
                bool isMet = supDefMet(subDef.SupportDefinition);
                if (requireAll)
                {
                    if (!isMet) return false;
                }
                else
                {
                    if (isMet) return true;
                }
            }
            return requireAll; // If all were required and this function hasn't exited yet, all are matched
        }
        return supDefMet(Policy.SupportedOn);
    }
}

public enum PolicyState
{
    NotConfigured = 0,
    Disabled = 1,
    Enabled = 2,
    Unknown = 3
}

public class RegistryKeyValuePair : IEquatable<RegistryKeyValuePair>
{
    public string Key;
    public string Value;

    public bool Equals(RegistryKeyValuePair other)
    {
        if (other is null) return false;
        return other.Key.Equals(Key, StringComparison.InvariantCultureIgnoreCase) && other.Value.Equals(Value, StringComparison.InvariantCultureIgnoreCase);
    }

    public override bool Equals(object obj)
    {
        if (obj is not RegistryKeyValuePair other) return false;
        return Equals(other);
    }

    public override int GetHashCode()
    {
        return Key.ToLowerInvariant().GetHashCode() ^ Value.ToLowerInvariant().GetHashCode();
    }
}
