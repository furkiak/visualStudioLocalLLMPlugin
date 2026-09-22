# Contributing

Use a small branch with a concrete reproduction and one behavior change. Build with Visual Studio 2022, .NET desktop development, Visual Studio extension development and the .NET Framework 4.7.2 targeting pack.

```powershell
dotnet test LLLMPluginModern.Tests/LLLMPluginModern.Tests.csproj -c Release
msbuild LLLMPluginModern/LLLMPluginModern.csproj /restore /t:Rebuild /p:Configuration=Release /p:DeployExtension=false
```

Run MSBuild from a Developer PowerShell for VS 2022. The test project links production source to test settings, view models and provider contracts without loading the IDE; the separate VSIX build checks the SDK and XAML integration. It does not replace the real IDE checks in `docs/VALIDATION.md`.

Do not commit credentials, settings.json, editor caches or generated packages. Use synthetic code and a local HTTP fixture for provider tests. Retain the AGPL-3.0 license and document external dependencies. Describe what was actually tested and what needs an IDE, second Windows account or real provider.
