namespace lstwoMODS_WobblyLife;

/// <summary>Which of the game's two shipped builds we are running inside.</summary>
public enum GameBuildKind
{
    /// <summary><c>Wobbly Life.exe</c>: Steam P2P transport, Steam Workshop mods.</summary>
    Steam,

    /// <summary><c>Wobbly Life_EOS.exe</c>: Epic Online Services transport, crossplay, no Workshop.</summary>
    Crossplay,
}

/// <summary>
/// Tells the two game builds apart at runtime.
/// <para>
/// Both exes sit in one folder and share a single BepInEx install (one <c>winhttp.dll</c>, one
/// <c>doorstop_config.ini</c>, one plugins folder), so the same plugin assembly is loaded into
/// whichever one the user launched. Anything that differs between them has to be decided here
/// rather than at build time.
/// </para>
/// <para>
/// The probe walks <see cref="SteamP2PNetworkManager"/>'s base chain. That type exists in both
/// builds, but only the crossplay build slots <c>EOSNetworkManager</c> in between it and
/// <c>HawkNetworkManager</c>. Matching on the name rather than the type keeps this file free of any
/// reference to an EOS type, which would fail to resolve on the Steam build.
/// </para>
/// </summary>
public static class GameBuild
{
    private const string EosManagerTypeName = "EOSNetworkManager";

    private static GameBuildKind? _kind;

    public static GameBuildKind Kind => _kind ??= Probe();

    /// <summary>Steam P2P build: connections are <c>SteamConnection</c>, peers have Steam ids.</summary>
    public static bool IsSteam => Kind == GameBuildKind.Steam;

    /// <summary>
    /// Crossplay build: every connection is an <c>EOSConnection</c> and traffic runs over EOS P2P.
    /// A Steam lobby is still created, but only as an invite surface: the game never accepts a Steam
    /// P2P session here, so nothing can be carried over one.
    /// </summary>
    public static bool IsCrossplay => Kind == GameBuildKind.Crossplay;

    /// <summary>Whether the game can download Workshop mods. The crossplay build strips that path
    /// entirely and hides the in-world mod machine.</summary>
    public static bool HasWorkshop => IsSteam;

    /// <summary>Human readable build name, for logs and the UI.</summary>
    public static string DisplayName => IsCrossplay ? "Crossplay (EOS)" : "Steam";

    private static GameBuildKind Probe()
    {
        for (var type = typeof(SteamP2PNetworkManager).BaseType; type != null; type = type.BaseType)
            if (type.Name == EosManagerTypeName)
                return GameBuildKind.Crossplay;

        return GameBuildKind.Steam;
    }
}
