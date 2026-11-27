using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Nautilus.Handlers;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
    private readonly List<BindConflict> conflicts = new();
    private readonly HashSet<string> reportedBindings = new(StringComparer.OrdinalIgnoreCase);
    private readonly ManualLogSource log;
    private bool bindingsDirty = true;
    private bool inputChecked;
    private bool revealConflicts, priorityConflicts;
    private bool priorityArmed;
    private bool faulted;
    private int lastFrame = -1;

    public bool IsRevealing { get; private set; }
    public bool TogglePriority { get; private set; }

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

    public void Dispose() => GameInput.OnBindingsChanged -= InvalidateBindings;
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
        }
    }

    private void ReadCore(Player player, Settings settings)
    {
        lastFrame = Time.frameCount;
        TogglePriority = false;
        bool ready = GameInput.IsInitialized;
        if (ready && (bindingsDirty || !inputChecked))
        {
            CheckBindings();
            inputChecked = true;
            bindingsDirty = false;
        }

        bool allowed = ready && settings.Active && player && !player.cinematicModeActive &&
            !WaitScreen.IsWaiting && !FreezeTime.HasFreezers() && Time.timeScale > 0f &&
            !GameInput.IsRebinding && !Cursor.visible && player.GetPDA() is { isInUse: false } && !CapturingText();
        bool revealAllowed = allowed && !revealConflicts;
        bool priorityAllowed = allowed && !priorityConflicts;

        IsRevealing = revealState.Read(settings.RevealMode, revealAllowed,
            revealAllowed && GameInput.GetButtonHeld(reveal), revealAllowed && GameInput.GetButtonDown(reveal));

        if (!priorityAllowed)
            priorityArmed = false;
        else if (!priorityArmed)
            priorityArmed = !GameInput.GetButtonHeld(priority);
        else
            TogglePriority = GameInput.GetButtonDown(priority);
    }

    public void Reset()
    {
        lastFrame = -1;
        IsRevealing = false;
        TogglePriority = false;
        priorityArmed = false;
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

    private void CheckBindings()
    {
        revealConflicts = HasConflict(reveal);
        priorityConflicts = HasConflict(priority);
    }

    private bool HasConflict(GameInput.Button button)
    {
        foreach (GameInput.Device device in GameInput.AllDevices)
        {
            foreach (GameInput.BindingSet slot in GameInput.AllBindingSets)
            {
                string binding = GameInput.GetBinding(device, button, slot);
                if (string.IsNullOrWhiteSpace(binding))
                    continue;
                conflicts.Clear();
                GameInput.GetAllActions(device, binding, conflicts);
                foreach (BindConflict conflict in conflicts)
                {
                    if (conflict.action == button)
                        continue;
                    if (reportedBindings.Add(binding))
                        log.LogWarning($"Smart Beacons ignored a conflicting binding ({binding}). Choose a free binding under Mod Input.");
                    return true;
                }
            }
        }
        return false;
    }
}
