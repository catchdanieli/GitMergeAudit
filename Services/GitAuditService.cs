using System.Diagnostics;
namespace GitMergeAudit.Services;
public class GitAuditService
{
    private static async Task<string> Git(string repo, params string[] args)
    {
        if (!Directory.Exists(repo)) throw new ArgumentException("Repository directory does not exist.");
        var psi = new ProcessStartInfo("git") { WorkingDirectory = repo, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        psi.ArgumentList.Add("--no-pager");
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Unable to start git. Install Git and add it to PATH.");
        var stdout = p.StandardOutput.ReadToEndAsync(); var stderr = p.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try { await p.WaitForExitAsync(timeout.Token); } catch (OperationCanceledException) { try { p.Kill(true); } catch { } throw new TimeoutException("Git operation exceeded 45 seconds."); }
        var output = await stdout; var error = await stderr;
        if (p.ExitCode != 0) throw new InvalidOperationException(error.Trim());
        return output;
    }
    private static async Task<Dictionary<string, string>> Changed(string repo, string from, string to)
    {
        var fields = (await Git(repo, "diff", "--no-renames", "--name-status", "-z", from, to))
            .Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var changes = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index + 1 < fields.Length; index += 2)
            changes.Add(fields[index + 1], ChangeType(fields[index]));
        return changes;
    }

    private static string ChangeType(string status) => status switch
    {
        "A" => "Added",
        "D" => "Deleted",
        "M" => "Modified",
        "T" => "Type changed",
        "U" => "Unmerged",
        _ => $"Status {status}"
    };
    public async Task<string[]> GetBranches(string repository)
    {
        var repo = Path.GetFullPath(repository);
        await Git(repo, "rev-parse", "--show-toplevel");
        var branches = (await Git(repo, "for-each-ref", "--format=%(refname:short)", "refs/heads", "refs/remotes"))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(branch => !branch.EndsWith("/HEAD", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(branch => branch, StringComparer.Ordinal)
            .ToArray();
        if (branches.Length == 0) throw new InvalidOperationException("No local or remote branches were found in this repository.");
        return branches;
    }

    public async Task<object> Analyze(AuditRequest req)
    {
        var repo = Path.GetFullPath(req.Repository);
        await Git(repo, "rev-parse", "--show-toplevel");
        var firstBranch = req.FirstBranch?.Trim();
        var secondBranch = req.SecondBranch?.Trim();
        if (string.IsNullOrWhiteSpace(firstBranch) || string.IsNullOrWhiteSpace(secondBranch))
            throw new ArgumentException("Select two branches.");

        var firstTip = await ResolveBranch(repo, firstBranch);
        var secondTip = await ResolveBranch(repo, secondBranch);
        if (firstTip == secondTip) throw new ArgumentException("Select two branches with different commits.");

        var bases = (await Git(repo, "merge-base", "--all", firstTip, secondTip)).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (bases.Length == 0) throw new InvalidOperationException("The selected branches have no common ancestor.");
        if (bases.Length != 1) throw new InvalidOperationException("Multiple merge bases found; automatic comparison is ambiguous.");
        var ancestor = bases[0];
        var first = await Changed(repo, ancestor, firstTip);
        var second = await Changed(repo, ancestor, secondTip);
        var all = first.Keys.Union(second.Keys).OrderBy(path => path, StringComparer.Ordinal).Select(path => new {
            path, inFirst = first.ContainsKey(path), inSecond = second.ContainsKey(path),
            firstStatus = first.GetValueOrDefault(path), secondStatus = second.GetValueOrDefault(path),
            overlap = first.ContainsKey(path) && second.ContainsKey(path)
        }).ToArray();
        return new {
            firstBranch, secondBranch, firstTip, secondTip, ancestor, files = all,
            summary = new { total = all.Length, overlap = all.Count(file => file.overlap) }
        };
    }

    private static async Task<string> ResolveBranch(string repo, string branch)
    {
        var refs = (await Git(repo, "for-each-ref", "--format=%(refname:short)", "refs/heads", "refs/remotes"))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(name => !name.EndsWith("/HEAD", StringComparison.Ordinal));
        if (!refs.Contains(branch, StringComparer.Ordinal))
            throw new ArgumentException($"'{branch}' is not a local or remote branch.");

        var tip = (await Git(repo, "rev-parse", "--verify", "--end-of-options", branch + "^{commit}")).Trim();
        return tip;
    }

    public async Task<object> Diff(DiffRequest req)
    {
        var repo = Path.GetFullPath(req.Repository);
        await Git(repo, "rev-parse", "--show-toplevel");
        var firstBranch = req.FirstBranch?.Trim();
        var secondBranch = req.SecondBranch?.Trim();
        if (string.IsNullOrWhiteSpace(firstBranch) || string.IsNullOrWhiteSpace(secondBranch))
            throw new ArgumentException("Select two branches.");
        var firstTip = await ResolveBranch(repo, firstBranch);
        var secondTip = await ResolveBranch(repo, secondBranch);
        var bases = (await Git(repo, "merge-base", "--all", firstTip, secondTip)).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (bases.Length == 0) throw new InvalidOperationException("The selected branches have no common ancestor.");
        if (bases.Length != 1) throw new InvalidOperationException("Ambiguous merge base.");
        var path = req.Path;
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('-') || path.Contains('\0')) throw new ArgumentException("Invalid path.");
        return new {
            firstDiff = await Git(repo, "diff", "--no-ext-diff", "--no-renames", "--unified=3", bases[0], firstTip, "--", path),
            secondDiff = await Git(repo, "diff", "--no-ext-diff", "--no-renames", "--unified=3", bases[0], secondTip, "--", path)
        };
    }
}
