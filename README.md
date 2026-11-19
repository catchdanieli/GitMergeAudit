# Git Branch Comparison (.NET 10)

A local, read-only, cross-platform .NET executable with a browser-based interface for comparing two Git branches.

## Prerequisites
- .NET 10 SDK to build and run with `dotnet`
- Git on PATH
- Linux folder browsing requires `zenity` or `kdialog` (the Browse button); macOS and Windows use their built-in folder dialogs.

## Run
```bash
dotnet run --project GitMergeAudit.csproj
```

Open http://localhost:5078 in your browser.

Enter an absolute path to a local Git repository, load its local and remote branches, select two branch names, and compare them.
The branch fields support typing to search the loaded branch names.

To publish a framework-dependent executable for the current system:
```bash
dotnet publish -c Release
```

For a different target system, specify its runtime identifier, for example `win-x64`, `linux-x64`, or `osx-arm64`:
```bash
dotnet publish -c Release -r <runtime-identifier> --self-contained false
```

## Features
- Finds the branches' unique common ancestor
- Lists all files changed from the common ancestor on either branch
- Shows whether each branch added, modified, or deleted each file; highlights files changed on both branches and can filter to common files only
- Keeps the file list in a side tray next to the selected file's diffs
- Lets you resize the file tray by dragging the divider or using its keyboard controls
- Shows line numbers, added/deleted counts, and each branch's changes side by side
- Displays copyable branch-tip and common-ancestor commit IDs for reproducible validation
- Labels each file's change type on both branches and provides copyable branch/ancestor commit IDs
- Synchronizes diff scrolling and provides previous/next file navigation
- Opens a native folder picker to choose the local repository path

## Limitations
- This is a comparison aid, not a correctness proof. Overlapping files merit review but are not necessarily conflicts.
- Only branch pairs with a unique merge base are supported.
- Renames are shown as deleted and added paths.
- Run locally only. Do not expose the HTTP service to untrusted networks; it accepts local repository paths.
