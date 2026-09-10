using ArifCE.Infrastructure;

public static class BenchmarkApiContract
{
    public static void Probe()
    {
        VerificationCommandKind kind = VerificationCommandPolicy.Classify("dotnet test --no-restore");
        _ = (kind, VerificationCommandKind.NamedDotNet, VerificationCommandKind.UnsafeShell);
    }
}
