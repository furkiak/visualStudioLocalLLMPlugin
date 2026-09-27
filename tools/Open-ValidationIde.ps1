param(
    [Parameter(Mandatory=$true)][string]$ProfileDirectory,
    [string]$DevEnvPath = 'C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\devenv.exe',
    [string]$RootSuffix = 'CodexLLLM'
)
$ErrorActionPreference = 'Stop'
if (-not [IO.Path]::IsPathRooted($ProfileDirectory)) { throw 'Provide an absolute fixture profile directory.' }
if ($RootSuffix -notmatch '^[a-zA-Z0-9]+$' -or $RootSuffix -eq 'Exp') { throw 'Choose a dedicated named experimental root suffix.' }
$profilePath = (Resolve-Path -LiteralPath $ProfileDirectory).Path
$marker = Get-Content -LiteralPath (Join-Path $profilePath 'profile.json') -Raw | ConvertFrom-Json
if ($marker.Version -ne 1 -or $marker.Mode -ne 'LoopbackHttpFixture') { throw 'Unsupported fixture profile.' }
$oldValue = [Environment]::GetEnvironmentVariable('LLLM_VALIDATION_PROFILE', 'Process')
try {
    [Environment]::SetEnvironmentVariable('LLLM_VALIDATION_PROFILE', $profilePath, 'Process')
    Start-Process -FilePath (Resolve-Path -LiteralPath $DevEnvPath).Path -ArgumentList @('/RootSuffix', $RootSuffix) -PassThru
} finally {
    [Environment]::SetEnvironmentVariable('LLLM_VALIDATION_PROFILE', $oldValue, 'Process')
}
