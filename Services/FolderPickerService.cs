using System.Diagnostics;

namespace GitMergeAudit.Services;

public class FolderPickerService
{
    public async Task<string?> PickFolder()
    {
        string executable;
        string[] arguments;
        int cancellationExitCode;
        if (OperatingSystem.IsWindows())
        {
            executable = "powershell.exe";
            arguments = new[] {
                "-NoProfile", "-STA", "-Command",
                "Add-Type -AssemblyName System.Windows.Forms; $dialog = New-Object System.Windows.Forms.FolderBrowserDialog; $dialog.Description = 'Select Git repository folder'; if ($dialog.ShowDialog() -eq 'OK') { [Console]::Write($dialog.SelectedPath) }"
            };
            cancellationExitCode = -1;
        }
        else if (OperatingSystem.IsMacOS())
        {
            executable = "osascript";
            arguments = new[] {
                "-e",
                "try\nset selectedFolder to choose folder with prompt \"Select Git repository folder\"\nreturn POSIX path of selectedFolder\non error errorMessage number errorNumber\nif errorNumber is -128 then return \"\"\nerror errorMessage number errorNumber\nend try"
            };
            cancellationExitCode = 1;
        }
        else if (OperatingSystem.IsLinux())
        {
            executable = FindLinuxDialog();
            arguments = Path.GetFileName(executable) == "zenity"
                ? new[] { "--file-selection", "--directory", "--title=Select Git repository folder" }
                : new[] { "--getexistingdirectory" };
            cancellationExitCode = 1;
        }
        else
        {
            throw new PlatformNotSupportedException("Native folder selection is not supported on this operating system.");
        }

        var psi = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) psi.ArgumentList.Add(argument);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Unable to start the native folder picker.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(true);
            throw new TimeoutException("Folder selection exceeded five minutes.");
        }

        var selectedPath = (await stdout).Trim();
        var error = (await stderr).Trim();
        if (process.ExitCode == cancellationExitCode || process.ExitCode == 0 && selectedPath.Length == 0)
            return null;
        if (process.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrEmpty(error) ? "The native folder picker failed." : error);

        return selectedPath;
    }

    private static string FindLinuxDialog()
    {
        foreach (var candidate in new[] { "zenity", "kdialog" })
        {
            var executable = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(directory => Path.Combine(directory, candidate))
                .FirstOrDefault(File.Exists);
            if (executable is not null) return executable;
        }

        throw new InvalidOperationException("Folder selection requires zenity or kdialog on Linux.");
    }
}
