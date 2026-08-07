using System;
using HawkNetworking;
using lstwoMODS.ImGui.Shared.UI;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using UnityEngine;

namespace lstwoMODS_WobblyLife;

public sealed class ModNetworkIndicator : TextColored
{
    private readonly Func<bool> _isReady;
    private int _lastState = int.MinValue;

    private static readonly Color Active  = new(0.40f, 0.85f, 0.45f); // green  channel is live
    private static readonly Color Warn    = new(0.95f, 0.80f, 0.30f); // amber  you host, but no network manager here
    private static readonly Color Missing = new(0.95f, 0.60f, 0.30f); // orange  connected, host has no compatible mod
    private static readonly Color Offline = new(0.60f, 0.60f, 0.62f); // gray  not in a game

    public ModNetworkIndicator(string id, Func<bool> isReady) : base(id, "", Offline)
    {
        _isReady = isReady ?? throw new ArgumentNullException(nameof(isReady));
        Apply();
    }

    public void Tick() => Apply();

    private void Apply()
    {
        // InstanceExists first: DefaultInstance builds the network singleton on demand, and this
        // runs every frame from the main menu onwards. Forcing it into existence early pins the
        // transport type before the LAN Multiplayer mod has chosen one.
        var manager = HawkNetworkManager.InstanceExists ? HawkNetworkManager.DefaultInstance : null;
        var host = manager != null && manager.IsServer();
        var inGame = manager != null && manager.IsConnected();
        var ready = _isReady();

        int state;
        string label;
        Color color;

        if (ready)
        {
            state = host ? 1 : 2;
            label = $"{Lucide.Wifi} Networked";
            color = Active;
        }
        else if (host)
        {
            state = 3;
            label = $"{Lucide.WifiOff} Not networked";
            color = Warn;
        }
        else if (inGame)
        {
            state = 4;
            label = $"{Lucide.WifiOff} Not networked";
            color = Missing;
        }
        else
        {
            state = 5;
            label = $"{Lucide.WifiOff} Offline";
            color = Offline;
        }

        if (state == _lastState) return;
        _lastState = state;

        var data = (TextData)Data;
        data.Text = label;
        data.R = color.r;
        data.G = color.g;
        data.B = color.b;
        data.A = color.a;
        MarkChanged();
    }
}
