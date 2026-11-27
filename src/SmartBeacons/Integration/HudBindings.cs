using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SmartBeacons.Integration;

internal sealed class HudBindings
{
    private const ManagedUpdate.Queue BeforePings = ManagedUpdate.Queue.PreCanvasFirst;
    private const ManagedUpdate.Queue AfterPings = ManagedUpdate.Queue.PreCanvasTooltip;

    private readonly AccessTools.FieldRef<ManagedUpdate, Dictionary<ManagedUpdate.Queue, List<ManagedUpdate.OnUpdate>>> subscribers;
    private ManagedUpdate scheduler;

    public readonly AccessTools.FieldRef<uGUI_Pings, Dictionary<string, uGUI_Ping>> Markers;
    public readonly AccessTools.FieldRef<uGUI_Ping, Color> IconColor;
    public readonly Func<uGUI_Pings, bool> IsVisible;

    public HudBindings()
    {
        Markers = AccessTools.FieldRefAccess<uGUI_Pings, Dictionary<string, uGUI_Ping>>("pings");
        IconColor = AccessTools.FieldRefAccess<uGUI_Ping, Color>("_iconColor");
        IsVisible = AccessTools.MethodDelegate<Func<uGUI_Pings, bool>>(
            AccessTools.Method(typeof(uGUI_Pings), "IsVisibleNow", Type.EmptyTypes));
        subscribers = AccessTools.FieldRefAccess<ManagedUpdate, Dictionary<ManagedUpdate.Queue, List<ManagedUpdate.OnUpdate>>>("subscribers");
    }

    public void Subscribe(ManagedUpdate.OnUpdate restore, ManagedUpdate.OnUpdate apply)
    {
        scheduler = ManagedUpdate.main;
        ManagedUpdate.Subscribe(BeforePings, restore);
        ManagedUpdate.Subscribe(AfterPings, apply);
    }

    public void Unsubscribe(ManagedUpdate.OnUpdate restore, ManagedUpdate.OnUpdate apply)
    {
        // Unsubscribe uses the singleton getter, which could recreate the scheduler during shutdown.
        if (!scheduler)
            return;
        try { ManagedUpdate.Unsubscribe(BeforePings, restore); }
        finally
        {
            ManagedUpdate.Unsubscribe(AfterPings, apply);
            scheduler = null;
        }
    }

    public uGUI_Pings FindActiveHud()
    {
        if (!scheduler || !subscribers(scheduler).TryGetValue(ManagedUpdate.Queue.PreCanvasPing, out var updates))
            return null;
        foreach (ManagedUpdate.OnUpdate update in updates)
        {
            if (update.Target is uGUI_Pings hud && hud && hud.isActiveAndEnabled)
                return hud;
        }
        return null;
    }
}
