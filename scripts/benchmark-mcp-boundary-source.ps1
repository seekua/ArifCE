function ConvertTo-BenchmarkMcpBoundarySource([string]$Source) {
    foreach ($marker in @('namespace ArifCE.Tests;', 'public sealed class BenchmarkMcpBoundaryTests', 'public BenchmarkMcpBoundaryTests()')) {
        if ([regex]::Matches($Source, [regex]::Escape($marker)).Count -ne 1) { throw 'Unexpected pinned MCP-boundary evaluator source shape.' }
    }
    return $Source.Replace('namespace ArifCE.Tests;', 'namespace ArifCE.IndependentEvaluator;').Replace('public sealed class BenchmarkMcpBoundaryTests', 'public sealed class IndependentTests').Replace('public BenchmarkMcpBoundaryTests()', 'public IndependentTests()')
}
