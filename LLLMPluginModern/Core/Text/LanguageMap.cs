using System;
using System.Collections.Generic;
using System.IO;

namespace LLLMPluginModern.Core.Text
{
    /// <summary>Maps Visual Studio content types and file extensions to display names and Markdown fence ids.</summary>
    public static class LanguageMap
    {
        private static readonly Dictionary<string, (string Name, string Id)> ByExtension =
            new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
            {
                [".cs"] = ("C#", "csharp"),
                [".csx"] = ("C#", "csharp"),
                [".vb"] = ("Visual Basic", "vb"),
                [".fs"] = ("F#", "fsharp"),
                [".fsx"] = ("F#", "fsharp"),
                [".cpp"] = ("C++", "cpp"),
                [".cc"] = ("C++", "cpp"),
                [".cxx"] = ("C++", "cpp"),
                [".c"] = ("C", "c"),
                [".h"] = ("C/C++ header", "cpp"),
                [".hpp"] = ("C++ header", "cpp"),
                [".ts"] = ("TypeScript", "typescript"),
                [".tsx"] = ("TypeScript (TSX)", "tsx"),
                [".js"] = ("JavaScript", "javascript"),
                [".jsx"] = ("JavaScript (JSX)", "jsx"),
                [".mjs"] = ("JavaScript", "javascript"),
                [".json"] = ("JSON", "json"),
                [".xml"] = ("XML", "xml"),
                [".xaml"] = ("XAML", "xml"),
                [".axaml"] = ("XAML", "xml"),
                [".csproj"] = ("MSBuild", "xml"),
                [".props"] = ("MSBuild", "xml"),
                [".targets"] = ("MSBuild", "xml"),
                [".config"] = ("XML", "xml"),
                [".html"] = ("HTML", "html"),
                [".htm"] = ("HTML", "html"),
                [".cshtml"] = ("Razor", "razor"),
                [".razor"] = ("Razor", "razor"),
                [".css"] = ("CSS", "css"),
                [".scss"] = ("SCSS", "scss"),
                [".less"] = ("Less", "less"),
                [".sql"] = ("SQL", "sql"),
                [".py"] = ("Python", "python"),
                [".ps1"] = ("PowerShell", "powershell"),
                [".psm1"] = ("PowerShell", "powershell"),
                [".sh"] = ("Shell", "bash"),
                [".bat"] = ("Batch", "bat"),
                [".cmd"] = ("Batch", "bat"),
                [".md"] = ("Markdown", "markdown"),
                [".yml"] = ("YAML", "yaml"),
                [".yaml"] = ("YAML", "yaml"),
                [".go"] = ("Go", "go"),
                [".rs"] = ("Rust", "rust"),
                [".java"] = ("Java", "java"),
                [".kt"] = ("Kotlin", "kotlin"),
                [".swift"] = ("Swift", "swift"),
                [".php"] = ("PHP", "php"),
                [".rb"] = ("Ruby", "ruby"),
                [".dart"] = ("Dart", "dart"),
                [".lua"] = ("Lua", "lua"),
                [".hlsl"] = ("HLSL", "hlsl"),
                [".proto"] = ("Protocol Buffers", "protobuf"),
            };

        private static readonly Dictionary<string, (string Name, string Id)> ByContentType =
            new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
            {
                ["CSharp"] = ("C#", "csharp"),
                ["Basic"] = ("Visual Basic", "vb"),
                ["F#"] = ("F#", "fsharp"),
                ["C/C++"] = ("C++", "cpp"),
                ["TypeScript"] = ("TypeScript", "typescript"),
                ["JavaScript"] = ("JavaScript", "javascript"),
                ["JScript"] = ("JavaScript", "javascript"),
                ["JSON"] = ("JSON", "json"),
                ["XML"] = ("XML", "xml"),
                ["XAML"] = ("XAML", "xml"),
                ["HTML"] = ("HTML", "html"),
                ["HTMLX"] = ("HTML", "html"),
                ["Razor"] = ("Razor", "razor"),
                ["RazorCSharp"] = ("Razor", "razor"),
                ["CSS"] = ("CSS", "css"),
                ["SQL Server Tools"] = ("SQL", "sql"),
                ["SQL"] = ("SQL", "sql"),
                ["Python"] = ("Python", "python"),
                ["PowerShell"] = ("PowerShell", "powershell"),
                ["markdown"] = ("Markdown", "markdown"),
            };

        private static readonly Dictionary<string, string> FenceNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["csharp"] = "C#", ["cs"] = "C#", ["c#"] = "C#", ["vb"] = "Visual Basic", ["vbnet"] = "Visual Basic",
            ["fsharp"] = "F#", ["fs"] = "F#", ["cpp"] = "C++", ["c++"] = "C++", ["c"] = "C", ["h"] = "C/C++",
            ["typescript"] = "TypeScript", ["ts"] = "TypeScript", ["tsx"] = "TSX", ["javascript"] = "JavaScript",
            ["js"] = "JavaScript", ["jsx"] = "JSX", ["json"] = "JSON", ["xml"] = "XML", ["xaml"] = "XAML", ["html"] = "HTML",
            ["css"] = "CSS", ["scss"] = "SCSS", ["sql"] = "SQL", ["python"] = "Python", ["py"] = "Python",
            ["powershell"] = "PowerShell", ["ps1"] = "PowerShell", ["bash"] = "Shell", ["sh"] = "Shell", ["shell"] = "Shell",
            ["bat"] = "Batch", ["cmd"] = "Batch", ["yaml"] = "YAML", ["yml"] = "YAML", ["markdown"] = "Markdown", ["md"] = "Markdown",
            ["razor"] = "Razor", ["cshtml"] = "Razor", ["go"] = "Go", ["rust"] = "Rust", ["rs"] = "Rust", ["java"] = "Java",
            ["kotlin"] = "Kotlin", ["swift"] = "Swift", ["php"] = "PHP", ["ruby"] = "Ruby", ["dart"] = "Dart", ["diff"] = "Diff",
            ["text"] = "Text", ["plaintext"] = "Text", ["txt"] = "Text",
        };

        /// <summary>Pretty name for a Markdown fence id ("csharp" → "C#"); unknown ids are returned as-is.</summary>
        public static string DisplayNameForFence(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return "Code";
            return FenceNames.TryGetValue(id.Trim(), out var name) ? name : id.Trim();
        }

        public static (string Name, string Id) Resolve(string filePath, string contentType)
        {
            string extension = string.IsNullOrEmpty(filePath) ? "" : Path.GetExtension(filePath);
            if (!string.IsNullOrEmpty(extension) && ByExtension.TryGetValue(extension, out var byExt))
                return byExt;
            if (!string.IsNullOrEmpty(contentType) && ByContentType.TryGetValue(contentType, out var byType))
                return byType;
            if (!string.IsNullOrEmpty(contentType) && !string.Equals(contentType, "text", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(contentType, "plaintext", StringComparison.OrdinalIgnoreCase))
                return (contentType, contentType.ToLowerInvariant().Replace(" ", ""));
            return ("Text", "");
        }
    }
}
