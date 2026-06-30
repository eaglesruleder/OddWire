namespace OddWire.System;

public static class StringExtensions
{
    public static string EndWith(this string str, char suffix)
    {
        if (string.IsNullOrEmpty(str))
            return str;
        return str[^1] == suffix ? str : str + suffix;
    }
}
