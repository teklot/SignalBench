using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.IO;

namespace SignalBench.Core.Session;

public class SessionManager
{
    private readonly ISerializer _serializer;
    private readonly IDeserializer _deserializer;

    public SessionManager()
    {
        _serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public void SaveSession(string path, ProjectSession session)
    {
        var yaml = _serializer.Serialize(session);
        File.WriteAllText(path, yaml);
    }

    public ProjectSession LoadSession(string path)
    {
        var yaml = File.ReadAllText(path);
        return _deserializer.Deserialize<ProjectSession>(yaml) ?? new ProjectSession();
    }
}
