using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Web.Services;

public static class LogSanitizer
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        MaxDepth = 8
    };

    private static readonly Regex SensitiveKeyRegex = new(
        @"""(""(password|pwd|secret|token|accessToken|refreshToken|authorization|apiKey|apikey|key)""\s*:\s*""[^""]*"")""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string ToSafeJson(object? obj, int maxLength = 2048)
    {
        if (obj is null) return "null";
        string json;
        try
        {
            json = JsonSerializer.Serialize(obj, JsonOpts);
        }
        catch
        {
            // fall back to ToString()
            json = obj.ToString() ?? obj.GetType().FullName ?? "<unknown>";
        }

        // compact whitespace to keep logs short
        json = CompactWhitespace(json);
        // mask sensitive values
        json = SensitiveKeyRegex.Replace(json, m =>
        {
            var key = m.Value;
            var idx = key.IndexOf(':');
            return idx > 0 ? key[..(idx + 1)] + "\"***\"" : "\"***\"";
        });

        // hard truncate large payloads
        if (json.Length > maxLength)
            json = json[..maxLength] + "... [truncated]";

        return json;
    }

    public static string CompactWhitespace(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        // Replace multiple whitespace (including newlines and tabs) with a single space
        return Regex.Replace(input, @"\s+", " ");
    }
}

