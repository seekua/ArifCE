using System.Text.Json;
using ArifCE.Core;

namespace ArifCE.Infrastructure;

/// <summary>Opt-in, local-only adapter for agent lifecycle hooks. It records metadata only.</summary>
public sealed record AgentHookPayload(string Provider, string Event, string? Agent = null, string? EntityId = null, string? Summary = null, int? ExitCode = null);

public sealed class AgentHookRecorder(JournalStore journal, SecretRedactor? redactor = null)
{
    private static readonly HashSet<string> Providers = new(StringComparer.OrdinalIgnoreCase) { "claude", "codex", "opencode" };
    private static readonly HashSet<string> Events = new(StringComparer.OrdinalIgnoreCase) { "session.start", "session.end", "command.completed", "handoff.reported" };
    private readonly SecretRedactor _redactor = redactor ?? new();

    public async Task<JournalEvent> RecordAsync(string root, AgentHookPayload payload, CancellationToken cancellationToken = default)
    {
        if (!Providers.Contains(payload.Provider)) throw new ArgumentException("Unsupported hook provider.");
        if (!Events.Contains(payload.Event)) throw new ArgumentException("Unsupported hook event.");
        var redacted = payload.Summary is null ? null : _redactor.Redact(payload.Summary).Text;
        var summary = redacted?[..Math.Min(1000, redacted.Length)];
        var entityId = string.IsNullOrWhiteSpace(payload.EntityId) ? "SESSION" : payload.EntityId.Trim();
        var value = new { provider = payload.Provider.ToLowerInvariant(), agent = payload.Agent, exitCode = payload.ExitCode, summary };
        var entry = new JournalEvent(1, Guid.NewGuid().ToString("N"), $"agent.{payload.Event}", DateTimeOffset.UtcNow, entityId, value);
        await journal.AppendAsync(root, entry, cancellationToken);
        return entry;
    }

    public static AgentHookPayload Parse(string json)
    {
        var payload = JsonSerializer.Deserialize<AgentHookPayload>(json, JsonDefaults.Options) ?? throw new ArgumentException("Hook payload is empty.");
        return payload;
    }
}
