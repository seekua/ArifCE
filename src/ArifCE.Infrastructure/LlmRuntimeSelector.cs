using ArifCE.Core;

namespace ArifCE.Infrastructure;

public static class LlmRuntimeSelector
{
    public static IReadOnlyList<LlmProviderProfile> Select(IEnumerable<LlmProviderProfile> profiles, LlmRuntimeMode mode) => mode == LlmRuntimeMode.Any ? profiles.Where(p => p.Enabled).ToArray() : profiles.Where(p => p.Enabled && p.RuntimeMode == mode).ToArray();
}
