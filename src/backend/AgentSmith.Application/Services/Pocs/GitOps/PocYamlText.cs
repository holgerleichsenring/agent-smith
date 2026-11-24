using System.Globalization;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>
/// 2026-11-23-c81eb: the scalars a render writes. A string whose plain form a YAML reader would take for anything but a
/// string (true, yes, on, null, ~, 8080, 1e3, 0x1F, .inf, a date, ...) is double-quoted, because Kubernetes refuses a boolean or a
/// number in a string field; a number is written plain, because it refuses a quoted one in an integer field.
/// </summary>
public static partial class PocYamlText
{
    public static YamlScalarNode Text(string value) =>
        new(value) { Style = ReadsAsNonString(value) ? ScalarStyle.DoubleQuoted : ScalarStyle.Any };

    public static YamlScalarNode Number(int value) =>
        new(value.ToString(CultureInfo.InvariantCulture)) { Style = ScalarStyle.Plain };

    /// <summary>Whether a plain scalar of this text resolves as a boolean, null or number under YAML 1.1 or 1.2.</summary>
    public static bool ReadsAsNonString(string value) => value.Length == 0 || NonString().IsMatch(value);

    /// <summary>A plain scalar in a field that must hold a string, double-quoted when it reads as anything else.</summary>
    public static void Requote(YamlNode node)
    {
        if (node is YamlScalarNode { Style: ScalarStyle.Plain or ScalarStyle.Any, Value: { } value } scalar && ReadsAsNonString(value))
            scalar.Style = ScalarStyle.DoubleQuoted;
    }

    [GeneratedRegex(@"^(y|Y|yes|Yes|YES|n|N|no|No|NO|true|True|TRUE|false|False|FALSE|on|On|ON|off|Off|OFF|null|Null|NULL|~"
        + @"|[-+]?[0-9][0-9_]*(\.[0-9_]*)?([eE][-+]?[0-9]+)?|[-+]?\.[0-9]+([eE][-+]?[0-9]+)?|0x[0-9a-fA-F_]+|0o[0-7]+|0b[01_]+"
        + @"|[-+]?[0-9][0-9_]*(:[0-5]?[0-9])+(\.[0-9_]*)?|[-+]?\.(inf|Inf|INF)|\.(nan|NaN|NAN)|[0-9]{4}-[0-9]{1,2}-[0-9]{1,2}([Tt ].*)?)$")]
    private static partial Regex NonString();
}
