# Installation Guide

This guide explains how to install Realistic Vehicle Radio v1.0.0.

The mod supports:

* Grand Theft Auto V Legacy

  * Script Hook V .NET (nightly)
  * Script Hook V .NET Enhanced (SHVDNE)
* Grand Theft Auto V Enhanced

  * Script Hook V .NET Enhanced (SHVDNE)

---

# 📦 Folder Structure

```text
RealisticVehicleRadio_v1.0.0
├── For Grand Theft Auto V Enhanced\
├── For Grand Theft Auto V Legacy\
│   ├── For Script Hook V .NET (nightly)\
│   └── For Script Hook V .NET Enhanced\
└── Install Helper.bat
```

---

# ⭐ Recommended Installation Method

## Use `Install Helper.bat`

The archive includes:

```text
Install Helper.bat
```

This helper can:

* Automatically detect:

  * Your GTA V edition
  * Your Script Hook V .NET version
* Tell you exactly which folder to install
* Provide a manual selection mode if automatic detection fails

### Automatic Detection Checks

The helper detects:

## GTA V Version

Using `GTA5.exe` file version:

* `1.0.3.x` → GTA V Legacy
* `1.0.1.x` → GTA V Enhanced

## Script Hook V .NET Version

Using `ScriptHookVDotNet3.dll` file version:

* `3.7.0.x` → Script Hook V .NET (nightly)
* `3.9.0.x` → Script Hook V .NET Enhanced (SHVDNE)

---

# 📌 Step 1 — Install Script Hook V

1. Download:
   http://www.dev-c.com/gtav/scripthookv/

2. Open the downloaded archive

3. Navigate to the `bin` folder

4. Extract these files into your GTA V directory
   (where `GTA5.exe` is located):

```text
dinput8.dll
ScriptHookV.dll
xinput1_4.dll
args.txt
```

> Ignore the "for developers" files unless you specifically need them.

---

# 📌 Step 2 — Install Script Hook V .NET

## For GTA V Legacy

You can install ONE of the following:

### Option A — Script Hook V .NET (nightly)

Download:
https://github.com/scripthookvdotnet/scripthookvdotnet-nightly/releases

### Option B — Script Hook V .NET Enhanced (SHVDNE)

Download:
https://www.gta5-mods.com/tools/script-hook-v-net-enhanced

> IMPORTANT:
> Do NOT install both versions at the same time.

---

## For GTA V Enhanced

You MUST install:

### Script Hook V .NET Enhanced (SHVDNE)

Download:
https://www.gta5-mods.com/tools/script-hook-v-net-enhanced

---

# 📌 Step 3 — Install .NET Framework 4.8

Download:
https://dotnet.microsoft.com/en-us/download/dotnet-framework/thank-you/net48-web-installer

Run the installer and follow the instructions.

---

# 📌 Step 4 — Create the `scripts` Folder

Inside your GTA V directory:

* Ensure a folder named:

```text
scripts
```

exists.

If it does not exist, create it using the exact same name.

---

# 📌 Step 5 — Install Realistic Vehicle Radio

## Method A — Automatic (Recommended)

1. Run:

```text
Install Helper.bat
```

2. Choose:

```text
[1] Automatic Detection
```

3. Enter your GTA V directory path

4. The helper will tell you exactly which folder to install

---

## Method B — Manual Installation

Choose the correct folder based on your setup:

| GTA V Version  | SHVDN Version                | Install Folder                                                   |
| -------------- | ---------------------------- | ---------------------------------------------------------------- |
| GTA V Legacy   | Script Hook V .NET (nightly) | `For Grand Theft Auto V Legacy\For Script Hook V .NET (nightly)` |
| GTA V Legacy   | Script Hook V .NET Enhanced  | `For Grand Theft Auto V Legacy\For Script Hook V .NET Enhanced`  |
| GTA V Enhanced | SHVDNE                       | `For Grand Theft Auto V Enhanced`                                |

Then:

1. Open the selected folder
2. Extract ALL files into:

```text
GTA V\scripts\
```

---

# ⚠️ Important Notes

* The correct build depends on BOTH:

  * Your GTA V edition
  * Your Script Hook V .NET version

* GTA V Legacy users can use:

  * Script Hook V .NET (nightly)
  * OR SHVDNE

* GTA V Enhanced users must use:

  * SHVDNE only

* Installing the wrong build may cause:

  * The script not loading
  * Runtime errors
  * Missing menu behavior
  * Script incompatibility

* If automatic detection fails, use Manual Installation mode.

* If `ScriptHookVDotNet3.dll` is missing:

  * Script Hook V .NET is not installed yet
  * OR installed incorrectly

* Compatibility may change depending on future GTA V updates or Script Hook V .NET updates.