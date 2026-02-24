using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SignalBench.Core.Session;

public class SessionManager
{
    private readonly ISerializer _serializer;
    private readonly IDeserializer _deserializer;

    public SessionManager()
    {
        _serializer = new SerializerBuilder()
            .Build();

        _deserializer = new DeserializerBuilder()
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
        return _deserializer.Deserialize<ProjectSession>(yaml);
    }
}
