using System.Diagnostics;
using System.Text.Json;

static class ReleasePackageTests
{
    public static void PrepareIsolatedStartup(string directory)
    {
        var full = Path.GetFullPath(directory);
        var temporary = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(temporary, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(full) || Directory.EnumerateFileSystemEntries(full).Any())
            throw new InvalidOperationException("Startup fixtures require a new empty directory strictly inside TEMP.");
        var user = new SkyrimRpEncyclopedia.Services.DatabaseService(Path.Combine(full, "user.db"));
        var official = new SkyrimRpEncyclopedia.Services.OfficialDatabaseService(Path.Combine(full, "official.db"));
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Console.WriteLine("Isolated startup fixtures created: " + full);
    }

    public static void Run(string? packagePath = null)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "SkyrimRpEncyclopedia.csproj"))) root = root.Parent;
        if (root is null) throw new DirectoryNotFoundException("Release package tests require checkout.");
        packagePath ??= Path.Combine(root.FullName, "bin", "Release", "net10.0-windows10.0.17763.0");
        var script = Path.Combine(root.FullName, "Scripts", "Test-ReleasePackage.ps1");
        var checks = 0;
        void Check(bool ok, string message) { checks++; if (!ok) throw new InvalidOperationException("Release package: " + message); }
        (int Code, string Output) Validate(string path)
        {
            var start = new ProcessStartInfo("pwsh") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-File", script, "-PackagePath", path }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Validator process not started.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000)) throw new TimeoutException("Read-only package validation exceeded 30 seconds.");
            return (process.ExitCode, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
        }
        var full = Validate(packagePath);
        Check(full.Code == 0, "Complete package rejected: " + full.Output);
        var folder = Path.Combine(Path.GetTempPath(), "skyrim-release-assets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            // Copy only binaries/manifests, never a database, log or personal profile.
            foreach (var file in Directory.EnumerateFiles(packagePath, "*", SearchOption.AllDirectories))
            {
                if (!new[] { ".dll", ".exe", ".json", ".so", ".dylib", ".a" }.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)) continue;
                var target = Path.Combine(folder, Path.GetRelativePath(packagePath, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
            // Native paths differ between framework-dependent and self-contained publish.
            // Use the same manifest contract as the validator, not a guessed layout.
            using var deps = JsonDocument.Parse(File.ReadAllText(Path.Combine(packagePath, "SkyrimRpEncyclopedia.deps.json")));
            var targetName = deps.RootElement.GetProperty("runtimeTarget").GetProperty("name").GetString()!;
            var nativeAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var library in deps.RootElement.GetProperty("targets").GetProperty(targetName).EnumerateObject())
            foreach (var kind in new[] { "native", "runtimeTargets" })
            {
                if (!library.Value.TryGetProperty(kind, out var assets)) continue;
                foreach (var asset in assets.EnumerateObject())
                    if (Path.GetFileName(asset.Name).Equals("e_sqlite3.dll", StringComparison.OrdinalIgnoreCase))
                        nativeAssets.Add(kind == "runtimeTargets" ? asset.Name.Replace('/', Path.DirectorySeparatorChar) : Path.GetFileName(asset.Name));
            }
            Check(nativeAssets.Count > 0, "Native SQLite is not declared in the dependency manifest.");
            foreach (var relative in new[] { "Microsoft.Data.Sqlite.dll", "SkyrimRpEncyclopedia.deps.json" }.Concat(nativeAssets.OrderBy(p => p, StringComparer.Ordinal)))
            {
                var file = Path.Combine(folder, relative);
                Check(File.Exists(file), "Negative test target does not exist: " + relative);
                File.Delete(file);
                var invalid = Validate(folder);
                Check(invalid.Code != 0 && invalid.Output.Contains(Path.GetFileName(relative), StringComparison.OrdinalIgnoreCase), "Missing asset accepted or unexplained: " + relative);
                File.Copy(Path.Combine(packagePath, relative), file);
            }
            File.WriteAllText(Path.Combine(folder, "SkyrimRpEncyclopedia.runtimeconfig.json"), "{}");
            Check(Validate(folder).Code != 0, "Missing Desktop runtime declaration accepted.");
        }
        finally { Directory.Delete(folder, true); }
        Console.WriteLine($"Release package OK: {checks} positive/negative dependency checks; no DB access or application launch.");
    }
}
