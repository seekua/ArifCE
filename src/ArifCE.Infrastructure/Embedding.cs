using System.Security.Cryptography;
using System.Text;

namespace ArifCE.Infrastructure;

public sealed record EmbeddingProfile(string Id, string Provider, int Dimensions = 128, bool Enabled = true);
public interface IEmbeddingProvider { string Id { get; } int Dimensions { get; } Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default); }

/// <summary>Offline lexical-semantic baseline: normalized token vectors make shared concepts comparable without a service.</summary>
public sealed class TokenEmbeddingProvider(EmbeddingProfile profile) : IEmbeddingProvider
{
    public string Id => profile.Id; public int Dimensions => profile.Dimensions;
    public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var vector = new float[Dimensions];
        foreach (var token in System.Text.RegularExpressions.Regex.Matches(text ?? string.Empty, "[A-Za-z0-9_]{2,}").Select(x => x.Value.ToLowerInvariant()).Distinct())
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            for (var i = 0; i < 4; i++) vector[(hash[i] + i * 31) % Dimensions] += 1f;
        }
        var norm = MathF.Sqrt(vector.Sum(x => x * x));
        if (norm > 0) for (var i = 0; i < vector.Length; i++) vector[i] /= norm;
        return Task.FromResult(vector);
    }
}

/// <summary>Deterministic hash vector for offline determinism tests. This is not a semantic embedding.</summary>
public class DeterministicHashEmbeddingProvider(EmbeddingProfile profile) : IEmbeddingProvider
{
    public string Id => profile.Id; public int Dimensions => profile.Dimensions;
    public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text ?? string.Empty));
        var vector = new float[Dimensions];
        for (var i = 0; i < vector.Length; i++) vector[i] = (bytes[i % bytes.Length] - 128) / 128f;
        return Task.FromResult(vector);
    }
}

/// <summary>Compatibility alias for the former test-only provider name.</summary>
[Obsolete("Use DeterministicHashEmbeddingProvider; this provider is not semantic.")]
public sealed class LocalEmbeddingProvider(EmbeddingProfile profile) : DeterministicHashEmbeddingProvider(profile);

public sealed class EmbeddingProviderSelector(IEnumerable<IEmbeddingProvider> providers)
{
    private readonly IReadOnlyList<IEmbeddingProvider> _providers = providers.ToArray();
    public IEmbeddingProvider Select(string? id = null) => _providers.FirstOrDefault(p => string.IsNullOrWhiteSpace(id) || p.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException("No matching embedding provider is configured.");
}
