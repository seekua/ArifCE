using System.Text;

namespace ArifCE.Infrastructure;

public enum VerificationCommandKind { NamedDotNet, UnsafeShell }

public static class VerificationCommandPolicy
{
    private static readonly char[] ShellMetacharacters = ['&', '|', ';', '>', '<', '`', '\r', '\n', '\0'];

    public static VerificationCommandKind Classify(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) throw new ArgumentException("Verification command must not be blank.", nameof(command));
        if (command.IndexOfAny(ShellMetacharacters) >= 0 || command.Contains("$(", StringComparison.Ordinal) || command.Contains("${", StringComparison.Ordinal)) return VerificationCommandKind.UnsafeShell;
        var tokens = Tokenize(command);
        return tokens.Count >= 2 && tokens[0].Equals("dotnet", StringComparison.OrdinalIgnoreCase) && (tokens[1].Equals("build", StringComparison.OrdinalIgnoreCase) || tokens[1].Equals("test", StringComparison.OrdinalIgnoreCase))
            ? VerificationCommandKind.NamedDotNet
            : VerificationCommandKind.UnsafeShell;
    }

    public static IReadOnlyList<string> Tokenize(string command)
    {
        var tokens = new List<string>(); var current = new StringBuilder(); var quote = '\0';
        foreach (var character in command.Trim())
        {
            if (quote != '\0')
            {
                if (character == quote) quote = '\0'; else current.Append(character);
                continue;
            }
            if (character is '\'' or '"') { quote = character; continue; }
            if (char.IsWhiteSpace(character))
            {
                if (current.Length > 0) { tokens.Add(current.ToString()); current.Clear(); }
                continue;
            }
            current.Append(character);
        }
        if (quote != '\0') throw new ArgumentException("Verification command contains an unterminated quote.", nameof(command));
        if (current.Length > 0) tokens.Add(current.ToString());
        if (tokens.Count == 0) throw new ArgumentException("Verification command must not be blank.", nameof(command));
        return tokens;
    }
}
