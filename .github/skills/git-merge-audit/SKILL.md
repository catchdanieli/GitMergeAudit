---
name: git-merge-audit
description: 'Use when changing, debugging, testing, or extending the Git Merge Audit branch comparison app, its Git analysis API, native folder picker, or browser UI.'
---

# Git Merge Audit

Maintain this cross-platform .NET web application for comparing changes on two Git branches.

## Project map

- `Program.cs` defines the ASP.NET Core endpoints and request records.
- `Services/GitAuditService.cs` lists branches, resolves branch refs, finds the unique merge base, classifies changed paths, and generates branch diffs.
- `Services/FolderPickerService.cs` invokes the operating system folder dialog (Windows/macOS) or `zenity`/`kdialog` (Linux).
- `wwwroot/index.html` contains the browser UI, styles, and client-side behavior.

## Workflow

1. Read the relevant endpoint, service, and UI code before changing behavior. Keep the API response shape and UI rendering consistent.
2. Keep Git operations read-only. Invoke Git with `ProcessStartInfo.ArgumentList`; do not build shell command strings.
3. Treat selected branch names as refs from the repository's local or remote branch list. Compare both branch tips against their unique merge base.
4. Preserve UI usability: file paths should wrap in the resizable tray, common-file filtering should reflect changes on both branches, and the two diff panes should retain line numbers and synchronized scrolling.
5. For UI changes, validate the embedded JavaScript with `node --check` and use the browser to exercise the affected interactions when available.
6. Run `dotnet build --no-restore` after C# or project changes. Test branch analysis and file diffs against a local Git repository when API behavior changes.
7. Update `README.md` when prerequisites, run instructions, platform behavior, or user-facing features change.

## Constraints

- The application is a read-only comparison aid; never modify the selected repository.
- Preserve explicit errors for invalid repositories, branches, ambiguous merge bases, and unavailable platform folder-picker tools.
- Keep platform-specific functionality working on Windows, macOS, and Linux; document external Linux dependencies.
