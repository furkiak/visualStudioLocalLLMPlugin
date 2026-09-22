# Changelog

## 3.1.0 — release candidate, not yet published

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
