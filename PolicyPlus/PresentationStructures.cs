using System.Collections.Generic;

// These structures hold information on how the UI for policy elements appears
public class Presentation
{
    public string Name;
    public List<PresentationElement> Elements = new();
}

public abstract class PresentationElement
{
    public string ID; // refId
    public string ElementType;
}

// <text>
public class LabelPresentationElement : PresentationElement
{
    public string Text; // Inner text
}

// <decimalTextBox>
public class NumericBoxPresentationElement : PresentationElement
{
    public uint DefaultValue; // defaultValue
    public bool HasSpinner = true; // spin
    public uint SpinnerIncrement; // spinStep
    public string Label; // Inner text
}

// <textBox>
public class TextBoxPresentationElement : PresentationElement
{
    public string Label; // <label>
    public string DefaultValue; // <defaultValue>
}

// <checkBox>
public class CheckBoxPresentationElement : PresentationElement
{
    public bool DefaultState; // defaultChecked
    public string Text; // Inner text
}

// <comboBox>
public class ComboBoxPresentationElement : PresentationElement
{
    public bool NoSort; // noSort
    public string Label; // <label>
    public string DefaultText; // <default>
    public List<string> Suggestions = new(); // <suggestion>s
}

// <dropdownList>
public class DropDownPresentationElement : PresentationElement
{
    public bool NoSort; // noSort
    public int? DefaultItemID; // defaultItem
    public string Label; // Inner text
}

// <listBox>
public class ListPresentationElement : PresentationElement
{
    public string Label; // Inner text
}

// <multiTextBox>
public class MultiTextPresentationElement : PresentationElement
{
    public string Label; // Inner text
    // Undocumented, but never appears to have any other parameters
}
