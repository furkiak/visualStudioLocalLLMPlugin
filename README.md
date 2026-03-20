# Visual Studio AI Assistant (Local LLM Plugin Modern)

A powerful, modern, and highly optimized AI assistant extension for **Visual Studio**. Seamlessly integrate local LLMs (Ollama) and cloud-based AI models (OpenAI, Anthropic, Gemini) directly into your coding environment. 

Built with **Clean Architecture**, **MVVM**, and **Dependency Injection**, this plugin offers a lightning-fast, native-feeling dark theme experience that boosts your productivity without leaving your IDE.

## Features

* **Multi-Provider AI Support:** Switch seamlessly between **Ollama (Local)**, **OpenAI (ChatGPT)**, **Anthropic (Claude)**, and **Google Gemini**.
* **Smart Defaults:** Automatically configures API URLs and model names based on the selected provider.
* **Modern WPF Dark Theme:** A sleek, eye-friendly interface that perfectly matches Visual Studio's native dark mode.
* **Lightning Fast Text Application:** Utilizes Visual Studio's `UndoContext` to instantly apply massive code blocks without freezing the editor, allowing single-step Undo (`Ctrl+Z`).
* **Partial Selection Injection:** Select a specific part of the AI's response to inject only that portion into your code, ignoring unnecessary conversational text.
* **Advanced Text Manipulation Tools:**
    * ` Remove Duplicates`: Instantly cleans up duplicate lines in your selection.
    * ` Modify & Replicate`: Find a target word and duplicate the line for multiple new variables in one click.
    * ` Erase`: Quickly strip specific words or characters from a massive text block.
    * ` Add`: Prepend or append specific text to every selected line.

## Keyboard Shortcuts (Power User Friendly)

The UI is entirely optimized for keyboard-only navigation:
* **`Ctrl + 1`**: Open the AI Assistant (Global VS Shortcut).
* **`Enter`**: Send your prompt to the AI.
* **`Shift + Enter`**: Apply the AI's response directly to your code editor and close the window.
* **`Ctrl + Enter`**: Add a new line within the prompt input box.
* **`Esc`**: Cancel and close the assistant.

## Installation

1. Download the latest `.vsix` release from the [Releases](../../releases) page.
2. Double-click the downloaded file to open the VSIX Installer.
3. Select **Visual Studio** and click Install.
4. Restart Visual Studio. 
5. You can find the tool under the **Tools -> Local LLM Plugin (Modern)** menu, or simply press `Ctrl + 1`.

## Architecture & Technologies

This extension was rebuilt from scratch to meet Enterprise standards:
* **Framework:** .NET Framework 4.7.2 / WPF
* **Architecture:** Clean Architecture & MVVM (Model-View-ViewModel)
* **Patterns:** Dependency Injection (Microsoft.Extensions.DependencyInjection), Factory Pattern (for AI providers)
* **Performance:** `StringBuilder` and `StringReader` for handling large text blocks without memory leaks.

## Configuration

1. Open the plugin (`Ctrl + 1`).
2. Click the **⚙️ Settings** button on the top toolbar.
3. Select your preferred **AI Provider**.
4. Enter your API Key (Securely stored as a `PasswordBox`). URLs and Model names will be auto-filled, but you can override them.
5. Click **Save Settings**.

## Contributing
Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.

## License
[MIT](https://choosealicense.com/licenses/mit/)