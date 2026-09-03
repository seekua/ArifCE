function ConvertTo-BenchmarkSafetySource([string]$Source) {
    foreach ($marker in @('namespace ArifCE.Tests;', 'public sealed class BenchmarkSafetyTests')) {
        if ([regex]::Matches($Source, [regex]::Escape($marker)).Count -ne 1) { throw 'Unexpected pinned safety evaluator source shape.' }
    }
    return $Source.Replace('namespace ArifCE.Tests;', 'namespace ArifCE.IndependentEvaluator;').Replace('public sealed class BenchmarkSafetyTests', 'public sealed class IndependentTests')
}
