using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SmartBeacons.Persistence;

internal sealed class PriorityStore
{
    private const string FileName = "SmartBeacons.priorities.txt";
    private const int MaxEntries = 4096;
    private const int MaxIdLength = 256;
    // UTF-8 uses at most three bytes per UTF-16 code unit, plus a CRLF per line.
    private const int MaxSidecarBytes = MaxEntries * (3 * MaxIdLength + 2);
    private readonly HashSet<string> ids = new(StringComparer.Ordinal);
    private string directory;
    private bool dirty;

    public bool Ready => directory != null;
    public bool Contains(string id) => id != null && ids.Contains(id);

    public static bool ValidId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > MaxIdLength)
            return false;
        foreach (char character in id)
        {
            if (char.IsControl(character))
                return false;
        }
        return true;
    }

    public void Reset()
    {
        ids.Clear();
        directory = null;
        dirty = false;
    }

    public bool TryLoad(string saveDirectory, out string warning)
    {
        Reset();
        if (string.IsNullOrWhiteSpace(saveDirectory))
        {
            warning = "Could not find the active save directory.";
            return false;
        }

        string fullDirectory = Path.GetFullPath(saveDirectory);
        string file = Path.Combine(fullDirectory, FileName);
        if (File.Exists(file))
        {
            using var stream = File.OpenRead(file);
            if (stream.Length > MaxSidecarBytes)
            {
                warning = "The priority sidecar exceeds the size limit.";
                return false;
            }

            var loaded = new HashSet<string>(StringComparer.Ordinal);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            while (loaded.Count < MaxEntries && reader.ReadLine() is string line)
            {
                if (ValidId(line))
                    loaded.Add(line);
            }
            ids.UnionWith(loaded);
        }

        directory = fullDirectory;
        warning = null;
        return true;
    }

    public bool Toggle(string id)
    {
        if (!Ready || !ValidId(id))
            return false;
        if (!ids.Remove(id))
        {
            if (ids.Count == MaxEntries)
                return false;
            ids.Add(id);
        }
        dirty = true;
        return true;
    }

    public void Save(string saveDirectory)
    {
        if (!dirty || !Ready || string.IsNullOrWhiteSpace(saveDirectory) ||
            !string.Equals(directory, Path.GetFullPath(saveDirectory), StringComparison.Ordinal))
            return;

        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, FileName);
        string temporary = file + ".tmp";
        var sorted = new List<string>(ids);
        sorted.Sort(StringComparer.Ordinal);
        try
        {
            File.WriteAllLines(temporary, sorted, new UTF8Encoding(false));
            if (File.Exists(file))
                File.Replace(temporary, file, null);
            else
                File.Move(temporary, file);
            dirty = false;
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }
}
