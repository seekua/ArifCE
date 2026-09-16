function Get-CodexBenchmarkHostArguments(
    [Parameter(Mandatory)][string]$Model,
    [Parameter(Mandatory)][ValidateSet('low','medium','high','xhigh','max','ultra')][string]$Reasoning,
    [Parameter(Mandatory)][string]$PermissionProfile,
    [bool]$WindowsHost = $IsWindows
) {
    if ($PermissionProfile -cne 'preauthorized-write-build-v1') {
        throw "Unsupported Codex benchmark permission profile: $PermissionProfile"
    }

    $arguments = [System.Collections.Generic.List[string]]::new()
    foreach ($value in @(
        'exec', '--json', '--ephemeral', '--ignore-user-config', '--ignore-rules',
        '--model', $Model,
        '-c', ('model_reasoning_effort="' + $Reasoning + '"')
    )) { $arguments.Add($value) }

    # Auto-review uses the workspace-write sandbox. On Windows, pin the
    # restricted-token implementation explicitly because --ignore-user-config
    # otherwise discards the host's working [windows] sandbox selection. The
    # default fs helper has produced false "path contains a reparse point"
    # failures for ordinary files on affected Codex Desktop installations.
    if ($WindowsHost) {
        $arguments.Add('-c')
        $arguments.Add('windows.sandbox="unelevated"')
    }

    $arguments.Add('--approve-for-me')
    $arguments.Add('-')
    return $arguments.ToArray()
}
