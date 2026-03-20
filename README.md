<div align="center">

![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![.NET Framework](https://img.shields.io/badge/.NET_4.7.2-5C2D91?style=for-the-badge&logo=.net&logoColor=white)
![Visual Studio 2022](https://img.shields.io/badge/Visual_Studio_2022-5C2D91?style=for-the-badge&logo=visual-studio&logoColor=white)
![WPF](https://img.shields.io/badge/WPF-157EFB?style=for-the-badge&logo=windows&logoColor=white)
![OpenAI](https://img.shields.io/badge/OpenAI-412991?style=for-the-badge&logo=openai&logoColor=white)
![Ollama](https://img.shields.io/badge/Ollama-000000?style=for-the-badge&logo=ollama&logoColor=white)
![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=for-the-badge)

</div>

# Visual Studio AI Assistant (Local LLM Plugin Modern)

<p align="center">
  <img src="https://raw.githubusercontent.com/furkiak/visualStudioLocalLLMPlugin/refs/heads/master/SSL.png" width="600" title="ActiveRest Dashboard">
</p>


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


## 🇹🇷 Türkçe Açıklama

# Visual Studio AI Asistanı (Local LLM Plugin Modern)

**Visual Studio** için geliştirilmiş güçlü, modern ve yüksek performanslı bir yapay zeka asistanı eklentisidir. Yerel LLM'leri (Ollama) ve bulut tabanlı yapay zeka modellerini (OpenAI, Anthropic, Gemini) doğrudan kodlama ortamınıza sorunsuz bir şekilde entegre eder.

**Clean Architecture (Temiz Mimari)**, **MVVM** ve **Dependency Injection** kullanılarak inşa edilen bu eklenti, IDE'nizden ayrılmadan üretkenliğinizi artıran, VS'nin kendi doğasına uygun, şık ve hızlı bir karanlık tema (Dark Theme) deneyimi sunar.

## Özellikler

* **Çoklu Yapay Zeka Desteği:** **Ollama (Yerel)**, **OpenAI (ChatGPT)**, **Anthropic (Claude)** ve **Google Gemini** arasında tek tıkla geçiş yapın.
* **Akıllı Varsayılanlar:** Seçilen sağlayıcıya göre API URL'lerini ve model adlarını otomatik olarak doldurur.
* **Modern WPF Karanlık Tema:** Visual Studio'nin yerel karanlık moduna mükemmel uyum sağlayan, göz yormayan şık bir arayüz.
* **Işık Hızında Koda Uygulama:** Devasa kod bloklarını editörü dondurmadan anında uygulamak için Visual Studio'nun `UndoContext` yapısını kullanır. İşlemi tek seferde geri almanızı (`Ctrl+Z`) sağlar.
* **Kısmi Metin Enjeksiyonu:** Yapay zekanın verdiği uzun cevabın sadece sizin seçtiğiniz belirli bir kısmını editöre aktararak gereksiz sohbet metinlerinden kurtulmanızı sağlar.
* **Gelişmiş Metin İşleme Araçları:**
    * `Remove Duplicates`: Seçiminizdeki yinelenen satırları anında temizler.
    * `Modify & Replicate`: Hedef bir kelimeyi bulur ve tek tıkla birden fazla yeni değişken için o satırı çoğaltır.
    * `Erase`: Büyük bir kod bloğundan belirli kelimeleri veya karakterleri hızla siler.
    * `Add`: Seçilen her satırın başına veya sonuna belirli bir metni ekler.

## Klavye Kısayolları (Hızlı Kullanım)

Arayüz, fare kullanmadan tamamen klavye ile yönetilmek üzere optimize edilmiştir:
* **`Ctrl + 1`**: AI Asistanını açar (Global VS Kısayolu).
* **`Enter`**: Prompt'unuzu yapay zekaya gönderir.
* **`Shift + Enter`**: Yapay zekanın cevabını doğrudan kod editörünüze uygular ve pencereyi kapatır.
* **`Ctrl + Enter`**: Prompt giriş kutusu içinde yeni bir alt satıra geçer.
* **`Esc`**: İşlemi iptal eder ve asistanı kapatır.

## Kurulum

1. [Releases](../../releases) sayfasından en son `.vsix` sürümünü indirin.
2. İndirilen dosyaya çift tıklayarak VSIX Yükleyicisini açın.
3. **Visual Studio**'yi seçin ve Kur'a tıklayın.
4. Visual Studio'yu yeniden başlatın.
5. Aracı **Araçlar (Tools) -> Local LLM Plugin (Modern)** menüsü altında bulabilir veya doğrudan `Ctrl + 1` tuşlarına basabilirsiniz.

## Mimari ve Teknolojiler

Bu eklenti, tam bir Kurumsal (Enterprise) standartları karşılayacak şekilde sıfırdan yeniden yazılmıştır:
* **Framework:** .NET Framework 4.7.2 / WPF
* **Mimari:** Clean Architecture & MVVM (Model-View-ViewModel)
* **Tasarım Desenleri:** Dependency Injection (Microsoft.Extensions.DependencyInjection), Factory Pattern (AI sağlayıcıları için)
* **Performans:** Bellek sızıntısı olmadan büyük metin bloklarını işlemek için `StringBuilder` ve `StringReader` kullanımı.

## Yapılandırma (Ayarlar)

1. Eklentiyi açın (`Ctrl + 1`).
2. Üst araç çubuğundaki **⚙️ Settings (Ayarlar)** butonuna tıklayın.
3. Tercih ettiğiniz **AI Provider (Yapay Zeka Sağlayıcısını)** seçin.
4. API Anahtarınızı girin (Güvenli bir şekilde `PasswordBox` olarak tutulur). URL'ler ve Model isimleri otomatik doldurulacaktır, isterseniz değiştirebilirsiniz.
5. **Save Settings (Ayarları Kaydet)** butonuna tıklayın.

## Katkıda Bulunma
Pull Request'ler kabul edilmektedir. Büyük değişiklikler için, lütfen önce neyi değiştirmek istediğinizi tartışmak üzere bir konu (issue) açın.
