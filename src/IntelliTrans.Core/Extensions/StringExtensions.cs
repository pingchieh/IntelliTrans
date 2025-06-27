using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace IntelliTrans.Core.Extensions;

public static class StringExtensions
{
    /// <summary>
    /// 确定指定的字符串是否包含中文字符。
    /// </summary>
    public static bool ContainsChinese(this string text)
    {
        return Regex.IsMatch(text, @"[\u4e00-\u9fa5]");
    }

    /// <summary>
    /// 确定指定的字符串是否为 null 或为空字符串 ("")。
    /// </summary>
    public static bool IsNullOrEmpty(this string? text)
    {
        return string.IsNullOrEmpty(text);
    }

    /// <summary>
    /// 确定指定的字符串是否为 null、空或仅包含空白字符。
    /// </summary>
    public static bool IsNullOrWhiteSpace(this string? text)
    {
        return string.IsNullOrWhiteSpace(text);
    }

    /// <summary>
    /// 计算字符串的MD5值
    /// </summary>
    /// <param name="input"> 需要计算的字符串 </param>
    /// <returns> MD5值 </returns>
    public static string CalculateMd5(this string input)
    {
        return MD5.HashData(Encoding.UTF8.GetBytes(input))
            .Select(c => c.ToString("x2"))
            .Aggregate((a, b) => a + b);
    }

    /// <summary>
    /// 使用指定的替换字符串替换字符串中与正则表达式匹配的所有匹配项。
    /// </summary>
    /// <param name="input">要执行替换的输入字符串。</param>
    /// <param name="pattern">要匹配的正则表达式模式。</param>
    /// <param name="replacement">替换匹配项的字符串。</param>
    /// <returns>一个新字符串，除了已替换的匹配项之外，该字符串与原始输入字符串相同。</returns>
    public static string RegexReplace(this string input, string pattern, string replacement)
    {
        return Regex.Replace(input, pattern, replacement);
    }

    /// <summary>
    /// 替换字符串中连续的空白字符为一个指定的替换字符串。
    /// </summary>
    /// <param name="input">要处理的输入字符串。</param>
    /// <param name="replacement">用于替换连续空白字符的字符串，默认为单个空格。</param>
    /// <returns>替换了连续空白字符的字符串。</returns>
    public static string ReplacExtraSpaces(this string input, string replacement = " ")
    {
        return Regex.Replace(input, @"\s+", replacement);
    }

    /// <summary>
    /// 确定指定的输入字符串是否与指定的正则表达式模式匹配。
    /// </summary>
    /// <param name="input">要搜索匹配项的字符串。</param>
    /// <param name="pattern">要匹配的正则表达式模式。</param>
    /// <returns>如果正则表达式模式找到匹配项，则为 <c>true</c>；否则为 <c>false</c>。</returns>
    public static bool IsRegexMatch(this string input, string pattern)
    {
        return Regex.IsMatch(input, pattern);
    }
}
