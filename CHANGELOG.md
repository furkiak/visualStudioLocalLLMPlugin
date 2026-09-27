# Changelog

## 3.1.1 — local release candidate, not yet published

- Add an explicit process-only validation profile and real loopback chunked HTTP fixture for repeatable IDE stream/error/cancel checks. Show synthetic/no-model labels, isolate settings from normal AppData/DPAPI, reject credentials and other recipients, and allow scenario changes from Settings without restarting the IDE. Keep normal startup/provider behavior.
- Retain 47 tests and add 15 profile/real HTTP workflow cases; prove early Candidate delivery with a gate and an expected-red buffering mutation.
- Give corrupt validation settings an explicit fixture-only recovery default, so the read-only endpoint can be saved from the visible recovery form without editing files.
- Increment local VSIX/assembly version to 3.1.1 while preserving the extension ID and existing public release assets.

## 3.1.0 — previous local release candidate

- Protect saved API keys with Windows DPAPI CurrentUser; migrate legacy plaintext settings atomically without a plaintext backup.
- Require a successful, finished candidate and explicit review before replacing an unchanged editor selection. Preserve one Undo operation.
- Compare the active document by path during review; a real VS smoke test found COM wrapper reference checks left Apply disabled. Remove the conflicting Ctrl+1 default binding and keep the Tools menu command.
- Separate status/errors from candidate text; propagate cancellation to all four HTTP providers. Close cancels an active request.
- Read Ollama NDJSON incrementally. Reject truncated, malformed, empty and oversized responses; bound request size and elapsed time.
- Show the actual destination, model and outgoing UTF-8 byte count. Only the selected code and instruction are sent. Disable redirects; require HTTPS outside loopback; send Gemini keys in a header.
- Use Visual Studio theme resources for the main window; provide original/candidate/change views and keyboard labels.
- Correct the README license to the existing AGPL-3.0 license. Preserve extension ID and repository URL.
- Add Windows tests and CI; stop tracking IDE cache output.

Existing release assets have not been replaced. See the validation matrix in `docs/VALIDATION.md` before publishing this candidate.
