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

            // Initialize unified config before any sub-mods so the section
            // enable flags are available when deciding what to load.
            if (isServer)
            {
                CompetitiveAdjustments.ConfigManager.EnsureConfig();
                CompetitiveAdjustments.ConfigManager.ReloadConfig();
            }

            var cfg = CompetitiveAdjustments.ConfigManager.Config;

            // DashFall runs on both sides; it self-gates client-only components.
            if (cfg.EnableDashfall)
            {
                _dashFall = new DashFallGameMod();
                if (!_dashFall.OnEnable())
                    CompetitiveAdjustments.ConfigManager.LogWarning("DashFall sub-mod failed to enable.");
            }
            else
            {
                CompetitiveAdjustments.ConfigManager.Log("DashFall sub-mod skipped (EnableDashfall=false).");
            }

            _lifecycleHarmony = new Harmony("CompetitiveAdjustments.Lifecycle");
            _lifecycleHarmony.CreateClassProcessor(typeof(StartHostPatch)).Patch();
            _lifecycleHarmony.CreateClassProcessor(typeof(StartServerPatch)).Patch();

            if (cfg.EnableCompTweaks)
            {
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
            }
            else
            {
                CompetitiveAdjustments.ConfigManager.Log("CompTweaks/Companion sub-mod skipped (EnableCompTweaks=false).");
            }

            CompetitiveAdjustments.ConfigManager.Log(
                $"Enabled.  Section enables: Dashfall={cfg.EnableDashfall} CompAdjust={cfg.EnableCompAdjust} CompTweaks={cfg.EnableCompTweaks}.");
            return true;
        }
        catch (Exception ex)
        {
            CompetitiveAdjustments.ConfigManager.LogError("FATAL in OnEnable: " + ex);
            return false;
        }
    }

    /// <summary>
    /// Reconcile sub-mod state with the current config flags.  Called after
    /// any config reload so flipping a top-level enable takes effect without
    /// a full mod reload.  Idempotent: bringing a sub-mod back online is
    /// just OnEnable, taking one offline is OnDisable, no-op if state
    /// already matches.
    /// </summary>
    public void ApplySubModEnables()
    {
        try
        {
            var cfg = CompetitiveAdjustments.ConfigManager.Config;
            if (cfg == null) return;

            // DashFall
            if (cfg.EnableDashfall && _dashFall == null)
            {
                _dashFall = new DashFallGameMod();
                if (!_dashFall.OnEnable())
                    CompetitiveAdjustments.ConfigManager.LogWarning("DashFall sub-mod failed to enable on apply.");
                else
                    CompetitiveAdjustments.ConfigManager.Log("DashFall sub-mod brought online.");
            }
            else if (!cfg.EnableDashfall && _dashFall != null)
            {
                _dashFall.OnDisable();
                _dashFall = null;
                CompetitiveAdjustments.ConfigManager.Log("DashFall sub-mod taken offline.");
            }

            // CompTweaks (server) or Companion (client)
            bool isServer = IsHeadless;
            if (cfg.EnableCompTweaks)
            {
                if (isServer && _tweaks == null)
                {
                    _tweaks = new CompetitivePuckTweaks.src.PluginCore();
                    if (!_tweaks.OnEnable())
                        CompetitiveAdjustments.ConfigManager.LogWarning("Tweaks sub-mod failed to enable on apply.");
                    else
                        CompetitiveAdjustments.ConfigManager.Log("Tweaks sub-mod brought online.");
                }
                else if (!isServer && _companion == null)
                {
                    _companion = new CompetitiveCompanion.PluginCore();
                    if (!_companion.OnEnable())
                        CompetitiveAdjustments.ConfigManager.LogWarning("Companion sub-mod failed to enable on apply.");
                    else
                        CompetitiveAdjustments.ConfigManager.Log("Companion sub-mod brought online.");
                }
            }
            else
            {
                if (_tweaks != null)
                {
                    _tweaks.OnDisable();
                    _tweaks = null;
                    CompetitiveAdjustments.ConfigManager.Log("Tweaks sub-mod taken offline.");
                }
                if (_companion != null)
                {
                    _companion.OnDisable();
                    _companion = null;
                    CompetitiveAdjustments.ConfigManager.Log("Companion sub-mod taken offline.");
                }
            }
        }
        catch (Exception ex)
        {
            CompetitiveAdjustments.ConfigManager.LogError("ApplySubModEnables failed: " + ex);
        }
    }

    internal static void NotifyConfigReloaded()
    {
        _instance?.ApplySubModEnables();
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
