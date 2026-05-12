// CompetitiveAdjustmentsGameMod.cs
// Single IPuckMod entry point for the combined CompetitiveAdjustments mod.
// Detects headless server vs windowed client and routes to the appropriate sub-mods:
//   Server  → CompetitivePuckTweaks (physics/gameplay tuning)
//   Client  → CompetitiveCompanion (visual companion)
//   Both    → DashFall (dash/dive/twist – it self-gates its own client components)

using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class CompetitiveAdjustmentsGameMod : IPuckPlugin
{
    private static CompetitiveAdjustmentsGameMod _instance;
    private Harmony _lifecycleHarmony;
    private DashFallGameMod _dashFall;
    private CompetitivePuckTweaks.src.PluginCore _tweaks;
    private CompetitiveCompanion.PluginCore _companion;

    private static bool IsHeadless =>
        Application.isBatchMode ||
        SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;

    public bool OnEnable()
    {
        try
        {
            _instance = this;
            bool isServer = IsHeadless;
            Debug.Log($"[COMPADJUST] Enabling (isServer={isServer})…");

            // Initialize unified config before any sub-mods
            if (isServer)
            {
                CompetitiveAdjustments.ConfigManager.EnsureConfig();
                CompetitiveAdjustments.ConfigManager.ReloadConfig();
            }

            // DashFall runs on both sides; it self-gates client-only components.
            _dashFall = new DashFallGameMod();
            if (!_dashFall.OnEnable())
            {
                CompetitiveAdjustments.ConfigManager.LogWarning("DashFall sub-mod failed to enable.");
            }

            _lifecycleHarmony = new Harmony("CompetitiveAdjustments.Lifecycle");
            _lifecycleHarmony.CreateClassProcessor(typeof(StartHostPatch)).Patch();
            _lifecycleHarmony.CreateClassProcessor(typeof(StartServerPatch)).Patch();

            if (isServer)
            {
                _tweaks = new CompetitivePuckTweaks.src.PluginCore();
                if (!_tweaks.OnEnable())
                    CompetitiveAdjustments.ConfigManager.LogWarning("Tweaks sub-mod failed to enable.");
            }
            else
            {
                _companion = new CompetitiveCompanion.PluginCore();
                if (!_companion.OnEnable())
                    CompetitiveAdjustments.ConfigManager.LogWarning("Companion sub-mod failed to enable.");
            }

            CompetitiveAdjustments.ConfigManager.Log("Enabled.");
            return true;
        }
        catch (Exception ex)
        {
            CompetitiveAdjustments.ConfigManager.LogError("FATAL in OnEnable: " + ex);
            return false;
        }
    }

    public bool OnDisable()
    {
        try
        {
            _lifecycleHarmony?.UnpatchSelf();
            _lifecycleHarmony = null;
            _dashFall?.OnDisable();
            _tweaks?.OnDisable();
            _companion?.OnDisable();
            _instance = null;
            CompetitiveAdjustments.ConfigManager.Log("Disabled.");
            return true;
        }
        catch (Exception ex)
        {
            CompetitiveAdjustments.ConfigManager.LogError("Error in OnDisable: " + ex);
            return false;
        }
    }

    private void EnsureServerRuntimeComponents()
    {
        try
        {
            CompetitiveAdjustments.ConfigManager.EnsureConfig();
            CompetitiveAdjustments.ConfigManager.ReloadConfig();

            if (_tweaks == null)
                _tweaks = new CompetitivePuckTweaks.src.PluginCore();

            if (!_tweaks.OnEnable())
                CompetitiveAdjustments.ConfigManager.LogWarning("Tweaks sub-mod failed to enable in host/practice mode.");
        }
        catch (Exception ex)
        {
            CompetitiveAdjustments.ConfigManager.LogError("Failed to enable server runtime components: " + ex);
        }
    }

    [HarmonyPatch(typeof(Unity.Netcode.NetworkManager), "StartHost")]
    private static class StartHostPatch
    {
        [HarmonyPostfix]
        private static void Postfix(bool __result)
        {
            if (!__result) return;
            _instance?.EnsureServerRuntimeComponents();
        }
    }

    [HarmonyPatch(typeof(Unity.Netcode.NetworkManager), "StartServer")]
    private static class StartServerPatch
    {
        [HarmonyPostfix]
        private static void Postfix(bool __result)
        {
            if (!__result) return;
            _instance?.EnsureServerRuntimeComponents();
        }
    }
}

/// <summary>
/// Shared helper – apply Harmony patches only for types whose namespace
/// starts with one of the supplied prefixes (avoids cross-mod patch bleed).
/// </summary>
internal static class HarmonyPatchHelper
{
    public static void PatchNamespaces(Harmony harmony, params string[] nsPrefixes)
    {
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (type.Namespace == null) continue;
            foreach (var prefix in nsPrefixes)
            {
                if (type.Namespace == prefix || type.Namespace.StartsWith(prefix + "."))
                {
                    try { harmony.CreateClassProcessor(type).Patch(); }
                    catch { /* skip types with no patches */ }
                    break;
                }
            }
        }
    }
}
