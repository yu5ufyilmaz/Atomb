using System;
using System.Text.RegularExpressions;

// Independent of Unity so formatting and token preservation can be checked offline.
public static class InputHintText
{
    static readonly Regex Token = new Regex(@"\{input:([A-Za-z][A-Za-z0-9_]*)\}");
    public static string Resolve(string text, Func<string, string> bindingName)
    {
        return string.IsNullOrEmpty(text) ? text : Token.Replace(text, m => bindingName(m.Groups[1].Value));
    }
    public static string EscapeForFormat(string text)
    {
        return string.IsNullOrEmpty(text) ? text : Token.Replace(text, m => "{" + m.Value + "}");
    }
}
