using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace UpscaylVideo.Helpers;

public static class ConfigurationHelper
{
    private static readonly string _appDataFolder;

    static ConfigurationHelper()
    {
        _appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), nameof(UpscaylVideo));
        Directory.CreateDirectory(_appDataFolder);
    }

    public static T LoadConfig<T>(JsonTypeInfo<T>? typeInfo = null)
        where T : new()
    {
        var type = typeof(T);
        var path = Path.Combine(_appDataFolder, $"{type.Name}.json");
        if (!File.Exists(path))
            return new T();
        try
        {
            using var stream = File.OpenRead(path);
            return (typeInfo is null
                ? JsonSerializer.Deserialize<T>(stream)
                : JsonSerializer.Deserialize<T>(stream, typeInfo)) ?? new T();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
        }

        return new T();
    }

    public static void SaveConfig<T>(T value, JsonTypeInfo<T>? typeInfo = null)
    {
        var type = typeof(T);
        var path = Path.Combine(_appDataFolder, $"{type.Name}.json");
        try
        {
            using var stream = File.OpenWrite(path);
            if (typeInfo is null)
                JsonSerializer.Serialize(stream, value);
            else
                JsonSerializer.Serialize(stream, value, typeInfo);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
        }
    }
}