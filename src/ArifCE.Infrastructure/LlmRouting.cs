using ArifCE.Core;

namespace ArifCE.Infrastructure;

public sealed record LlmRouteResult(LlmResponse Response, IReadOnlyList<string> AttemptedProviders, decimal EstimatedCost);

public sealed class LlmRouter
{
    private readonly IReadOnlyList<ILlmProvider> _providers;
    private readonly IReadOnlyDictionary<string, LlmProviderProfile> _profiles;
    public LlmRouter(IEnumerable<(ILlmProvider Provider, LlmProviderProfile Profile)> providers)
    {
        var list = providers.ToList();
        _providers = list.Select(x => x.Provider).ToArray();
        _profiles = list.ToDictionary(x => x.Provider.ProviderId, x => x.Profile, StringComparer.OrdinalIgnoreCase);
    }
    public async Task<LlmRouteResult> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        var attempts = new List<string>(); Exception? last = null;
        foreach (var provider in _providers)
        {
            attempts.Add(provider.ProviderId);
            try
            {
                var response = await provider.CompleteAsync(request, cancellationToken);
                var profile = _profiles[provider.ProviderId];
                var cost = (response.Usage.InputTokens ?? 0) / 1_000_000m * profile.InputCostPerMillion + (response.Usage.OutputTokens ?? 0) / 1_000_000m * profile.OutputCostPerMillion;
                return new(response, attempts, cost);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { last = ex; }
        }
        throw new InvalidOperationException($"All configured LLM providers failed after trying {attempts.Count} provider(s).", last);
    }
}
