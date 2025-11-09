<div align="center">
    <img src="./ProseFlow.UI/Assets/logo.svg" alt="Project Logo" width="256" height="256">

# ProseFlow

**Your Universal AI Text Processor, Powered by Local and Cloud LLMs.**

**[Official Website](https://lsxprime.github.io/proseflow-web)**

[![Build Status](https://github.com/LSXPrime/ProseFlow/actions/workflows/release.yml/badge.svg)](https://github.com/LSXPrime/ProseFlow/actions/workflows/release.yml) [![License: AGPL v3](https://img.shields.io/badge/License-AGPL_v3-blue.svg)](https://www.gnu.org/licenses/agpl-3.0) [![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0) [![Release](https://img.shields.io/github/v/release/LSXPrime/ProseFlow?color=black)](https://github.com/LSXPrime/ProseFlow/releases) [![ProseFlow-Actions-v1 Dataset](https://img.shields.io/badge/%F0%9F%A4%97%20Hugging%20Face-Datasets-yellow)](https://huggingface.co/datasets/LSXPrime/ProseFlow-Actions-v1)

ProseFlow is a cross-platform desktop application that integrates powerful AI text processing into your daily workflow.
With a simple hotkey, you can access a menu of customizable AI actions to proofread, summarize, generate, or transform
text in *any* application—be it your code editor, browser, or word processor.


  <img src="https://raw.githubusercontent.com/LSXPrime/_resources/refs/heads/main/ProseFlow/video-hero_section-software.gif" width="80%"  alt="ProseFlow Preview"/>

Its unique hybrid engine allows you to seamlessly switch between the world's best cloud-based LLMs and private, offline-capable models running directly on your own hardware.

---

[![Stand With Palestine](https://raw.githubusercontent.com/Safouene1/support-palestine-banner/master/banner-support.svg)](https://thebsd.github.io/StandWithPalestine)
  <p><strong>This project stands in solidarity with the people of Palestine and condemns the ongoing violence and ethnic cleansing by Israel. We believe developers have a responsibility to be aware of such injustices.</strong></p>

</div>

---

### Screenshots

<table>
  <tr>
    <td align="center"><strong>Floating Action Menu</strong></td>
    <td align="center"><strong>Templated Action Menu</strong></td>
  </tr>
  <tr>
    <td><img src="https://raw.githubusercontent.com/LSXPrime/_resources/refs/heads/main/ProseFlow/screenshot-floating-action-menu.png" alt="Floating Action Menu"></td>
    <td><img src="https://raw.githubusercontent.com/LSXPrime/_resources/refs/heads/main/ProseFlow/screenshot-template-menu.gif" alt="Templated Action Menu"></td>
  </tr>
  <tr>
    <td align="center"><strong>Action Management</strong></td>
    <td align="center"><strong>Advanced Action Editor</strong></td>
  </tr>
  <tr>
    <td><img src="https://raw.githubusercontent.com/LSXPrime/_resources/refs/heads/main/ProseFlow/screenshot-actions.png" alt="Action Management"></td>
    <td><img src="https://raw.githubusercontent.com/LSXPrime/_resources/refs/heads/main/ProseFlow/screenshot-action-editor.gif" alt="Advanced Action Editor"></td>
  </tr>
  <tr>
    <td align="center"><strong>Comprehensive Dashboard</strong></td>
    <td align="center"><strong>Local Model Library</strong></td>
  </tr>
  <tr>
    <td><img src="https://raw.githubusercontent.com/LSXPrime/_resources/refs/heads/main/ProseFlow/screenshot-dashboard.png" alt="Dashboard"></td>
    <td><img src="https://raw.githubusercontent.com/LSXPrime/_resources/refs/heads/main/ProseFlow/screenshot-local-model-library.png" alt="Local Model Library"></td>
  </tr>
  <tr>
    <td align="center"><strong>Floating Orb & Arc Menu</strong></td>
    <td align="center"><strong>Workspace Sync Management</strong></td>
  </tr>
  <tr>
    <td><img src="https://raw.githubusercontent.com/LSXPrime/_resources/refs/heads/main/ProseFlow/screenshot-arc-menu.png" alt="Floating Orb & Arc Menu"></td>
    <td><img src="https://raw.githubusercontent.com/LSXPrime/_resources/refs/heads/main/ProseFlow/screenshot-workspace-sync.png" alt="Workspace Sync Management"></td>
  </tr>
</table>

---

<div align="center">
<details>
<summary><strong>Table of Contents</strong></summary>
<br>

- [Features](#-features)
- [Getting Started](#-getting-started)
- [How to Use](#-how-to-use)
- [Official Local Models](#-official-local-models)
- [Architecture Overview](#-architecture-overview)
- [Support This Project](#-support-this-project)
- [Building from Source](#-building-from-source)
- [Technology Stack](#-technology-stack)
- [Contributing](#-contributing)
- [License](#-license)
- [Acknowledgements](#-acknowledgements)

</details>
</div>

---

### ✨ Features

ProseFlow is packed with features designed for power, privacy, and productivity.

#### 🚀 Core Workflow & UI

*   **Global Hotkey Activation:** Access ProseFlow from **any application** with a customizable system-wide hotkey.
*   **Generation Mode:** Create content from scratch. When no text is selected, the menu adapts to show generation-focused actions.
*   **Floating Action Orb:** A persistent on-screen button providing visual feedback on background tasks and a new way to trigger actions.
*   **Radial Arc Menu:** Drag and drop text onto the orb to instantly bring up a context-aware radial menu of your favorite actions.
*   **Floating Action Menu:** An elegant, **searchable menu** of your AI actions appears right where you need it.
*   **Smart Paste:** Assign a dedicated hotkey to your most frequent action for **one-press text transformation**.
*   **Flexible Output Modes:** Choose to have results instantly **replace** your text, open in an **interactive window** for review, or view as a side-by-side **diff**.
*   **Iterative Refinement:** **Conversationally refine** AI output in the result window until it's perfect.
*   **Context-Aware Actions:** Configure actions to only appear when you're in **specific applications**.

#### 🛠️ Customization & Management

*   **Templated Actions:** Create dynamic, multi-step actions with **placeholders** (e.g., `Write an email to [Recipient]`). The menu will prompt you for input at runtime.
*   **Advanced Placeholders:** Supports multiple input types (Text, Choice, Boolean, File Picker), validation rules, conditional logic, and smart defaults (`{clipboard}`, `{date}`).
*   **Powerful Action Editor:** A completely redesigned editor for building and managing complex templated actions.
*   **Bulk Action Management:** Select multiple actions to **delete, move, or export** them all at once.
*   **Duplicate & Reorder Actions:** Easily clone existing actions or move them up/down within their group.
*   **Action Groups:** Organize your actions into logical groups with a **drag-and-drop interface**.
*   **Action Favorites:** Mark your most-used actions as favorites for prioritized access.
*   **Import & Export:** **Share your action sets** with others or back up your configuration to a JSON file.
*   **Action Presets:** Get started quickly by importing **curated sets of actions** for common tasks like writing, coding, and more.

#### 🤝 Workspace & Collaboration

*   **Workspace Sync:** Share your actions and provider configurations across devices or with your team by syncing to a shared folder (e.g., on a network drive or cloud service).
*   **Password-Protected Encryption:** Sensitive data like API keys are encrypted with a workspace password, ensuring only authorized users can access them.
*   **Automatic & Manual Sync:** Choose to sync automatically in the background or get notified of changes to sync manually.
*   **Conflict Resolution:** Intelligently handle cases where imported actions conflict with existing ones.

#### 🧠 Hybrid AI Engine

*   **Run 100% Locally & Offline:** Use **GGUF-compatible models** on your own hardware for maximum **privacy** and offline access.
*   **Official Fine-Tuned Models:** Download our **custom-built models**, which are specifically **optimized for ProseFlow's tasks** to provide the best possible local experience. [Learn More](#-official-local-models)
*   **Connect to Cloud APIs:** Integrates with **OpenAI, Groq, Anthropic, Google**, and any **OpenAI-compatible endpoint**.
*   **Intelligent Fallback Chain:** Configure multiple cloud providers. If one fails, ProseFlow **automatically tries the next**.
*   **Secure Credential Storage:** API keys are **always encrypted** and stored securely on your local machine.
*   **Advanced Local Model Control:** Fine-tune performance with specific GPU selection, thread count, and advanced memory options.

#### 📊 Dashboard & Analytics

*   **Usage Dashboard:** Visualize your **token usage over time** for both cloud and local models.
*   **Live Task Monitoring:** An "Active Processes" widget shows all ongoing AI tasks and allows you to **cancel them**.
*   **Live Application Log:** A real-time log console in the dashboard for advanced diagnostics and troubleshooting.
*   **Performance Monitoring:** Track **provider latency** and **tokens/second** to optimize your setup.
*   **Live Hardware Monitor:** See real-time **CPU, GPU, RAM, and VRAM usage** when running local models.
*   **Interaction History:** Review a **detailed log** of all your past AI operations, and even save custom instructions as new actions.

#### 💻 Platform Integration

*   **Cross-Platform:** Native support for **Windows, macOS, and Linux**.
*   **System Tray Control:** Runs quietly in the background with a **tray icon for quick access** to key functions.
*   **Launch at Login:** Configure ProseFlow to **start automatically** with your system, optionally minimized to the tray.
*   **Guided Onboarding:** A **smooth setup process** for new users to get configured in minutes.

---

### 🚀 Getting Started

The easiest way to get started is by downloading the latest version from our official website.

### **[➡️ Download from the Official Website ⬅️](https://lsxprime.github.io/proseflow-web)**

The website will automatically suggest the best download for your operating system (Windows, macOS, or Linux).

1.  **Download the Installer:** Click the main download button on the website for your detected OS, or choose a specific
    version from the options below it.
2.  **Install & Run:** Install the application like any other.
3.  **Onboarding:** The first time you run ProseFlow, a guided setup window will help you configure your first AI
    provider and set your global hotkey. You'll be ready in minutes!

For advanced users who need access to all builds, portable versions, or detailed release notes, you can visit
the [GitHub Releases page](https://github.com/LSXPrime/ProseFlow/releases).


#### macOS Installation & Permissions

Due to Apple's security policies, you will need to perform a few extra steps to run ProseFlow on macOS:

1.  **Open the App for the First Time:**
    *   After downloading, move `ProseFlow.app` to your `Applications` folder.
    *   Right-click the app and select "Open".
    *   You will see a warning that the developer is unidentified. This is expected. Click "Open" again to confirm. You only need to do this once.

2.  **Grant Required Permissions:**
    ProseFlow needs two critical permissions to function. When prompted, you must grant them.
    *   Go to **System Settings > Privacy & Security**.
    *   Find **Accessibility** in the list. Click it, and then enable the toggle for **ProseFlow**.
    *   Find **Input Monitoring** in the list. Click it, and then enable the toggle for **ProseFlow**.

*If ProseFlow is not in the list, click the `+` button and add it from your Applications folder.*

---

### 📖 How to Use

ProseFlow offers multiple ways to process text, designed to be fast and intuitive:

1.  **Transforming Selected Text:**
    *   **Select Text:** Highlight any text in any application.
    *   **Press Hotkey:** Press your configured Action Menu hotkey (default is `Ctrl+J`).
    *   **Choose an Action:** The floating menu will appear. Use your mouse or arrow keys to select an action and press `Enter`.
2.  **Generating New Content:**
    *   **Press Hotkey:** With no text selected, press your Action Menu hotkey.
    *   **Choose an Action:** Select a generation action (e.g., "Write an Email").
    *   **Fill the Form:** Complete the steps in the menu.
3.  **Drag-and-Drop Method:**
    *   **Drag Text:** Select and drag text from any application.
    *   **Drop on Orb:** Drop the text onto the Floating Action Orb.
    *   **Choose an Action:** The radial Arc Menu will appear around the orb. Click an action to process the text.
4.  **Results:**
    *   Depending on the action's configuration, your text will be replaced instantly, or a result window (standard, or a side-by-side diff) will appear for review.

---

### 🧠 Official Local Models

To provide the best possible out-of-the-box experience, we have fine-tuned and released official models specifically for ProseFlow. These models are optimized to understand the application's unique instruction format and excel at a wide range of tasks.

Both models are available for one-click download directly within ProseFlow from the Model Library (`Providers -> Manage Models...`).

| Model | Best For | VRAM (Approx.) |
| :--- | :--- | :--- |
| **[ProseFlow-v1-1.5B-Instruct](https://huggingface.co/LSXPrime/ProseFlow-v1-1.5B-Instruct) (Recommended)** | The **best overall experience**. A versatile model based on `Qwen2.5-Coder` that excels at coding, logical reasoning, and high-quality text generation. The `Q8_0` quant is recommended. | ~2.5 GB |
| **[ProseFlow-v1-360M-Instruct](https://huggingface.co/LSXPrime/ProseFlow-v1-360M-Instruct) (Experimental)** | **Extremely lightweight** use on low-resource devices. Fast, but with significant limitations in reasoning and complex tasks. Suitable for basic text formatting. | ~1 GB |

For most users, the **`1.5B-Instruct`** model provides an ideal balance of high performance and manageable resource requirements.

The task-focused ability of our official models is made possible by the custom dataset they were trained on, which we have also open-sourced for the community, **[ProseFlow-Actions-v1](https://huggingface.co/datasets/LSXPrime/ProseFlow-Actions-v1)** is a high-quality, diverse dataset of over 1,800 structured examples. Unlike general-purpose chat datasets, it's specifically designed for the "tool-based" workflow of ProseFlow, focusing on tasks that require high-fidelity text transformation and strict adherence to formatting constraints.


By open-sourcing the data, we invite developers and researchers to inspect our methodology, build upon our work, and create even better small, task-focused models. The dataset is released under the permissive **MIT License**.

---

### 🏗️ Architecture Overview

ProseFlow is built using a modern, layered architecture inspired by **Clean Architecture**, promoting separation of
concerns, testability, and maintainability.

*   **`ProseFlow.Core`**: The domain layer. Contains the core business models, enums, and interfaces for repositories and
    services. It has zero dependencies on other layers.
*   **`ProseFlow.Application`**: The application layer. It orchestrates the business logic using services, DTOs, and
    application-specific events. It depends only on `Core`.
*   **`ProseFlow.Infrastructure`**: The infrastructure layer. Contains all implementations of external concerns,
    including:
    *   **Data Access:** Entity Framework Core with SQLite using the Repository & Unit of Work patterns.
    *   **AI Providers:** Implementations for Cloud (`LlmTornado`) and Local (`LLamaSharp`) providers.
    *   **OS Services:** Cross-platform hotkeys (`SharpHook`), clipboard access, and active window tracking.
*   **`ProseFlow.UI`**: The presentation layer. A cross-platform desktop application built with **Avalonia** and the **ShadUI** component library, following the **MVVM** pattern.

---

## ❤️ Support This Project

ProseFlow is an open-source project driven by a single developer on a single PC. The goal is to create a powerful, private, and flexible AI writing assistant that works flawlessly for everyone, regardless of their operating system.

However, developing and maintaining a professional cross-platform application comes with significant challenges and costs that are difficult to cover alone.

**The Current Reality:**
ProseFlow is actively developed and tested on **Windows and Linux (via WSL)**. Due to a lack of hardware, **I am currently unable to properly test, debug, build, or release for macOS.** The current macOS support is "best-effort" and relies on community feedback, which is not a sustainable way to ensure a quality product.

### Your Support Directly Enables:

*   **First-Class macOS Support:** The single biggest hurdle for the project. Your contributions will go directly towards acquiring a Mac for dedicated macOS development. This is essential for:
    *   Properly testing and debugging the application.
    *   Building official, signed, and notarized releases.
    *   Fixing platform-specific bugs and ensuring a native feel.

*   **Official Code Signing & Notarization:** To keep the application safe and functional, we need:
    *   **An Apple Developer Account ($99/year):** Required for building and distributing a trusted macOS application.
    *   **A Windows Code Signing Certificate:** Essential for removing the "untrusted application" warnings on Windows, making ProseFlow a secure and trustworthy tool you can rely on.

*   **Dedicated Development Time:** Your support allows me to dedicate more focused time to development, leading to faster feature implementation, more thorough bug fixes, and better overall project quality across all platforms.

You can directly support ProseFlow and help transform it into a truly professional, cross-platform tool through:

*   **Ko-fi:** For simple one-time donations or flexible recurring support.

    [![ko-fi](https://img.shields.io/badge/Ko--fi-F16061?style=for-the-badge&logo=ko-fi&logoColor=white)](https://ko-fi.com/lsxprime)


*   **AirTM:** For simple one-time donations with various payment options like Direct Bank Transfer (ACH), Debit / Credit Card via Moonpay, Stablecoins, and more than 500 banks and e-wallets.

    [Donate using AirTM](https://airtm.me/lsxprime)

*   **USDT (Tron/TRC20):** Supporting directly by sending to the following USDT wallet address.

    ```markdown
    TKZzeB71XacY3Av5rnnQVrz2kQqgzrkjFn
    ```

    **Important:** Please ensure you are sending USDT via the **TRC20 (Tron)** network. Sending funds on any other network may result in their permanent loss.

**By becoming a sponsor or making a donation, you are directly investing in the future of ProseFlow, helping to overcome critical hardware limitations and ensuring it becomes a reliable, secure, and first-class application for all users. Thank you for your generosity!**

---

### 🔧 Building from Source

#### Prerequisites

*   .NET 8 SDK
*   Git

#### Steps

1.  Clone the repository:
    ```bash
    git clone https://github.com/LSXPrime/ProseFlow.git
    cd ProseFlow
    ```
2.  Navigate to the UI project:
    ```bash
    cd ProseFlow.UI
    ```
3.  Run the application:
    ```bash
    dotnet run -f net8.0
    ```
    Replace `net8.0` with `net8.0-windows` for Windows, `net8.0-mac` for macOS, or `net8.0` for Linux.

---

### 🛠️ Technology Stack

*   **UI Framework:** [Avalonia UI](https://avaloniaui.net/)
*   **UI Components:** [ShadUI](https://github.com/accntech/shad-ui)
*   **MVVM Framework:** [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
*   **Database:** [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/) with SQLite
*   **Local LLM Engine:** [LLamaSharp](https://github.com/SciSharp/LLamaSharp)
*   **Cloud LLM Library:** [LlmTornado](https://github.com/lofcz/LlmTornado)
*   **Global Hotkeys:** [SharpHook](https://github.com/TolikPylypchuk/SharpHook)
*   **Hardware Monitoring:** [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)
*   **Dependency Injection:** [Microsoft.Extensions.DependencyInjection](https://github.com/dotnet/runtime/tree/main/src/libraries/Microsoft.Extensions.DependencyInjection)
*   **Logging:** [Serilog](https://serilog.net/)
*   **Update:** [Velopack](https://github.com/velopack/velopack)

---

### 🤝 Contributing

Contributions are welcome! Whether it's reporting a bug, suggesting a new feature, or submitting a pull request, your
help is greatly appreciated. Please check the [CONTRIBUTING.md](CONTRIBUTING.md) file for more information.

---

### 📜 License

ProseFlow is free and open-source software licensed under the **GNU Affero General Public License v3.0 (AGPLv3)**. See
the [LICENSE](LICENSE.md) file for details.

---

### 🙏 Acknowledgements

This project would not be possible without the incredible open-source libraries it is built upon. Special thanks to the
teams and contributors behind Avalonia, LLamaSharp, LlmTornado, all the other fantastic projects listed in the
technology stack, and indeed the rest of the open-source libraries empowering this project.