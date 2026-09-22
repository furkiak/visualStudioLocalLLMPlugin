# Release candidate validation

2026-09-22 · Windows x64 · VS Professional 2022 17.14 MSBuild · SDK 10.0.401 · .NET Framework 4.8 runtime executing net472 tests.

| Check | Result |
| --- | --- |
| Windows DPAPI encrypt/decrypt, legacy migration, no plaintext backup, tamper rejection, failed atomic replace | Passed using synthetic keys and temporary directories |
| Apply gating, stale selection snapshot, errors, cancellation, busy reset, duplicate send, late response on close | Passed on production view-model/model source |
| Four provider HTTP contracts, 401/403/404/429/500, invalid JSON, incomplete output, timeout, endpoint validation | Passed with HTTP fixtures; no external model calls |
| Real loopback chunked HTTP: first NDJSON piece before final piece; cancel after headers/body begins | Passed with a local TCP HTTP fixture |
| Release VSIX build | Passed; no warning in final local build |
| Real Visual Studio selection → review → apply → one Undo | Pending experimental-instance check |
| Different Windows account cannot decrypt the DPAPI blob | Pending actual second-account check; tamper tests are not a substitute |
| Community / Enterprise runtime and fresh-machine VSIX install | Pending; manifest targeting is not a runtime test |
| Real Ollama, OpenAI, Gemini, Anthropic model responses | Pending; no credentials or proprietary code used in tests |

The framework HTTP stack can buffer fixed Content-Length responses until more body data arrives. Incremental behavior is verified for chunked NDJSON, the intended Ollama streaming transport. Reverse proxies must forward chunks rather than buffer them. No token streaming is claimed for the other three providers.

## IDE smoke procedure

Use a dedicated experimental VS instance and a disposable text file containing two identical lines. Do not use a production solution.

1. Select both lines; open Tools → Local LLM Plugin (Modern). Apply must be disabled initially.
2. Choose Remove duplicates. Original must retain both lines, Candidate one line, Changes show a removal. Apply remains disabled until review is checked.
3. Check the review box; apply. Only the original selection changes. Ctrl+Z restores both lines in one operation.
4. Generate with the local fixture or a configured model. Cancel while waiting: no editor change; busy clears; Apply is disabled. Close while waiting: the HTTP operation is cancelled.
5. Cause a 401, malformed stream and timeout. Error text stays in status; Apply cannot replace code with it. Recover through Settings without restarting VS.
6. Change target contents/selection while a request is pending using a second editor surface. Applying a stale result must be rejected.
7. Check light/dark/high-contrast themes, keyboard-only controls, 200% scaling and a narrow window. Record actual screenshots and VS build number.

## Migration and rollback

The first successful settings read replaces the legacy `ApiKey` field with a DPAPI-protected `ProtectedApiKey` field, preserving provider, model and URL. Failed parsing/decryption leaves the existing file untouched. Settings shows a recovery explanation; saving explicitly replaces unreadable settings. The extension does not inspect or modify old backups.

Version 3.0 cannot read the protected key format. A rollback to 3.0 requires re-entering the key and restores that version's plaintext-storage risk. Prefer disabling 3.1 while investigating instead of downgrading and re-saving secrets. Keep the old VSIX available, but do not change old release assets or tags.

## Sources

- [Windows DPAPI CurrentUser](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.protecteddata)
- [Ollama generation and streaming](https://docs.ollama.com/api/generate)
- [VSIX target SKU inheritance](https://learn.microsoft.com/en-us/visualstudio/extensibility/vsix-extension-schema-2-0-reference?view=vs-2022)
