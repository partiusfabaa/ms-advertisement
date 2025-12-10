using System.Text.Json.Serialization;

namespace Advertisement;

public class Config : List<Advertisement>;

public class Advertisement
{
    public float Interval { get; set; }
    public List<Dictionary<Destination, string>> Messages { get; set; }

    private int _currentMessageIndex;

    [JsonIgnore]
    public Dictionary<Destination, string> NextMessages => Messages[_currentMessageIndex++ % Messages.Count];
}

public enum Destination : byte
{
    Chat = 0,
    Center = 1
}