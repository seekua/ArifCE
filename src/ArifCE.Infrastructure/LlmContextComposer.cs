using System.Text;
using System.Text.RegularExpressions;
using ArifCE.Core;

namespace ArifCE.Infrastructure;

public sealed record LlmContext(string Task, string Content, int EstimatedTokens, IReadOnlyList<string> Sources);

public sealed class LlmContextComposer(IndexStore index)
{
    public async Task<LlmContext> ComposeAsync(string root, string task, int budget = 4000, CancellationToken cancellationToken = default)
    {
        if (budget <= 0) throw new ArgumentOutOfRangeException(nameof(budget));
        var terms = Regex.Matches(task ?? string.Empty, "[A-Za-z0-9_]+", RegexOptions.CultureInvariant).Select(m => m.Value).Distinct(StringComparer.OrdinalIgnoreCase).Take(16).ToArray();
        if (terms.Length == 0) throw new ArgumentException("Context task must contain searchable terms.", nameof(task));
        var hits = await index.SearchAsync(root, string.Join(" OR ", terms.Select(t => $"\"{t}\"")), 50, cancellationToken);
        var builder = new StringBuilder(); var sources = new List<string>(); var used = 0;
        foreach (var hit in hits)
        {
            var estimate = Math.Max(1, (int)Math.Ceiling(hit.Snippet.Length / 4d));
            if (used + estimate > budget) continue;
            builder.AppendLine($"[{hit.Path}]\n{hit.Snippet}\n"); sources.Add(hit.Path); used += estimate;
        }
        return new(task ?? string.Empty, builder.ToString().Trim(), used, sources);
    }
}
