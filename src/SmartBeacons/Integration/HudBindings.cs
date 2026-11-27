using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SmartBeacons.Integration;

internal sealed class HudBindings
{
    public readonly AccessTools.FieldRef<uGUI_Pings, Dictionary<string, uGUI_Ping>> Markers;
    public readonly AccessTools.FieldRef<uGUI_Ping, Color> IconColor;
    public readonly Func<uGUI_Pings, bool> IsVisible;
    public readonly System.Reflection.MethodInfo Update;
    public readonly System.Reflection.MethodInfo Disable;

    public HudBindings()
    {
        Markers = AccessTools.FieldRefAccess<uGUI_Pings, Dictionary<string, uGUI_Ping>>("pings");
        IconColor = AccessTools.FieldRefAccess<uGUI_Ping, Color>("_iconColor");
        IsVisible = AccessTools.MethodDelegate<Func<uGUI_Pings, bool>>(
            AccessTools.Method(typeof(uGUI_Pings), "IsVisibleNow", Type.EmptyTypes));
        Update = AccessTools.Method(typeof(uGUI_Pings), "UpdatePings", Type.EmptyTypes)
            ?? throw new MissingMethodException("uGUI_Pings", "UpdatePings");
        Disable = AccessTools.Method(typeof(uGUI_Pings), "OnDisable", Type.EmptyTypes)
            ?? throw new MissingMethodException("uGUI_Pings", "OnDisable");
    }
}
