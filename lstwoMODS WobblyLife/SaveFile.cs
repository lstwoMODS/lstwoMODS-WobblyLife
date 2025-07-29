using System.Reflection;
using Newtonsoft.Json.Linq;

namespace lstwoMODS_WobblyLife;

public static class SaveFile
{
    public static string FullPath => Assembly.GetEntryAssembly().Location;
    public static string RelativePath { get; private set; } = "lstwoMODS/wobbly_life/data.json";
    public static JObject data;
}