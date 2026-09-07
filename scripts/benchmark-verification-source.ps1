function ConvertTo-BenchmarkVerificationSource([string]$Source) {
    foreach ($marker in @('namespace ArifCE.Tests;', 'public sealed class BenchmarkVerificationTests', 'public BenchmarkVerificationTests()')) {
        if ([regex]::Matches($Source, [regex]::Escape($marker)).Count -ne 1) { throw 'Unexpected pinned verification evaluator source shape.' }
    }
    return $Source.Replace('namespace ArifCE.Tests;', 'namespace ArifCE.IndependentEvaluator;').Replace('public sealed class BenchmarkVerificationTests', 'public sealed class IndependentTests').Replace('public BenchmarkVerificationTests()', 'public IndependentTests()')
}
