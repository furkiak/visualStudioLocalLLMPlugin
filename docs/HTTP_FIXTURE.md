# Isolated IDE HTTP protocol validation

This fixture is a **synthetic HTTP server, not Ollama and not a language model**. It exercises the production `LlmApiServiceFactory` → `OllamaApiService` → `HttpLlmApiService` → `MainViewModel` path. It does not replace a provider with fake answers inside the extension. Normal startup has no automatic fixture fallback.

Only the disposable Visual Studio process receives `LLLM_VALIDATION_PROFILE=<absolute fixture directory>`. Bootstrapper constructs the separate validation settings service, never the normal AppData settings service. This profile does not read/migrate/decrypt normal API keys and cannot save API-key or DPAPI fields. Settings are written atomically only inside the explicit profile. A bad/missing marker stops extension initialization instead of falling back to personal settings. A bad profile settings file stays intact until the user explicitly saves replacements.

Main and Settings titles say **ISOLATED HTTP FIXTURE**, and the destination banner says synthetic response/no model. In Settings, provider stays Ollama, endpoint is read-only, API Key is disabled, and the model dropdown lists five scenarios. The profile only accepts the exact `127.0.0.1` HTTP endpoint from its marker; other recipients, providers and model identifiers are rejected. Native profile requests expire after **15 seconds**; the normal production timeout stays two minutes.

## Build and start the fixture

```powershell
dotnet build tools/LoopbackFixture/LoopbackFixture.csproj -c Release
$fixture = & ./tools/Start-ValidationFixture.ps1
$fixture
```

The fixture binds IPv4 loopback on an OS-assigned free port and creates a new GUID profile under ignored `TestResults`. Existing directories are rejected, never reset. Its process is hidden; stdout/stderr are saved beside the profile. The event log records only fixed scenario names, stages, UTC time and outgoing prompt byte count; it does not log selection, prompt, responses, headers or credentials. Authorization headers are rejected. Use only a disposable editor file. These protocol tests are not model-quality, real-Ollama latency or cloud-provider evidence.

## Install/open the dedicated IDE instance

Install the newly built VSIX into a dedicated experimental Visual Studio instance using the main validation workflow. The scripts do not install extensions or alter Visual Studio settings. Close an already running instance of the same root suffix first so the new process inherits the profile; do not close ordinary Visual Studio sessions for this check.

```powershell
& ./tools/Open-ValidationIde.ps1 -ProfileDirectory $fixture.ProfileDirectory -RootSuffix CodexLLLM
```

The launcher restores its own process environment immediately after launch. It does not write User/Machine environment variables. Opening the extension must show the fixture label before sending. If it does not, close that disposable window and check the process/profile setup; do not send using a normal profile.

## Native scenarios, without restarting Visual Studio

Select disposable text, open **Tools → Local LLM Plugin (Modern)**, then Settings. Select a scenario in the model dropdown and Save Settings. The main window refreshes the same isolated profile. No external file edits are required to switch scenarios.

| Model dropdown | Real wire behavior | Expected IDE behavior |
| --- | --- | --- |
| `fixture-stream` | Chunked NDJSON. The first line is split across HTTP chunks inside a UTF-8 character. First text flushes after about 80 ms; the final line follows 7 seconds later | Candidate shows `// Synthetic loopback fixture (no model) · Türkçe` before completion. Apply stays disabled during generation. Final candidate adds phase two and `int fixtureValue = 42;`; explicit review is still required |
| `fixture-401` | Actual HTTP 401 | Authentication status; empty Candidate; Apply disabled; busy clears |
| `fixture-malformed` | Valid first NDJSON line, then broken JSON after 7 seconds | Partial Candidate is visible while busy; failure clears it; invalid JSON status cannot be applied |
| `fixture-timeout` | Headers/first line, then blank NDJSON keepalive lines with no `done` for 30 seconds | At about 15 seconds timeout status; empty Candidate; Apply disabled; busy clears |
| `fixture-cancel` | Same unfinished real response | Press Cancel after the first text, within 15 seconds. Request cancelled status; empty Candidate; Apply disabled; window remains usable |

After every failure/cancel, switch to `fixture-stream` in Settings and send again. It must recover in the same window without restarting the IDE. Inspect Changes and Apply gating; use only the disposable selection if exercising Apply/Undo. The fixture server may record a connection ending after cancellation; exact TCP disconnect detection timing is not a model or network guarantee.

To stop the owned fixture:

```powershell
New-Item -ItemType File -Path $fixture.StopFile | Out-Null
```

The server cancels its owned sockets and exits. Keep profile/event files as evidence; no recursive cleanup is needed. Later profiles receive new directories and ports. Normal extension startup (without the process variable) keeps its existing settings/DPAPI behavior.

## Automated evidence

The suite retains the previous 47 cases and adds 15 cases, including actual TCP chunked HTTP passed through the production factory/provider/view-model. A gate proves the first Candidate update arrives before the server is allowed to send `done`; UTF-8 and NDJSON framing cross HTTP chunk boundaries. Actual 401, malformed second line, timeout during body read and explicit cancellation all clear Candidate/busy and support a subsequent successful retry. Portable tests of a synthetic profile cover isolation, atomic roundtrip, rejection/preservation of credential-bearing or corrupt files, explicit recovery, recipient/provider/model limits and refusal of missing/unsupported/normal profile locations.

Automated fixtures shorten the inter-chunk delay and deadline to 200 ms / 3 seconds; native fixture defaults remain 7 / 15 seconds. Mutating production `ResponseHeadersRead` to `ResponseContentRead` makes the real first-Candidate-before-done test fail; restoring the original bytes makes the suite pass. The tests do not operate the native IDE, decrypt under another Windows account, install on a clean machine or invoke actual models. Native IDE results must be recorded separately with package hash and Visual Studio build.
