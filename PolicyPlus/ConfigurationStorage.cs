using Microsoft.Win32;

public class ConfigurationStorage
{
    private readonly RegistryKey ConfigKey;

    public ConfigurationStorage(RegistryHive RegistryBase, string Subkey)
    {
        try
        {
            ConfigKey = RegistryKey.OpenBaseKey(RegistryBase, RegistryView.Default).CreateSubKey(Subkey);
        }
        catch
        {
            // The key couldn't be created
        }
    }

    public object GetValue(string ValueName, object DefaultValue)
    {
        return ConfigKey is not null ? ConfigKey.GetValue(ValueName, DefaultValue) : DefaultValue;
    }

    public void SetValue(string ValueName, object Data)
    {
        ConfigKey?.SetValue(ValueName, Data);
    }

    public bool HasValue(string ValueName)
    {
        return ConfigKey is not null && ConfigKey.GetValue(ValueName) is not null;
    }
}
