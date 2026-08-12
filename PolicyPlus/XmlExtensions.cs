using System.ComponentModel;
using System.Xml;

// Convenience methods for parsing XML in AdmxFile and AdmlFile
public static class XmlExtensions
{
    public static string AttributeOrNull(this XmlNode Node, string Attribute)
    {
        return Node.Attributes[Attribute] is null ? null : Node.Attributes[Attribute].Value;
    }

    public static object AttributeOrDefault(this XmlNode Node, string Attribute, object DefaultVal)
    {
        if (Node.Attributes[Attribute] is null) return DefaultVal;
        TypeConverter converter = TypeDescriptor.GetConverter(DefaultVal.GetType());
        if (converter.IsValid(Node.Attributes[Attribute].Value))
        {
            return converter.ConvertFromString(Node.Attributes[Attribute].Value);
        }
        return DefaultVal;
    }
}
