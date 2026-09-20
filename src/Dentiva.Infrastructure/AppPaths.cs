namespace Dentiva.Infrastructure;

/// <summary>
/// Resolves the on-disk layout of all Dentiva data. The default location is
/// %LOCALAPPDATA%\Dentiva; a custom directory can be supplied (setup,
/// selftest, portable installs). Nothing outside this root is ever written.
/// </summary>
public sealed class AppPaths
{
    public string Root { get; }
    public string DatabasePath => Path.Combine(Root, "dentiva.db");
    public string AttachmentsRoot => Path.Combine(Root, "attachments");
    public string LogsRoot => Path.Combine(Root, "logs");
    public string TempRoot => Path.Combine(Root, "tmp");

    public AppPaths(string? rootOverride = null)
    {
        Root = rootOverride is not null
            ? Path.GetFullPath(rootOverride)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Dentiva");
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(AttachmentsRoot);
        Directory.CreateDirectory(LogsRoot);
        Directory.CreateDirectory(TempRoot);
    }

    public long FreeDiskSpaceBytes()
    {
        try
        {
            var drive = DriveInfo.GetDrives()
                .Where(d => Root.StartsWith(d.RootPath, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(d => d.RootPath.Length)
                .FirstOrDefault();

            return drive?.AvailableFreeSpace ?? -1;
        }
        catch (IOException)
        {
            return -1;
        }
    }

    public long DatabaseSizeBytes() => File.Exists(DatabasePath) ? new FileInfo(DatabasePath).Length : 0;

    public long AttachmentsSizeBytes()
    {
        if (!Directory.Exists(AttachmentsRoot))
        {
            return 0;
        }

        try
        {
            return Directory.EnumerateFiles(AttachmentsRoot, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
        }
        catch (IOException)
        {
            return -1;
        }
    }

    public override string ToString() => Root;
}
