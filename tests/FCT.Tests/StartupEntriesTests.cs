using System.Diagnostics;
using FCT.App;

namespace FCT.Tests;

/// <summary>
/// The guard of the task and of the standby's startup entry: the exe runs while it exists; once it is deleted by hand, the
/// entry cleans up after it. Run for real through conhost and cmd, with a path full of spaces and parentheses.
/// </summary>
public sealed class StartupEntriesTests : IDisposable
{
    private readonly List<string> _folders = new();

    public void Dispose()
    {
        foreach (var folder in _folders)
        {
            try { Directory.Delete(folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <param name="folderName">With spaces (quoted all the way) and without (bare, its "&amp;" and parentheses escaped).</param>
    /// <param name="exeName">Same, for the exe.</param>
    [Theory]
    [InlineData("FCT tests (guard) & co", "FCT (1).exe")]
    [InlineData("FCT-tests(guard)&co", "FCT(1)&x.exe")]
    public void Guard_RunsWhileTheExeExists_AndCleansUpOnceItIsGone(string folderName, string exeName)
    {
        var folder = Path.Combine(Path.GetTempPath(), folderName + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);
        _folders.Add(folder);
        var exe = Path.Combine(folder, exeName);
        var ran = Path.Combine(folder, "ran(1).txt");
        var cleaned = Path.Combine(folder, "cleaned & done.txt");
        var arguments = StartupEntries.Guarded(exe, $"echo x> {StartupEntries.Arg(ran)}", $"echo x> {StartupEntries.Arg(cleaned)}")!;

        File.WriteAllText(exe, "");
        Run(arguments);
        Assert.True(File.Exists(ran));
        Assert.False(File.Exists(cleaned));

        File.Delete(exe);
        File.Delete(ran);
        Run(arguments);
        Assert.False(File.Exists(ran));
        Assert.True(File.Exists(cleaned));
    }

    [Fact]
    public void TaskAndStandbyEntry_GoThroughTheGuard_AndTheirExeIsStillFound()
    {
        var exe = @"C:\Games (x86)\FCT & co\FCT.exe";
        var (command, arguments) = StartupEntries.TaskAction(exe, "--on-game --data \"C:\\Data\\FCT\"");
        Assert.EndsWith("conhost.exe", command, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--on-game --data C:\\Data\\FCT", arguments); // bare: no space in it
        Assert.Contains("/Delete /TN", arguments);
        Assert.Equal(exe, StartupEntries.GuardedExe(arguments));

        var standby = StartupEntries.StandbyCommand(exe, "--data \"C:\\Data\\FCT\"");
        Assert.Contains("--standby", standby);
        Assert.Equal(exe, StartupEntries.GuardedExe(standby));

        var bare = @"C:\Games(x86)\FCT&co\FCT.exe";
        Assert.Equal(bare, StartupEntries.GuardedExe(StartupEntries.TaskAction(bare, "--on-game").Arguments));
    }

    [Fact]
    public void APathWithPercent_SkipsTheGuard()
    {
        var exe = @"C:\100%\FCT.exe";
        Assert.Equal((exe, "--on-game"), StartupEntries.TaskAction(exe, "--on-game"));
        Assert.Null(StartupEntries.GuardedExe(StartupEntries.StandbyCommand(exe, "")));
    }

    [Fact]
    public void RecycleBin_IsRecognised_WithItsRecord()
    {
        var thrown = @"C:\$Recycle.Bin\S-1-5-21-1\$RAB12CD.exe";
        Assert.True(ExeLocation.IsInRecycleBin(thrown));
        Assert.Equal(@"C:\$Recycle.Bin\S-1-5-21-1\$IAB12CD.exe", ExeLocation.RecycleBinRecord(thrown));
        Assert.False(ExeLocation.IsInRecycleBin(@"C:\Users\me\Downloads\FCT.exe"));
        Assert.Null(ExeLocation.RecycleBinRecord(@"C:\Users\me\Downloads\FCT.exe"));
    }

    private static void Run(string guardedArguments)
    {
        using var process = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "conhost.exe"), guardedArguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        Assert.True(process.WaitForExit(15_000));
    }
}
