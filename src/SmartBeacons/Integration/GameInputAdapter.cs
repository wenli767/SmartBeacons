using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Nautilus.Handlers;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UWE;

namespace SmartBeacons.Integration;

using Core;

internal sealed class GameInputAdapter
{
    private static readonly AccessTools.FieldRef<DevConsole> ConsoleInstance =
        AccessTools.StaticFieldRefAccess<DevConsole>(AccessTools.Field(typeof(DevConsole), "instance"));

    private static readonly AccessTools.FieldRef<DevConsole, bool> ConsoleOpen =
        AccessTools.FieldRefAccess<DevConsole, bool>("state");

    private readonly GameInput.Button reveal;
    private readonly GameInput.Button priority;
    private readonly RevealState revealState = new();
    private readonly PriorityPress priorityPress = new();
    private readonly List<BindConflict> conflicts = new();
    private readonly HashSet<string> reportedBindings = new(StringComparer.OrdinalIgnoreCase);
    private readonly ManualLogSource log;
    private InputAction revealAction, priorityAction;
    private bool bindingsDirty = true;
    private bool faulted;
    private int lastFrame = -1;

    public bool IsRevealing { get; private set; }
    public bool ConsumePriorityPress() => priorityPress.Consume();

    public GameInputAdapter(ManualLogSource log)
    {
        this.log = log;
        reveal = EnumHandler.AddEntry<GameInput.Button>("SmartBeaconsQuickReveal")
            .CreateInput("Quick reveal", "Show eligible navigation labels and icons.")
            .WithKeyboardBinding(GameInputHandler.Paths.Keyboard.LeftAlt)
            .WithCategory("Smart Beacons");
        priority = EnumHandler.AddEntry<GameInput.Button>("SmartBeaconsTogglePriority")
            .CreateInput("Toggle focused beacon priority", "Look toward an enabled beacon to change its priority.")
            .WithCategory("Smart Beacons");
        GameInput.OnBindingsChanged += InvalidateBindings;
    }

    public void Dispose()
    {
        GameInput.OnBindingsChanged -= InvalidateBindings;
        DisposeActions();
    }

    private void InvalidateBindings() => bindingsDirty = true;

    public void Read(Player player, Settings settings)
    {
        if (faulted || lastFrame == Time.frameCount)
            return;
        try
        {
            ReadCore(player, settings);
        }
        catch (Exception ex)
        {
            faulted = true;
            Reset();
            log.LogWarning($"Smart Beacons hotkeys are unavailable for this session: {ex.Message}");
            try { DisposeActions(); }
            catch (Exception cleanupError) { log.LogWarning($"Could not dispose Smart Beacons hotkeys: {cleanupError.Message}"); }
        }
    }

    private void ReadCore(Player player, Settings settings)
    {
        lastFrame = Time.frameCount;
        bool ready = GameInput.IsInitialized;
        if (!ready)
        {
            if (revealAction != null || priorityAction != null)
                DisposeActions();
            bindingsDirty = true;
            Reset();
            return;
        }

        if (bindingsDirty)
            RefreshBindings();

        bool allowed = settings.Active && player && !player.cinematicModeActive &&
            !WaitScreen.IsWaiting && !FreezeTime.HasFreezers() && Time.timeScale > 0f &&
            !GameInput.IsRebinding && !Cursor.visible && player.GetPDA() is { isInUse: false } && !CapturingText();
        bool revealAllowed = allowed && revealAction != null;
        bool priorityAllowed = allowed && priorityAction != null;

        IsRevealing = revealState.Read(settings.RevealMode, revealAllowed,
            revealAllowed && revealAction.IsPressed(), revealAllowed && revealAction.WasPressedThisFrame());
        priorityPress.Read(priorityAllowed, priorityAllowed && priorityAction.IsPressed(),
            priorityAllowed && priorityAction.WasPressedThisFrame());
    }

    public void Reset()
    {
        IsRevealing = false;
        priorityPress.Reset();
        revealState.Reset();
    }

    private static bool CapturingText()
    {
        DevConsole console = ConsoleInstance();
        if (console && ConsoleOpen(console))
            return true;
        GameObject selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        if (!selected)
            return false;
        var tmp = selected.GetComponent<TMP_InputField>();
        if (tmp && tmp.isFocused)
            return true;
        var legacy = selected.GetComponent<InputField>();
        return legacy && legacy.isFocused;
    }

    private void RefreshBindings()
    {
        DisposeActions();
        revealAction = CreateFilteredAction(reveal, priority);
        priorityAction = CreateFilteredAction(priority, reveal);
        bindingsDirty = false;
        Reset();
    }

    private InputAction CreateFilteredAction(GameInput.Button button, GameInput.Button otherHotkey)
    {
        // Nautilus owns rebinding. These polling actions contain only the working paths.
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (GameInput.Device device in GameInput.AllDevices)
        {
            foreach (GameInput.BindingSet slot in GameInput.AllBindingSets)
            {
                string binding = GameInput.GetBinding(device, button, slot);
                if (string.IsNullOrWhiteSpace(binding))
                    continue;
                if (BindingConflicts(device, button, otherHotkey, binding))
                {
                    if (reportedBindings.Add(binding))
                        log.LogWarning($"Smart Beacons ignored a conflicting binding ({binding}). Choose a free binding under Mod Input.");
                }
                else
                    paths.Add(binding);
            }
        }

        if (paths.Count == 0)
            return null;

        var action = new InputAction(type: InputActionType.Button) { wantsInitialStateCheck = true };
        try
        {
            foreach (string path in paths)
                action.AddBinding(path);
            action.Enable();
            return action;
        }
        catch
        {
            action.Dispose();
            throw;
        }
    }

    private bool BindingConflicts(GameInput.Device device, GameInput.Button button, GameInput.Button otherHotkey,
        string path)
    {
        conflicts.Clear();
        GameInput.GetAllActions(device, path, conflicts);
        foreach (BindConflict conflict in conflicts)
        {
            if (conflict.action != button)
                return true;
        }
        foreach (GameInput.BindingSet slot in GameInput.AllBindingSets)
        {
            if (string.Equals(path, GameInput.GetBinding(device, otherHotkey, slot), StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private void DisposeActions()
    {
        try { revealAction?.Dispose(); }
        finally
        {
            revealAction = null;
            InputAction action = priorityAction;
            priorityAction = null;
            action?.Dispose();
        }
    }
}
