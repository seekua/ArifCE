using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ArifCE.Core;

namespace ArifCE.Infrastructure;

public sealed class LocalLlmSettingsStore
{
    private readonly string _path;
    public LocalLlmSettingsStore(string? path = null) => _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArifCE", "llm-providers.json");
    public async Task<IReadOnlyList<LlmProviderProfile>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path)) return [];
        await using var stream = File.OpenRead(_path);
        return await JsonSerializer.DeserializeAsync<List<LlmProviderProfile>>(stream, JsonDefaults.Options, cancellationToken) ?? [];
    }
    public async Task UpsertAsync(LlmProviderProfile profile, CancellationToken cancellationToken = default)
    {
        var errors = LlmProviderValidation.Validate(profile);
        if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors));
        var items = (await ListAsync(cancellationToken)).Where(x => !string.Equals(x.Id, profile.Id, StringComparison.OrdinalIgnoreCase)).Append(profile).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(items, JsonDefaults.Options), new UTF8Encoding(false), cancellationToken);
    }
    public async Task RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        var items = (await ListAsync(cancellationToken)).Where(x => !string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase)).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(items, JsonDefaults.Options), new UTF8Encoding(false), cancellationToken);
    }
}

public static class LlmProviderFactory
{
    public static ILlmProvider Create(LlmProviderProfile profile, HttpClient? client = null) => profile.Provider switch
    {
        LlmProviderKind.Anthropic => new AnthropicProvider(profile, client ?? new HttpClient()),
        LlmProviderKind.Gemini => new GeminiProvider(profile, client ?? new HttpClient()),
        _ => new OpenAiCompatibleProvider(profile, client ?? new HttpClient())
    };
}

public sealed class OpenAiCompatibleProvider : ILlmProvider
{
    private readonly LlmProviderProfile _profile; private readonly HttpClient _client;
    public OpenAiCompatibleProvider(LlmProviderProfile profile, HttpClient client) { _profile = profile; _client = client; }
    public string ProviderId => _profile.Id; public LlmProviderKind Kind => _profile.Provider;
    private string BaseUrl => (_profile.Endpoint ?? _profile.Provider switch { LlmProviderKind.Ollama => "http://127.0.0.1:11434", LlmProviderKind.LmStudio => "http://127.0.0.1:1234/v1", LlmProviderKind.OpenRouter => "https://openrouter.ai/api/v1", _ => "https://api.openai.com/v1" }).TrimEnd('/');
    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "/chat/completions"); AddAuth(message);
        message.Content = JsonContent.Create(new { model = request.Model ?? _profile.Model, messages = new[] { new { role = "user", content = request.Prompt } }, max_tokens = request.MaxOutputTokens, temperature = request.Temperature });
        var watch = Stopwatch.StartNew(); using var response = await _client.SendAsync(message, cancellationToken); var raw = await response.Content.ReadAsStringAsync(cancellationToken); response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(raw); var root = doc.RootElement; var text = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? ""; var usage = ParseUsage(root); return new(_profile.Id, request.Model ?? _profile.Model, text, usage, watch.Elapsed, raw);
    }
    public async Task<LlmConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    { var watch=Stopwatch.StartNew(); try { using var req=new HttpRequestMessage(HttpMethod.Get,BaseUrl+"/models"); AddAuth(req); using var res=await _client.SendAsync(req,cancellationToken); var msg=res.IsSuccessStatusCode?"Connection succeeded":$"Provider returned {(int)res.StatusCode}"; return new(_profile.Id,res.IsSuccessStatusCode,msg,watch.Elapsed); } catch(Exception ex){return new(_profile.Id,false,ex.Message,watch.Elapsed);} }
    private void AddAuth(HttpRequestMessage request) { if (!string.IsNullOrWhiteSpace(_profile.ApiKey)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _profile.ApiKey); }
    private static LlmUsage ParseUsage(JsonElement root) => root.TryGetProperty("usage", out var u) ? new(u.TryGetProperty("prompt_tokens", out var i) ? i.GetInt32() : null, u.TryGetProperty("completion_tokens", out var o) ? o.GetInt32() : null) : new(null, null);
}

public sealed class AnthropicProvider : ILlmProvider
{
    private readonly LlmProviderProfile _profile; private readonly HttpClient _client;
    public AnthropicProvider(LlmProviderProfile profile, HttpClient client) { _profile=profile; _client=client; }
    public string ProviderId=>_profile.Id; public LlmProviderKind Kind=>LlmProviderKind.Anthropic;
    public async Task<LlmResponse> CompleteAsync(LlmRequest request,CancellationToken cancellationToken=default){var url=(_profile.Endpoint??"https://api.anthropic.com/v1").TrimEnd('/')+"/messages";using var m=new HttpRequestMessage(HttpMethod.Post,url);m.Headers.Add("x-api-key",_profile.ApiKey??"");m.Headers.Add("anthropic-version","2023-06-01");m.Content=JsonContent.Create(new{model=request.Model??_profile.Model,max_tokens=request.MaxOutputTokens,messages=new[]{new{role="user",content=request.Prompt}}});var w=Stopwatch.StartNew();using var r=await _client.SendAsync(m,cancellationToken);var raw=await r.Content.ReadAsStringAsync(cancellationToken);r.EnsureSuccessStatusCode();using var d=JsonDocument.Parse(raw);var root=d.RootElement;var text=root.GetProperty("content")[0].GetProperty("text").GetString()??"";var usage=root.TryGetProperty("usage",out var u)?new LlmUsage(u.TryGetProperty("input_tokens",out var i)?i.GetInt32():null,u.TryGetProperty("output_tokens",out var o)?o.GetInt32():null):new(null,null);return new(_profile.Id,request.Model??_profile.Model,text,usage,w.Elapsed,raw);}
    public async Task<LlmConnectionResult> TestConnectionAsync(CancellationToken cancellationToken=default){var w=Stopwatch.StartNew();try{using var m=new HttpRequestMessage(HttpMethod.Get,(_profile.Endpoint??"https://api.anthropic.com/v1").TrimEnd('/')+"/models");m.Headers.Add("x-api-key",_profile.ApiKey??"");m.Headers.Add("anthropic-version","2023-06-01");using var r=await _client.SendAsync(m,cancellationToken);return new(_profile.Id,r.IsSuccessStatusCode,r.IsSuccessStatusCode?"Connection succeeded":$"Provider returned {(int)r.StatusCode}",w.Elapsed);}catch(Exception ex){return new(_profile.Id,false,ex.Message,w.Elapsed);}}
}

public sealed class GeminiProvider : ILlmProvider
{
    private readonly LlmProviderProfile _profile; private readonly HttpClient _client;
    public GeminiProvider(LlmProviderProfile profile,HttpClient client){_profile=profile;_client=client;} public string ProviderId=>_profile.Id; public LlmProviderKind Kind=>LlmProviderKind.Gemini;
    public async Task<LlmResponse> CompleteAsync(LlmRequest request,CancellationToken cancellationToken=default){var model=request.Model??_profile.Model;var endpoint=(_profile.Endpoint??"https://generativelanguage.googleapis.com/v1beta").TrimEnd('/')+"/models/"+model+":generateContent?key="+Uri.EscapeDataString(_profile.ApiKey??"");using var m=new HttpRequestMessage(HttpMethod.Post,endpoint){Content=JsonContent.Create(new{contents=new[]{new{parts=new[]{new{text=request.Prompt}}}}})};var w=Stopwatch.StartNew();using var r=await _client.SendAsync(m,cancellationToken);var raw=await r.Content.ReadAsStringAsync(cancellationToken);r.EnsureSuccessStatusCode();using var d=JsonDocument.Parse(raw);var root=d.RootElement;var text=root.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString()??"";var usage=root.TryGetProperty("usageMetadata",out var u)?new LlmUsage(u.TryGetProperty("promptTokenCount",out var i)?i.GetInt32():null,u.TryGetProperty("candidatesTokenCount",out var o)?o.GetInt32():null):new(null,null);return new(_profile.Id,model,text,usage,w.Elapsed,raw);}
    public async Task<LlmConnectionResult> TestConnectionAsync(CancellationToken cancellationToken=default){var w=Stopwatch.StartNew();try{var result=await CompleteAsync(new("connection-test","Reply with OK",_profile.Model,16),cancellationToken);return new(_profile.Id,true,"Connection succeeded",w.Elapsed);}catch(Exception ex){return new(_profile.Id,false,ex.Message,w.Elapsed);}}
}
