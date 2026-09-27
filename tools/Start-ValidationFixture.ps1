param(
    [string]$FixtureExe = (Join-Path $PSScriptRoot 'LoopbackFixture\bin\Release\net472\LoopbackFixture.exe'),
    [string]$ArtifactDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'TestResults'),
    [string]$ProfileName = ('native-http-' + [Guid]::NewGuid().ToString('N'))
)
$ErrorActionPreference = 'Stop'
if ($ProfileName -notmatch '^native-http-[a-zA-Z0-9-]+$') { throw 'Use an owned native-http-* profile name.' }
$artifactRoot = [IO.Path]::GetFullPath($ArtifactDirectory)
$exePath = (Resolve-Path -LiteralPath $FixtureExe).Path
New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
$profilePath = Join-Path $artifactRoot $ProfileName
if (Test-Path -LiteralPath $profilePath) { throw 'Profile already exists; choose a new name.' }
$stdout = Join-Path $artifactRoot ($ProfileName + '-stdout.txt')
$stderr = Join-Path $artifactRoot ($ProfileName + '-stderr.txt')
$process = Start-Process -FilePath $exePath -ArgumentList @('--profile-directory', ('"' + $profilePath + '"')) -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
$markerPath = Join-Path $profilePath 'profile.json'
$waitUntil = [DateTime]::UtcNow.AddSeconds(10)
while (-not (Test-Path -LiteralPath $markerPath)) {
    if ($process.HasExited -or [DateTime]::UtcNow -gt $waitUntil) { throw "Fixture startup did not produce its marker. Inspect $stderr" }
    Start-Sleep -Milliseconds 100
}
$marker = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
[pscustomobject]@{
    ProfileDirectory = $profilePath
    FixturePid = $process.Id
    Endpoint = $marker.Endpoint
    StopFile = (Join-Path $profilePath 'stop.fixture')
    HttpEventLog = (Join-Path $profilePath 'http-events.ndjson')
    LaunchScript = (Join-Path $PSScriptRoot 'Open-ValidationIde.ps1')
    Notice = 'Synthetic loopback HTTP fixture, no model. Use only a disposable IDE document.'
}
