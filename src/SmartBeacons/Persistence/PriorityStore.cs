using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SmartBeacons.Persistence;

internal sealed class PriorityStore
{
    private const string FileName = "SmartBeacons.priorities.txt";
    private const int MaxEntries = 4096;
    private readonly HashSet<string> ids = new(StringComparer.Ordinal);
    private string directory;
    private bool dirty;

    public bool Ready => directory != null;
    public bool Contains(string id) => ValidId(id) && ids.Contains(id);

    public static bool ValidId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 256)
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

    public void Load(string saveDirectory)
    {
        Reset();
        if (string.IsNullOrWhiteSpace(saveDirectory))
            return;
        directory = Path.GetFullPath(saveDirectory);
        string file = Path.Combine(directory, FileName);
        if (!File.Exists(file))
            return;
        if (new FileInfo(file).Length > 2 * 1024 * 1024)
            throw new InvalidDataException("The priority sidecar exceeds the size limit.");

        var loaded = new HashSet<string>(StringComparer.Ordinal);
        foreach (string line in File.ReadLines(file, Encoding.UTF8))
        {
            if (ValidId(line))
                loaded.Add(line);
            if (loaded.Count == MaxEntries)
                break;
        }
        ids.UnionWith(loaded);
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
