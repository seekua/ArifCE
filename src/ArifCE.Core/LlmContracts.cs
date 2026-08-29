namespace ArifCE.Core;

public enum LlmProviderKind { OpenAI, Anthropic, Gemini, OpenRouter, Ollama, LmStudio }

public sealed record LlmProviderProfile(
    string Id,
    LlmProviderKind Provider,
    string Model,
    string? Endpoint,
    string? ApiKey,
    bool Enabled = true,
    decimal InputCostPerMillion = 0,
    decimal OutputCostPerMillion = 0);

public sealed record LlmRequest(string Task, string Prompt, string? Model = null, int MaxOutputTokens = 2048, decimal Temperature = 0.1m);
public sealed record LlmUsage(int? InputTokens, int? OutputTokens)
{
    public int TotalTokens => (InputTokens ?? 0) + (OutputTokens ?? 0);
}
public sealed record LlmResponse(string ProviderId, string Model, string Text, LlmUsage Usage, TimeSpan Latency, string RawResponse = "");
public sealed record LlmConnectionResult(string ProviderId, bool Success, string Message, TimeSpan Latency);

public interface ILlmProvider
{
    string ProviderId { get; }
    LlmProviderKind Kind { get; }
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default);
    Task<LlmConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default);
}
