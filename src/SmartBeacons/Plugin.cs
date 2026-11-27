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

[BepInPlugin(Id, "Smart Beacons", "0.5.0.3")]
[BepInDependency(NautilusId, "1.0.0.57")]
[BepInProcess("Subnautica.exe")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Id = "com.smartbeacons.subnautica";
    private const string NautilusId = "com.snmodding.nautilus";

    private static Plugin instance;
    private readonly PriorityStore priorities = new();
    private OptionsMenu settings;
    private GameInputAdapter input;
    private HudController hud;
    private Harmony harmony;
    private bool loaded, hudFaulted;

    private void Awake()
    {
        instance = this;
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

            var bindings = new HudBindings();
            try { input = new GameInputAdapter(Logger); }
            catch (Exception ex) { Logger.LogWarning($"Smart Beacons could not initialize hotkeys: {ex.Message}"); }
            hud = new HudController(bindings, input, priorities, Logger);
            harmony = new Harmony(Id);
            harmony.Patch(bindings.Update,
                prefix: Hook(nameof(BeforeUpdate), Priority.First),
                postfix: Hook(nameof(AfterUpdate), Priority.Last));
            harmony.Patch(bindings.Disable, prefix: Hook(nameof(BeforeDisable), Priority.First));
            MethodInfo remove = AccessTools.Method(typeof(uGUI_Pings), "OnRemove", new[] { typeof(string) });
            if (remove != null)
                harmony.Patch(remove, prefix: Hook(nameof(BeforeRemove), Priority.First));
            else
                Logger.LogWarning("uGUI_Pings.OnRemove is unavailable. Removal cleanup will run on the next HUD update.");

            settings.Changed += OnSettingsChanged;
            WaitScreenHandler.RegisterEarlyLoadTask("Smart Beacons", _ => { if (loaded) LoadPriorities(); }, "Loading beacon priorities");
            SaveUtils.RegisterOnSaveEvent(SavePriorities);
            SaveUtils.RegisterOnQuitEvent(LeaveSave);
            loaded = true;
            Logger.LogInfo("Resolved uGUI_Pings.UpdatePings, IsVisibleNow, OnDisable, navigation dictionary and ping presentation fields.");
            if (settings.Current.Debug)
                LogCompatibility();
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Smart Beacons HUD integration could not initialize. Vanilla HUD will be used. {ex}");
            StopHud();
        }
    }

    private static HarmonyMethod Hook(string name, int priority) =>
        new(AccessTools.Method(typeof(Plugin), name)) { priority = priority };

    private static void BeforeUpdate(uGUI_Pings __instance)
    {
        Plugin plugin = instance;
        if (plugin?.hud?.Owns(__instance) == true)
            plugin.Safely("restore vanilla HUD", plugin.hud.Restore);
    }

    private static void AfterUpdate(uGUI_Pings __instance, bool __runOriginal)
    {
        Plugin plugin = instance;
        if (plugin?.hud == null || plugin.hudFaulted || !plugin.settings.Current.Active)
            return;
        if (!__runOriginal)
        {
            if (plugin.hud.Owns(__instance))
                plugin.Safely("hide Smart Beacons presentation for a skipped HUD update", plugin.hud.SuspendPresentation);
            return;
        }
        try { plugin.hud.Update(__instance, plugin.settings.Current); }
        catch (Exception ex) { plugin.FailHud("update navigation HUD", ex); }
    }

    private static void BeforeDisable(uGUI_Pings __instance)
    {
        Plugin plugin = instance;
        if (plugin?.hud?.Owns(__instance) == true)
            plugin.Safely("clear the departing HUD", plugin.hud.Clear);
    }

    private static void BeforeRemove(uGUI_Pings __instance, string __0)
    {
        Plugin plugin = instance;
        if (plugin?.hud == null || plugin.hudFaulted)
            return;
        try { plugin.hud.Remove(__instance, __0); }
        catch (Exception ex) { plugin.FailHud("restore a pooled navigation marker", ex); }
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
        try { hud?.Clear(); }
        catch (Exception ex) { Logger.LogWarning($"HUD cleanup encountered an error: {ex.Message}"); }
        try { input?.Dispose(); }
        catch (Exception ex) { Logger.LogWarning($"Could not detach Smart Beacons input: {ex.Message}"); }
        try { harmony?.UnpatchSelf(); }
        catch (Exception ex) { Logger.LogWarning($"Could not detach Smart Beacons HUD patches: {ex.Message}"); }
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
        instance = null;
    }
}
