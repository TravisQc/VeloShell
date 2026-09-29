using System.IO;
using System.Text.Json;

namespace VeloShell.Services;

/// <summary>
/// Reads and writes JSON files atomically (temp file + rename) so a crash or
/// power loss mid-write cannot leave a half-written store on disk (design.md D3).
/// </summary>
public static class AtomicJson
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static void Write<T>(string path, T value)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Options));
        File.Move(tmp, path, overwrite: true);
    }

    public static T? Read<T>(string path)
    {
        if (!File.Exists(path))
            return default;
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options);
    }
}
