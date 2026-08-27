<h1 align="center">📻 Realistic Vehicle Radio</h1>

<p align="center">
  <img src="https://img.shields.io/badge/GTA%20V-Legacy%20%7C%20Enhanced-green.svg" alt="GTA V Compatibility">
  <img src="https://img.shields.io/badge/License-GPLv2-blue.svg" alt="License">
  <img src="https://img.shields.io/badge/.NET-4.8-512BD4.svg" alt=".NET Framework">
  <img src="https://img.shields.io/badge/SHVDN-Nightly%20%7C%20Enhanced-orange.svg" alt="Script Hook V .NET">
</p>

<p align="center">
  <b>A highly optimized, feature-rich audio and tunnel detection system for GTA V.</b><br>
  Dynamically adjusts vehicle radio volume and availability based on physical vehicle state and environment.
</p>

---

## 📖 Table of Contents
- [Features](#-features)
- [Architecture & Performance](#-architecture--performance)
- [Companion Mod](#-companion-mod)
- [Requirements](#-requirements)
- [Installation](#-installation)
- [Default Controls](#-default-controls)
- [Documentation & Guides](#-documentation--guides)
- [Building from Source](#-building-from-source)
- [Contributing](#-contributing)
- [License & Credits](#-license--credits)
- [Support](#-support)

---

## ✨ Features

### 🌍 Environmental Awareness
* **Custom Tunnel Muting:** Automatically mutes the radio when driving through user-recorded tunnels, underpasses, or canyons.
* **Water Submersion:** Cuts the radio when the vehicle is deeply submerged underwater (configurable depth threshold).

### 🔊 Dynamic Loudness Mechanics
* **Convertible Roofs:** Switches to exterior "loud" radio mode when the roof/sunroof is open or opening.
* **Broken Windows:** Blasts the radio outward if any window is shattered.
* **Open/Broken Doors:** Adjusts audio projection when doors are ajar, open, or torn off.
* **Random Chance:** Configurable random probability for closed vehicles to have a loud exterior radio.

### 🛠️ Built-in Tunnel Recorder
* **Automatic Recording:** Drive through a tunnel and let the script automatically map the walls, ceiling, and floor using raycasting.
* **Manual Recording:** Add, delete, and tweak tunnel points one-by-one for precise intersections and complex geometry.
* **Real-Time Debug Visualizer:** See the exact mathematical boundaries and spatial-hashing grids in real-time.

### ⚙️ Deep Customization
* **Vehicle Exclusions:** Disable radio logic entirely for specific classes (Aircraft, Boats, Motorcycles) or specific vehicle hashes.
* **JSON Configuration:** Full control over tolerances, search distances, and hotkeys via easy-to-edit JSON files.
* **LemonUI Integration:** In-game menu to manage settings and tunnels.
* **Automatic Update Checker:** Automatically checks GitHub Releases on startup and notifies you in-game if a newer version is available.
* **Staged Menu Saving:** "Reset to Defaults" now safely stages changes in the menu without applying them until you explicitly press "Save Changes".

---

## 🚀 Architecture & Performance

*Modders and developers: Here is how we achieve **zero FPS impact** even with hundreds of mapped tunnels.*

1. **Spatial Hashing Grid (`TunnelSpatialIndex`)**: 
   Instead of checking every vehicle against every tunnel segment in the world (O(N*M)), the script pre-computes tunnel segments into a 64m spatial grid. At runtime, vehicles only check segments in their immediate grid cells. Far-away tunnels are completely ignored by the CPU.
2. **Decoupled Logic & Rendering**:
   Heavy native calls (raycasting, vehicle state checking, water depth) are throttled to a `100ms` logic loop. UI, Menu processing, and Debug Drawing run at `1ms` (every frame) to ensure smooth visuals without blocking the game thread.
3. **Safe Script Abort & State Cleanup**:
   When the script is unloaded, aborted, or reloaded, it gracefully iterates through all tracked vehicles and resets their native radio states. This prevents vehicles from getting "stuck" with disabled or loud radios if the script encounters an error or is stopped.
   
---

## 📍 Companion Mod

Want to visualize your recorded tunnels on the map? 
This script handles the *audio* and *detection* logic. To add custom map blips to your tunnels, check out my other open-source project:
👉 **[Advanced Custom Blips](https://github.com/AliOsamaBm/AdvancedCustomBlips)**

---

## 📦 Requirements

* **Grand Theft Auto V** (Legacy or Enhanced Edition)
* [Script Hook V](http://www.dev-c.com/gtav/scripthookv/)
* [Script Hook V .NET](https://github.com/scripthookvdotnet/scripthookvdotnet-nightly/releases) (Nightly) OR [SHVDN Enhanced](https://www.gta5-mods.com/tools/script-hook-v-net-enhanced)
* [.NET Framework 4.8](https://dotnet.microsoft.com/en-us/download/dotnet-framework/thank-you/net48-web-installer)
* [LemonUI](https://github.com/justalemon/LemonUI) (Included in release, or install manually)
* [Newtonsoft.Json](https://www.newtonsoft.com/json) (Included in release)

---

## 💿 Installation

1. Install all required dependencies
2. Run Install Helper.bat (recommended)
3. Follow the automatic detection or manual selection instructions
4. Extract the correct folder contents into your scripts folder
5. Launch the game
6. Press F3 to open the menu
7. *For detailed troubleshooting and setup, see the [Installation Guide](docs/guides/installation.md).*

---

## 🎮 Default Controls

| Key | Action |
| :--- | :--- |
| **F3** | Open Main Menu |
| **F6** | Toggle Debug Visualizer |
| **F7** | Toggle Automatic Tunnel Recorder |

*All hotkeys can be rebound or disabled via the in-game menu or the `Realistic Vehicle Radio Settings.json` file.*

---

## 📚 Documentation & Guides

Detailed guides are available in the `docs/guides/` directory:

* 📥 **[Installation Guide](docs/guides/installation.md)** - Step-by-step setup and troubleshooting.
* 🎮 **[Controls & Hotkeys](docs/guides/controls-and-hotkeys.md)** - How to rebind and disable keys.
* 🚗 **[Adding Tunnels](docs/guides/adding-tunnels.md)** - Mastering the Automatic and Manual recorders.
* 🗑️ **[Deleting Tunnels](docs/guides/deleting-tunnels.md)** - Managing your JSON database.
* 🐛 **[Debug Mode](docs/guides/debug-mode.md)** - Understanding the visualizer and spatial grid.
* ❓ **[FAQ](docs/faq.md)** - Frequently asked questions.

---

## 🤝 Contributing

Contributions, issues, and feature requests are welcome!
Please read [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines on submitting PRs. If you find a bug, please open an issue and attach your Realistic Vehicle Radio Log.txt or ScriptHookVDotNet.log.

## 📜 License
License: This project is licensed under the GNU General Public License v2.0 (GPLv2). See the [LICENSE](LICENSE) file for details.

## 💖 Support
If you enjoy this mod and want to support the development of future updates and new open-source GTA V projects, consider donating! Even $1 helps motivate me to keep creating.
PayPal: Soliman@dapoupharma.com