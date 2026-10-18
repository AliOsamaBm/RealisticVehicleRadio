# Frequently Asked Questions (FAQ)

---

## 🐛 I found a bug or mod conflict. How do I report it?

Please contact me directly through the mod's page (officially on the [GTA5-Mods website](https://www.gta5-mods.com/)).

When reporting an issue, please include:

* Your GTA V version (**Legacy or Enhanced**)
* Your SHVDN version (**Nightly or Enhanced**)
* The contents of `Realistic Vehicle Radio Log.txt`
* Additionally, include the contents of `scripthookvdotnet.log`
* The **steps to reproduce** the issue, if possible

Providing clear reproduction steps and the relevant log file makes it much easier to identify and fix the problem.

---

## 🎮 Does this mod affect performance (FPS)?

No.

The script uses a custom **spatial-hashing grid** for tunnel detection and throttles heavy logic checks. Even with hundreds of tunnels recorded across the map, the script only calculates the tunnels immediately around you.

As a result, the mod has **virtually zero FPS impact** during normal gameplay.

---

## 🎙️ How do I record a new tunnel?

1. Get into a vehicle.
2. Press the **Recorder Toggle Key** (default: **F7**) to start automatic recording.
3. Drive through the tunnel.
4. Press **F7** again to stop and save the recording.

You can also use the **Manual Recording** menu to add points one by one for more precise adjustments. See [this guide](guides/adding-tunnels.md) for more detailed information.

---

## 📍 Can I add map blips to my recorded tunnels?

This script handles the audio and tunnel detection logic.

To add custom map blips to your tunnels, I highly recommend using my other mod, [Advanced Custom Blips](https://www.gta5-mods.com/scripts/advanced-custom-blips-fe7d9d03-1fc1-4088-9fae-005dc2b1a270).

---

## ⌨️ How do I disable a key?

You can disable keys in two ways:

### 1. In-game menu

1. Open the menu using **F3**.
2. Go to the respective settings menu.
3. Find the key you want to disable.
4. Scroll through the list until it shows **"None"**.
5. Select **"None"**.
6. Press **Save Changes**.

### 2. JSON file

1. Open `Realistic Vehicle Radio Settings.json`.
2. Find the key you want to disable.
3. Replace its value with `"None"`.
4. See [this guide](guides/controls-and-hotkeys.md) for more detailed information about hotkeys.
---

## 🚗 Why does the radio stop playing when I exit my vehicle?

This is commonly caused by the vehicle's engine turning off when you exit. To keep the radio playing, use a mod that keeps the engine running after you leave the vehicle. I recommend [IV Style Exit Vehicle .NET (Optimized)](https://www.gta5-mods.com/scripts/iv-style-exit-vehicle-net-optimized). This solution has been confirmed to work with Realistic Vehicle Radio.

---

## 📻 Why do other vehicles' radios become audible when I turn off my own vehicle's radio? Why aren't they audible when my radio is on?

This is a behavior of GTA V's vanilla audio engine, not a bug caused by this script mod. The game appears to suppress the audio from other vehicles' radios while your own vehicle's radio is playing. Turning off your radio allows the other vehicles' radios to become audible again. Unfortunately, this behavior is controlled by the game's audio engine, and I have not found a way to override it.

---
