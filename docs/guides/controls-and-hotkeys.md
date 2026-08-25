# Controls & Hotkeys

Realistic Vehicle Radio uses a customizable hotkey system to allow quick access to recording, debugging, and menu features without needing to edit JSON files manually.

## Default Hotkeys

| Key | Function | Description |
| :--- | :--- | :--- |
| **F3** | Open Menu | Opens the main LemonUI configuration menu. |
| **F6** | Toggle Debug Visualizer | Turns the spatial debug drawing ON/OFF to see tunnel boundaries. |
| **F7** | Toggle Tunnel Recorder | Starts or stops the automatic drive-recording process. |

## How to Change Hotkeys

You can rebind or disable any hotkey using two methods:

### Method 1: In-Game Menu (Recommended)
1. Press **F3** to open the menu.
2. Navigate to the specific settings menu (e.g., *Settings -> Debug Settings* or *Settings -> Tunnel Recorder*).
3. Select the keybinding item and press **Enter**.
4. A full-screen overlay will appear. Press the new key you want to assign.
5. Navigate to **Save Changes** and press Enter to apply.
![Description](../images/keybinding-overlay.png)

### Method 2: Editing the JSON File
1. Navigate to `scripts/Realistic Vehicle Radio/`.
2. Open `Realistic Vehicle Radio Settings.json` in a text editor like Notepad++ or VS Code.
3. Find the key you want to change (e.g., `"OpenMenuKey": "F3"`).
4. Replace the value with a valid `System.Windows.Forms.Keys` name (e.g., `"F4"`, `"NumPad0"`, `"Insert"`). See [Valid Keys](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.keys?view=windowsdesktop-10.0)
5. Save the file and reload the script in-game to apply changes.

## How to Disable a Hotkey

If a hotkey conflicts with another mod, you can disable it:
- **In-Game:** Scroll through the in-game menu list until you select **"None"**, then save.
- **JSON:** Change the value to `"None"` (e.g., `"DebugToggleKey": "None"`).