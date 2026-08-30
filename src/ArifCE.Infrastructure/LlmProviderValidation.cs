using ArifCE.Core;

namespace ArifCE.Infrastructure;

public static class LlmProviderValidation
{
    public static IReadOnlyList<string> Validate(LlmProviderProfile profile)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(profile.Id)) errors.Add("Provider id is required.");
        if (string.IsNullOrWhiteSpace(profile.Model)) errors.Add("Provider model is required.");
        if (!Enum.IsDefined(profile.Provider)) errors.Add("Provider kind is invalid.");
        if (profile.InputCostPerMillion < 0 || profile.OutputCostPerMillion < 0) errors.Add("Provider costs cannot be negative.");
        if ((profile.Provider is LlmProviderKind.OpenAI or LlmProviderKind.Anthropic or LlmProviderKind.Gemini or LlmProviderKind.OpenRouter) && string.IsNullOrWhiteSpace(profile.ApiKey)) errors.Add("A cloud provider requires an API key.");
        return errors;
    }
}
