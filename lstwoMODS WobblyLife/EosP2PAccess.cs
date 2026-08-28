using System;
using System.Reflection;
using Epic.OnlineServices;
using Epic.OnlineServices.P2P;
using HawkNetworking;

namespace lstwoMODS_WobblyLife;

/// <summary>
/// Reflective access to the EOS P2P handles the crossplay build keeps private on its connections.
/// <para>
/// Anything that wants to move bytes over EOS without going through the game's message pipeline
/// needs three things: the platform's <see cref="P2PInterface"/>, our own product user id, and the
/// socket the game opened. The game publishes none of them. They are private fields on
/// <c>HawkNetworking.EOSConnection</c>, a type that does not exist in the Steam build's
/// <c>HawkNetworking.dll</c>, so naming it at a call site would break that build the moment a
/// method touching it was JITted. It is reached by name at runtime instead, exactly as
/// <see cref="PlayerIdentity"/> reaches <c>remoteUserId</c>.
/// </para>
/// <para>
/// The <c>Epic.OnlineServices</c> types themselves are safe to name directly: they live in
/// <c>EOS.dll</c>, which ships with <b>both</b> game builds and with the GameLibs reference package.
/// Only the Hawk side of the crossplay transport is build specific.
/// </para>
/// </summary>
public static class EosP2PAccess
{
    private const string EosConnectionTypeName = "EOSConnection";

    private const string P2PInterfaceField = "p2PInterfaceHandle";
    private const string LocalUserIdField   = "localUserId";
    private const string RemoteUserIdField  = "remoteUserId";
    private const string SocketIdField      = "socketId";

    private static Type _boundType;
    private static FieldInfo _p2p;
    private static FieldInfo _localId;
    private static FieldInfo _remoteId;
    private static FieldInfo _socket;

    private static bool _bindFailed;
    private static bool _readFailed;

    /// <summary>
    /// True once the connection type was found but did not carry the fields this build expects,
    /// which is the one failure that cannot fix itself: a game update renamed them. Callers use it
    /// to fall back permanently rather than retrying every frame. Merely not being in a game yet is
    /// not a failure and does not set it.
    /// </summary>
    public static bool PermanentlyUnavailable => _bindFailed;

    /// <summary>
    /// The interface handle, our own id, and the socket the game is using, read off any live EOS
    /// connection. Every connection carries the same three: <c>EOSConnection</c>'s constructor takes
    /// them from statics. That matters because a client only ever holds the host's connection, so
    /// there is no "our own" connection to look for on that side.
    /// </summary>
    public static bool TryGetLocal(out P2PInterface p2p, out ProductUserId localUserId, out SocketId socketId)
    {
        p2p = null;
        localUserId = null;
        socketId = default;

        var connection = AnyEosConnection();
        if (connection == null || !Bind(connection.GetType())) return false;

        try
        {
            p2p = _p2p.GetValue(connection) as P2PInterface;
            localUserId = _localId.GetValue(connection) as ProductUserId;
            socketId = (SocketId)_socket.GetValue(connection);
        }
        catch (Exception e)
        {
            ReportReadFailure(e);
            return false;
        }

        return p2p != null && localUserId != null && localUserId.IsValid();
    }

    /// <summary>The product user id behind a connection, or null when it is not an EOS one.</summary>
    public static ProductUserId RemoteIdOf(HawkConnection connection)
    {
        if (connection == null || !Bind(connection.GetType())) return null;

        try
        {
            return _remoteId.GetValue(connection) as ProductUserId;
        }
        catch (Exception e)
        {
            ReportReadFailure(e);
            return null;
        }
    }

    /// <summary>
    /// Any EOS connection in the current game. Deliberately the first one found rather than
    /// <c>GetMe()</c>: the host's own connection is built with <c>Me = true</c>, but a client's
    /// roster holds the host's connection, and either serves equally here.
    /// </summary>
    private static HawkConnection AnyEosConnection()
    {
        var players = HawkNetworkManager.DefaultInstance?.GetPlayers();
        if (players == null) return null;

        for (var i = 0; i < players.Count; i++)
        {
            var connection = players[i];
            if (connection != null && connection.GetType().Name == EosConnectionTypeName)
                return connection;
        }

        return null;
    }

    private static bool Bind(Type type)
    {
        if (type == null || type.Name != EosConnectionTypeName) return false;

        if (ReferenceEquals(type, _boundType)) return !_bindFailed;

        _boundType = type;
        _p2p      = type.GetField(P2PInterfaceField, Plugin.Flags);
        _localId  = type.GetField(LocalUserIdField,  Plugin.Flags);
        _remoteId = type.GetField(RemoteUserIdField, Plugin.Flags);
        _socket   = type.GetField(SocketIdField,     Plugin.Flags);

        _bindFailed = _p2p == null || _localId == null || _remoteId == null || _socket == null;

        if (_bindFailed)
            Plugin.LogSource.LogWarning(
                $"[VoiceChat] {EosConnectionTypeName} does not carry the P2P fields this build expects "
              + "(the game was probably updated); voice falls back to the game's own channel.");

        return !_bindFailed;
    }

    /// <summary>Logs the first failure only: callers sit on paths that run every frame.</summary>
    private static void ReportReadFailure(Exception e)
    {
        if (_readFailed) return;

        _readFailed = true;
        Plugin.LogSource.LogWarning($"[VoiceChat] could not read the EOS P2P handles: {e.Message}");
    }
}
