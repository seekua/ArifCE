using System.Security.Cryptography;
using System.Text;

namespace ArifCE.Infrastructure;

public sealed record EmbeddingProfile(string Id, string Provider, int Dimensions = 128, bool Enabled = true);
public interface IEmbeddingProvider { string Id { get; } int Dimensions { get; } Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default); }

/// <summary>Deterministic local embedding for offline ranking and tests; no hosted service required.</summary>
public sealed class LocalEmbeddingProvider(EmbeddingProfile profile) : IEmbeddingProvider
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

public sealed class EmbeddingProviderSelector(IEnumerable<IEmbeddingProvider> providers)
{
    private readonly IReadOnlyList<IEmbeddingProvider> _providers = providers.ToArray();
    public IEmbeddingProvider Select(string? id = null) => _providers.FirstOrDefault(p => string.IsNullOrWhiteSpace(id) || p.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException("No matching embedding provider is configured.");
}
