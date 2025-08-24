namespace YAESandBox.Launcher;

// DataModels.cs
using System.Collections.Generic;
using System.Text.Json.Serialization;

// 对应服务器上的 latest-version.json
public class LatestVersionInfo
{
    [JsonPropertyName("version")] public string Version { get; set; } = "0.0.0";

    [JsonPropertyName("manifestUrl")]
    public string ManifestUrl { get; set; } = string.Empty;
}

// 对应服务器上每个版本目录里的 manifest.json
public class Manifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "0.0.0";

    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; }= string.Empty;

    [JsonPropertyName("files")] public Dictionary<string, FileEntry> Files { get; set; } = [];
}

public class FileEntry
{
    [JsonPropertyName("hash")]
    public string Hash { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }
}


// 为需要序列化/反序列化的根类型添加特性
[JsonSerializable(typeof(LatestVersionInfo))]
[JsonSerializable(typeof(Manifest))]
// 如果你还需要序列化一个只有 version 属性的匿名类型，也需要为它创建一个具体的类
[JsonSerializable(typeof(VersionOnly))] 
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}

// 创建一个具体的类来代替之前用的匿名类型
public class VersionOnly
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "0.0.0";
}