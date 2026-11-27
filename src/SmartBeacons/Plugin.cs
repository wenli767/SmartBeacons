using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using Nautilus.Handlers;
using Nautilus.Utility;
using UnityEngine;

namespace SmartBeacons;

using Config;
using Integration;
using Persistence;

[BepInPlugin(Id, "Smart Beacons", "0.5.0.4")]
[BepInDependency(NautilusId, "1.0.0.57")]
[BepInProcess("Subnautica.exe")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Id = "com.smartbeacons.subnautica";
    private const string NautilusId = "com.snmodding.nautilus";

    private readonly PriorityStore priorities = new();
    private OptionsMenu settings;
    private GameInputAdapter input;
    private HudController hud;
    private HudBindings hudBindings;
    private bool loaded, hudFaulted;

    private void Awake()
    {
        try
        {
            settings = new OptionsMenu(Config, Logger);
            OptionsPanelHandler.RegisterModOptions(settings);
            string version = GetType().Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attribute => attribute.Key == "SmartBeaconsVersion")?.Value ?? Info.Metadata.Version.ToString();
            string nautilus = Chainloader.PluginInfos.TryGetValue(NautilusId, out var dependency)
                ? dependency.Metadata.Version.ToString() : "unknown";
            Logger.LogInfo($"Smart Beacons {version} | Subnautica {Application.version} | Unity {Application.unityVersion} | " +
                $"BepInEx {typeof(BaseUnityPlugin).Assembly.GetName().Version} | Nautilus {nautilus}");

            hudBindings = new HudBindings();
            try { input = new GameInputAdapter(Logger); }
            catch (Exception ex) { Logger.LogWarning($"Smart Beacons could not initialize hotkeys: {ex.Message}"); }
            hud = new HudController(hudBindings, input, priorities, Logger);
            hudBindings.Subscribe(BeforePingUpdate, AfterPingUpdate);
            PingManager.onRemove += OnPingRemoved;

            settings.Changed += OnSettingsChanged;
            WaitScreenHandler.RegisterEarlyLoadTask("Smart Beacons", _ => { if (loaded) LoadPriorities(); }, "Loading beacon priorities");
            SaveUtils.RegisterOnSaveEvent(SavePriorities);
            SaveUtils.RegisterOnQuitEvent(LeaveSave);
            loaded = true;
            Logger.LogInfo("Smart Beacons HUD callbacks registered around the vanilla ping update.");
            if (settings.Current.Debug)
                LogCompatibility();
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Smart Beacons HUD integration could not initialize. Vanilla HUD will be used. {ex}");
            StopHud();
        }
    }

    private void BeforePingUpdate()
    {
        if (loaded && hud != null)
            Safely("restore vanilla HUD", hud.Restore);
    }

    private void AfterPingUpdate()
    {
        if (!loaded || hud == null || hudFaulted || !settings.Current.Active)
            return;
        try { hud.Update(hudBindings.FindActiveHud(), settings.Current); }
        catch (Exception ex) { FailHud("update navigation HUD", ex); }
    }

    private void OnPingRemoved(string id)
    {
        if (!loaded || hud == null || hudFaulted)
            return;
        try { hud.Remove(id); }
        catch (Exception ex) { FailHud("restore a pooled navigation marker", ex); }
    }

    private void OnSettingsChanged()
    {
        if (!hudFaulted && hud != null)
        {
            try { hud.SettingsChanged(settings.Current.Active); }
            catch (Exception ex) { FailHud("apply settings", ex); }
        }
        if (settings.Current.Debug)
            LogCompatibility();
    }

    private bool compatibilityLogged;

    private void LogCompatibility()
    {
        if (compatibilityLogged)
            return;
        compatibilityLogged = true;
        var owners = Harmony.GetPatchInfo(AccessTools.Method(typeof(uGUI_Pings), "UpdatePings", Type.EmptyTypes));
        if (owners == null)
            return;
        foreach (string owner in owners.Owners)
        {
            if (owner != Id)
                Logger.LogInfo($"Another plugin patches navigation HUD updates ({owner}). Check BepInEx logs if marker presentation differs.");
        }
    }

    private void LoadPriorities()
    {
        LeaveSave();
        try
        {
            if (!priorities.TryLoad(SaveUtils.GetCurrentSaveDataDir(), out string warning))
                Logger.LogWarning($"Smart Beacons priorities are unavailable for this save load. {warning}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            priorities.Reset();
            Logger.LogWarning($"Could not read Smart Beacons priorities: {ex.Message}");
        }
    }

    private bool prioritySaveWarning;

    private void SavePriorities()
    {
        try
        {
            priorities.Save(SaveUtils.GetCurrentSaveDataDir());
            prioritySaveWarning = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            if (!prioritySaveWarning)
                Logger.LogWarning($"Could not save Smart Beacons priorities: {ex.Message}");
            prioritySaveWarning = true;
        }
    }

    private void LeaveSave()
    {
        priorities.Reset();
        prioritySaveWarning = false;
        if (hud != null)
            Safely("clear the previous save HUD", hud.Clear);
    }

    private void Safely(string operation, Action action)
    {
        if (hudFaulted)
            return;
        try { action(); }
        catch (Exception ex) { FailHud(operation, ex); }
    }

    private void FailHud(string operation, Exception ex)
    {
        if (hudFaulted)
            return;
        hudFaulted = true;
        Logger.LogWarning($"Smart Beacons could not {operation}. Falling back to vanilla HUD for this game session. {ex}");
        try { hud?.Clear(); }
        catch (Exception restoreError) { Logger.LogWarning($"HUD cleanup encountered an error: {restoreError.Message}"); }
    }

    private void StopHud()
    {
        PingManager.onRemove -= OnPingRemoved;
        try { hudBindings?.Unsubscribe(BeforePingUpdate, AfterPingUpdate); }
        catch (Exception ex) { Logger.LogWarning($"Could not detach Smart Beacons HUD callbacks: {ex.Message}"); }
        try { hud?.Clear(); }
        catch (Exception ex) { Logger.LogWarning($"HUD cleanup encountered an error: {ex.Message}"); }
        try { input?.Dispose(); }
        catch (Exception ex) { Logger.LogWarning($"Could not detach Smart Beacons input: {ex.Message}"); }
        hud = null;
    }

    private void OnDestroy()
    {
        loaded = false;
        if (settings != null)
            settings.Changed -= OnSettingsChanged;
        SaveUtils.UnregisterOnSaveEvent(SavePriorities);
        SaveUtils.UnregisterOnQuitEvent(LeaveSave);
        StopHud();
    }
}
