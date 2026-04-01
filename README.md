# <img src="NoteStitch/Assets/notes.png" width="32" height="32" valign="middle"> NoteStitch

> Stitch multiple Notepad windows into one document — instantly.

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D6?style=flat-square&logo=windows)](https://www.microsoft.com/windows)
[![WinForms](https://img.shields.io/badge/UI-WinForms-blueviolet?style=flat-square)]()
[![License](https://img.shields.io/badge/license-MIT-green?style=flat-square)](LICENSE)

---

## What is it?

You have six Notepad windows open. Meeting notes, a code snippet, a half-written email, some random thoughts. You need it all in one place — **right now**.

NoteStitch detects every open Notepad window, lets you pick which ones to include, and merges them into a single clean document with labeled sections. Copy it, save it, done.

---

## Features

| | |
|---|---|
| 🔍 **Auto-detect** | Listens to Notepad via Windows events — no polling, no refresh button |
| ☑️ **Pick & choose** | Checkbox list with live character counts so you know what you're including |
| 🔀 **Merge** | Combines selected documents with clear `=== Filename ===` section headers |
| 📋 **Copy / Save** | One-click copy to clipboard or save to file |
| 🗑️ **Close originals** | Optionally close the source Notepad windows after merging |
| ⌨️ **Hotkey launch** | Assign a `Ctrl + Alt + ?` shortcut to open NoteStitch from anywhere |

---

## How it works

```
  [Notepad]  [Notepad]  [Notepad]
      │           │           │
      └─────┬─────┘           │
            │◄────────────────┘
            ▼
       NoteStitch
       ┌──────────────────────────┐
       │ ☑ meeting-notes  (312 ch)│
       │ ☑ ideas          (88 ch) │
       │ ☐ scratch        (5 ch)  │
       └──────────────────────────┘
            │  [Merge ▶]
            ▼
   === meeting-notes ===
   Discussed Q3 roadmap...

   === ideas ===
   What if we used WinEvents...
```

---

## Getting started

### Download

Grab the latest single-file executable from [Releases](../../releases) — no installer, no runtime required.

### Build from source

```bash
git clone https://github.com/your-username/NoteStitch.git
cd NoteStitch
dotnet publish NoteStitch/NoteStitch.csproj -c Release
```

> Requires [.NET 10 SDK](https://dotnet.microsoft.com/download) and Windows.

---

## Usage

1. **Open** some Notepad windows with content
2. **Launch** NoteStitch — it detects them automatically
3. **Check** the windows you want to include
4. **Click** `Merge ▶`
5. Copy to clipboard, save to file, or close the originals

### Keyboard shortcut (optional)

Click **⌨ Shortcut…** to register a `Ctrl + Alt + ?` global hotkey that launches NoteStitch from anywhere. Sign out and back in once for Windows to register it.

---

## Technical notes

- Uses **`SetWinEventHook`** (`EVENT_OBJECT_SHOW`, `EVENT_OBJECT_DESTROY`, `EVENT_OBJECT_NAMECHANGE`) to react to Notepad windows opening, closing, and being renamed — no polling loop
- Reads content via **`WM_GETTEXT`** sent to the child `Edit` control of each Notepad window
- Ships as a **self-contained single `.exe`** (win-x64) — nothing to install

---

## License

MIT — do whatever you want with it.
