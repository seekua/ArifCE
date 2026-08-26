using System.Text.RegularExpressions;
using ArifCE.Core;

namespace ArifCE.Infrastructure;

public static partial class CommandEvidenceParser
{
    public static (string Kind, EvidenceMetrics? Metrics) Parse(string command, string output)
    {
        if (command.Contains("dotnet test", StringComparison.OrdinalIgnoreCase))
        {
            var match = EnglishTestSummary().Match(output);
            if (!match.Success) match = TurkishTestSummary().Match(output);
            return ("TEST_RUN", match.Success
                ? new EvidenceMetrics(Value(match, "total"), Value(match, "passed"), Value(match, "failed"), Value(match, "skipped"))
                : null);
        }

        if (command.Contains("dotnet build", StringComparison.OrdinalIgnoreCase))
        {
            var match = EnglishBuildSummary().Match(output);
            if (!match.Success) match = TurkishBuildSummary().Match(output);
            return ("BUILD", match.Success
                ? new EvidenceMetrics(null, null, null, null, Value(match, "warnings"), Value(match, "errors"))
                : null);
        }

        return ("COMMAND", null);
    }

    private static int Value(Match match, string group) => int.Parse(match.Groups[group].Value, System.Globalization.CultureInfo.InvariantCulture);

    [GeneratedRegex(@"Failed:\s*(?<failed>\d+),\s*Passed:\s*(?<passed>\d+),\s*Skipped:\s*(?<skipped>\d+),\s*Total:\s*(?<total>\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EnglishTestSummary();

    [GeneratedRegex(@"Başarısız:\s*(?<failed>\d+),\s*Başarılı:\s*(?<passed>\d+),\s*Atlanan:\s*(?<skipped>\d+),\s*Toplam:\s*(?<total>\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TurkishTestSummary();

    [GeneratedRegex(@"(?<warnings>\d+)\s+Warning\(s\).*?(?<errors>\d+)\s+Error\(s\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex EnglishBuildSummary();

    [GeneratedRegex(@"(?<warnings>\d+)\s+Uyarı.*?(?<errors>\d+)\s+Hata", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex TurkishBuildSummary();
}
