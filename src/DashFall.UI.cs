// DashFall.UI.cs - Full UI Panel with keybind editing (copied from PlayerInput style)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using UITK = UnityEngine.UIElements;

namespace DashFallMod.Client
{
    public partial class DashFallClientRunner
    {
        // UI Toolkit elements
        private UITK.VisualElement _dfPanel;
        private UITK.VisualElement _dfBackdrop;
        private UITK.VisualElement _captureOverlay;
        private UITK.Label _captureLabel;
        private UITK.ScrollView _scrollView;
        private UITK.VisualElement _actionsSection;
        
        // Tab system
        private UITK.Button _skaterTabBtn;
        private UITK.Button _goalieTabBtn;
        private UITK.Button _serverTabBtn;
        private UITK.Button _settingsTabBtn;
        private enum ActiveTab { Skater, Goalie, Server, Settings }
        private ActiveTab _activeTab = ActiveTab.Skater;
        
        private Action<string> _onChordCaptured;
        private bool _panelHiddenForCapture;
        private readonly List<UITK.VisualElement> _hiddenMenuButtons = new List<UITK.VisualElement>();

        // UI palette (matching base game)
        private static readonly Color32 TextFieldBg = new Color32(57, 57, 57, 255);
        private static readonly Color32 RowBg = new Color32(61, 61, 61, 255);
        private static readonly Color32 DisabledRowBg = new Color32(40, 40, 40, 255);
        private static readonly Color32 PanelBg = new Color32(48, 48, 47, 255);
        private static readonly Color32 TabActiveBg = new Color32(80, 80, 80, 255);
        private static readonly Color32 TabInactiveBg = new Color32(66, 66, 66, 255);
        private const int BTN_W = 80;

        // Font
        private static Font _uiFont;
        private static Font GetUIFont()
        {
            if (_uiFont != null) return _uiFont;
            try { _uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
            if (_uiFont == null)
            {
                try { _uiFont = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Segoe UI" }, 16); } catch { }
            }
            return _uiFont;
        }
        
        private static void ForceUIFont(UITK.VisualElement ve)
        {
            var f = GetUIFont();
            if (f != null) ve.style.unityFont = f;
        }

        private static void MakeReadable(UITK.Label l)
        {
            l.style.color = Color.white;
            ForceUIFont(l);
        }

        private static void MakeReadable(UITK.Button b)
        {
            b.style.color = Color.white;
            ForceUIFont(b);
        }

        // ========== PANEL BUILD ==========
        private void BuildDashFallPanel()
        {
            if (_dfPanel != null) return;

            var root = _doc?.rootVisualElement ?? _lastRoot;
            if (root == null) return;

            // Backdrop (semi-transparent overlay)
            _dfBackdrop = new UITK.VisualElement { name = "DashFall_Backdrop" };
            _dfBackdrop.style.position = UITK.Position.Absolute;
            _dfBackdrop.style.left = 0;
            _dfBackdrop.style.top = 0;
            _dfBackdrop.style.right = 0;
            _dfBackdrop.style.bottom = 0;
            _dfBackdrop.style.backgroundColor = new UITK.StyleColor(new Color(0, 0, 0, 0.0f));
            _dfBackdrop.style.display = UITK.DisplayStyle.None;
            _dfBackdrop.pickingMode = UITK.PickingMode.Position;
            _dfBackdrop.RegisterCallback<UITK.PointerUpEvent>(e =>
            {
                // Close panel when clicking backdrop
                CloseDashFallPanel();
            });

            // Main panel
            _dfPanel = new UITK.VisualElement { name = "DashFall_Panel" };
            _dfPanel.style.position = UITK.Position.Absolute;
            _dfPanel.style.left = new UITK.Length(50, UITK.LengthUnit.Percent);
            _dfPanel.style.top = new UITK.Length(50, UITK.LengthUnit.Percent);
            _dfPanel.style.translate = new UITK.Translate(
                new UITK.Length(-50, UITK.LengthUnit.Percent),
                new UITK.Length(-50, UITK.LengthUnit.Percent), 0f);
            int targetW = Mathf.Clamp(Mathf.RoundToInt(Screen.width * 0.58f), 680, 980);
            _dfPanel.style.width = targetW;
            _dfPanel.style.height = new UITK.Length(84, UITK.LengthUnit.Percent);
            _dfPanel.style.minHeight = new UITK.Length(56, UITK.LengthUnit.Percent);
            _dfPanel.style.maxHeight = new UITK.Length(56, UITK.LengthUnit.Percent);
            _dfPanel.style.overflow = UITK.Overflow.Hidden;
            _dfPanel.style.flexDirection = UITK.FlexDirection.Column;
            _dfPanel.style.backgroundColor = new UITK.StyleColor(PanelBg);
            _dfPanel.style.paddingLeft = 8; _dfPanel.style.paddingRight = 8;
            _dfPanel.style.paddingTop = 8; _dfPanel.style.paddingBottom = 12;
            _dfPanel.style.display = UITK.DisplayStyle.None;
            _dfPanel.pickingMode = UITK.PickingMode.Position;
            _dfPanel.RegisterCallback<UITK.PointerUpEvent>(e => e.StopPropagation());

            // Title
            var bigTitle = new UITK.Label("COMPADJUST");
            bigTitle.style.fontSize = 50;
            bigTitle.style.marginBottom = 8;
            MakeReadable(bigTitle);
            _dfPanel.Add(bigTitle);

            // Tab bar
            var tabBar = new UITK.VisualElement();
            tabBar.style.flexDirection = UITK.FlexDirection.Row;
            tabBar.style.marginBottom = 26;
            tabBar.style.height = 50;

            _skaterTabBtn = MakeTabButton("SKATER", true, () => SwitchToTab(ActiveTab.Skater));
            _goalieTabBtn = MakeTabButton("GOALIE", false, () => SwitchToTab(ActiveTab.Goalie));
            _serverTabBtn = MakeTabButton("SERVER", false, () => SwitchToTab(ActiveTab.Server));
            _settingsTabBtn = MakeTabButton("SETTINGS", false, () => SwitchToTab(ActiveTab.Settings));
            
            tabBar.Add(_skaterTabBtn);
            tabBar.Add(_goalieTabBtn);
            tabBar.Add(_serverTabBtn);
            tabBar.Add(_settingsTabBtn);
            _dfPanel.Add(tabBar);

            // Scroll view for content
            _scrollView = new UITK.ScrollView
            {
                verticalScrollerVisibility = UITK.ScrollerVisibility.Auto,
                horizontalScrollerVisibility = UITK.ScrollerVisibility.Hidden
            };
            _scrollView.style.flexGrow = 1;
            _dfPanel.Add(_scrollView);

            _actionsSection = new UITK.VisualElement();
            _scrollView.Add(_actionsSection);

            // Build the action rows
            BuildActionsUI();

                UITK.Button MakeDonateButton(string t, Action onClick)
                {
                    var b = new UITK.Button(onClick) { text = t.ToUpperInvariant() };
                    b.style.unityTextAlign = new UITK.StyleEnum<TextAnchor>(TextAnchor.MiddleCenter);
                    b.style.height = 50;
                    b.style.marginTop = 8;
                    b.style.marginBottom = 8;
                    b.style.paddingLeft = 18; b.style.paddingRight = 18;
                    b.style.backgroundColor = new UITK.StyleColor(ButtonBg);
                    MakeReadable(b);
                    AddButtonFlash(b);
                    return b;
                }
                UITK.Button MakeResetButton(string t, Action onClick)
                {
                    var b = new UITK.Button(onClick) { text = t.ToUpperInvariant() };
                    b.style.unityTextAlign = new UITK.StyleEnum<TextAnchor>(TextAnchor.MiddleCenter);
                    b.style.height = 50;
                    b.style.marginTop = 8;
                    b.style.marginBottom = 22;
                    b.style.paddingLeft = 18; b.style.paddingRight = 18;
                    b.style.marginLeft = 238;
                    b.style.backgroundColor = new UITK.StyleColor(ButtonBg);
                    MakeReadable(b);
                    AddButtonFlash(b);
                    return b;
                }
                UITK.Button MakeCloseButton(string t, Action onClick)
                {
                    var b = new UITK.Button(onClick) { text = t.ToUpperInvariant() };
                    b.style.unityTextAlign = new UITK.StyleEnum<TextAnchor>(TextAnchor.MiddleCenter);
                    b.style.height = 50;
                    b.style.marginTop = 8;
                    b.style.marginBottom = 22;
                    b.style.paddingLeft = 18; b.style.paddingRight = 182;
                    b.style.marginLeft = 8;
                    b.style.backgroundColor = new UITK.StyleColor(ButtonBg);
                    MakeReadable(b);
                    AddButtonFlash(b);
                    return b;
                }

            var donate = MakeDonateButton("COFFEE?", () =>
            {
                Application.OpenURL("https://buymeacoffee.com/amikiir");
            });

            var resetBtn = MakeResetButton("RESET TO DEFAULTS", () =>
            {
                ResetToDefaults();
                ResetInputActions();
                RefreshActionsUI();
            });

            var closeBtn = MakeCloseButton("CLOSE", () =>
            {
                DashFallConfigLoader.SaveSkaterConfig(_skater);
                DashFallConfigLoader.SaveGoalieConfig(_goalie);
                RebuildLookups();
                ResetInputActions();
                CloseDashFallPanel();
            });

            // Button row at bottom
            var buttonRow = new UITK.VisualElement();
            buttonRow.style.flexDirection = UITK.FlexDirection.Row;
            buttonRow.Add(donate);
            buttonRow.Add(resetBtn);
            buttonRow.Add(closeBtn);
            _dfPanel.Add(buttonRow);

            // Add to root
            root.Add(_dfBackdrop);
            _dfBackdrop.Add(_dfPanel);
        }

        private UITK.Button MakeTabButton(string text, bool isActive, Action onClick)
        {
            var btn = new UITK.Button(onClick) { text = text };
            btn.style.height = 50;
            btn.style.flexGrow = 1;
            btn.style.paddingLeft = 8;
            btn.style.paddingRight = 8;
            btn.style.marginRight = 8;
            btn.style.marginBottom = 26;
            btn.style.fontSize = 24;
            btn.style.unityTextAlign = new UITK.StyleEnum<TextAnchor>(TextAnchor.MiddleCenter);
            btn.style.borderTopLeftRadius = 6;
            btn.style.borderTopRightRadius = 6;
            btn.style.borderBottomLeftRadius = 0;
            btn.style.borderBottomRightRadius = 0;
            btn.style.borderBottomWidth = isActive ? 3 : 0;
            btn.style.borderBottomColor = new UITK.StyleColor(Color.white);
            btn.style.backgroundColor = new UITK.StyleColor(isActive ? TabActiveBg : TabInactiveBg);
            btn.style.color = isActive ? Color.white : new Color(0.7f, 0.7f, 0.7f);
            ForceUIFont(btn);
            
            // Add hover effect - white background on hover (unless active)
            btn.RegisterCallback<UITK.PointerEnterEvent>(_ => {
                // Check if this tab is currently active by checking border
                float borderWidth = btn.resolvedStyle.borderBottomWidth;
                if (borderWidth < 1)
                {
                    btn.style.backgroundColor = new UITK.StyleColor(Color.white);
                    btn.style.color = Color.black;
                }
            });
            btn.RegisterCallback<UITK.PointerLeaveEvent>(_ => {
                // Restore based on active state (check border)
                float borderWidth = btn.resolvedStyle.borderBottomWidth;
                if (borderWidth < 1)
                {
                    btn.style.backgroundColor = new UITK.StyleColor(TabInactiveBg);
                    btn.style.color = new Color(0.7f, 0.7f, 0.7f);
                }
            });
            
            return btn;
        }

        private void SwitchToTab(ActiveTab tab)
        {
            _activeTab = tab;
            UpdateTabStyles();
            RefreshActionsUI();
        }

        private void UpdateTabStyles()
        {
            if (_skaterTabBtn != null)
            {
                bool active = _activeTab == ActiveTab.Skater;
                _skaterTabBtn.style.backgroundColor = new UITK.StyleColor(active ? TabActiveBg : TabInactiveBg);
                _skaterTabBtn.style.color = active ? Color.white : new Color(0.7f, 0.7f, 0.7f);
                _skaterTabBtn.style.borderBottomWidth = active ? 3 : 0;
            }
            if (_goalieTabBtn != null)
            {
                bool active = _activeTab == ActiveTab.Goalie;
                _goalieTabBtn.style.backgroundColor = new UITK.StyleColor(active ? TabActiveBg : TabInactiveBg);
                _goalieTabBtn.style.color = active ? Color.white : new Color(0.7f, 0.7f, 0.7f);
                _goalieTabBtn.style.borderBottomWidth = active ? 3 : 0;
            }
            if (_serverTabBtn != null)
            {
                bool active = _activeTab == ActiveTab.Server;
                _serverTabBtn.style.backgroundColor = new UITK.StyleColor(active ? TabActiveBg : TabInactiveBg);
                _serverTabBtn.style.color = active ? Color.white : new Color(0.7f, 0.7f, 0.7f);
                _serverTabBtn.style.borderBottomWidth = active ? 3 : 0;
            }
            if (_settingsTabBtn != null)
            {
                bool active = _activeTab == ActiveTab.Settings;
                _settingsTabBtn.style.backgroundColor = new UITK.StyleColor(active ? TabActiveBg : TabInactiveBg);
                _settingsTabBtn.style.color = active ? Color.white : new Color(0.7f, 0.7f, 0.7f);
                _settingsTabBtn.style.borderBottomWidth = active ? 3 : 0;
            }
        }

        private void DontWrap(UITK.Label l)
        {
            l.style.whiteSpace = UITK.WhiteSpace.NoWrap;
            l.style.textOverflow = UITK.TextOverflow.Ellipsis;
        }

        private void BuildActionsUI()
        {
            _actionsSection.Clear();
            
            // Get server features (if connected)
            var features = PoncePuck.Keybinds.ServerBridge.ReceivedFeatures;
            bool hasFeatures = PoncePuck.Keybinds.ServerBridge.HasReceivedFeatures;

            if (_activeTab == ActiveTab.Skater)
            {
                // Skater section
                _actionsSection.Add(MakeBindRow("DIVE", () => _skater.divekey, v => _skater.divekey = v,
                    () => _skater.divekeytype, v => _skater.divekeytype = v, BindRowType.Pressable,
                    !hasFeatures || features.SkaterDiveEnabled));
                _actionsSection.Add(MakeBindRow("TWIST LEFT", () => _skater.twistleftkey, v => _skater.twistleftkey = v,
                    () => _skater.twistleftkeytype, v => _skater.twistleftkeytype = v, BindRowType.Pressable,
                    !hasFeatures || features.SkaterTwistEnabled));
                _actionsSection.Add(MakeBindRow("TWIST RIGHT", () => _skater.twistrightkey, v => _skater.twistrightkey = v,
                    () => _skater.twistrightkeytype, v => _skater.twistrightkeytype = v, BindRowType.Pressable,
                    !hasFeatures || features.SkaterTwistEnabled));
                _actionsSection.Add(MakeBindRow("SLIDE DI LEFT", () => _skater.slideinfluenceleftkey, v => _skater.slideinfluenceleftkey = v,
                    () => _skater.slideinfluenceleftkeytype, v => _skater.slideinfluenceleftkeytype = v, BindRowType.Holdable,
                    !hasFeatures || features.SkaterSlideInfluenceEnabled));
                _actionsSection.Add(MakeBindRow("SLIDE DI RIGHT", () => _skater.slideinfluencerightkey, v => _skater.slideinfluencerightkey = v,
                    () => _skater.slideinfluencerightkeytype, v => _skater.slideinfluencerightkeytype = v, BindRowType.Holdable,
                    !hasFeatures || features.SkaterSlideInfluenceEnabled));
                _actionsSection.Add(MakeBindRow("SLIDE DI FORWARD", () => _skater.slideinfluenceforwardkey, v => _skater.slideinfluenceforwardkey = v,
                    () => _skater.slideinfluenceforwardkeytype, v => _skater.slideinfluenceforwardkeytype = v, BindRowType.Holdable,
                    !hasFeatures || features.SkaterSlideInfluenceEnabled));
                _actionsSection.Add(MakeBindRow("SLIDE DI BACKWARD", () => _skater.slideinfluencebackwardkey, v => _skater.slideinfluencebackwardkey = v,
                    () => _skater.slideinfluencebackwardkeytype, v => _skater.slideinfluencebackwardkeytype = v, BindRowType.Holdable,
                    !hasFeatures || features.SkaterSlideInfluenceEnabled));
            }
            else if (_activeTab == ActiveTab.Goalie)
            {
                // Goalie section
                _actionsSection.Add(MakeBindRow("DIVE", () => _goalie.divekey, v => _goalie.divekey = v,
                    () => _goalie.divekeytype, v => _goalie.divekeytype = v, BindRowType.Pressable,
                    !hasFeatures || features.GoalieDiveEnabled));
                _actionsSection.Add(MakeBindRow("STANDING DASH LEFT", () => _goalie.standingdashleftkey, v => _goalie.standingdashleftkey = v,
                    () => _goalie.standingdashleftkeytype, v => _goalie.standingdashleftkeytype = v, BindRowType.Pressable,
                    !hasFeatures || features.GoalieStandingDashEnabled));
                _actionsSection.Add(MakeBindRow("STANDING DASH RIGHT", () => _goalie.standingdashrightkey, v => _goalie.standingdashrightkey = v,
                    () => _goalie.standingdashrightkeytype, v => _goalie.standingdashrightkeytype = v, BindRowType.Pressable,
                    !hasFeatures || features.GoalieStandingDashEnabled));
                _actionsSection.Add(MakeBindRow("TWIST LEFT", () => _goalie.twistleftkey, v => _goalie.twistleftkey = v,
                    () => _goalie.twistleftkeytype, v => _goalie.twistleftkeytype = v, BindRowType.Pressable,
                    !hasFeatures || features.GoalieTwistEnabled));
                _actionsSection.Add(MakeBindRow("TWIST RIGHT", () => _goalie.twistrightkey, v => _goalie.twistrightkey = v,
                    () => _goalie.twistrightkeytype, v => _goalie.twistrightkeytype = v, BindRowType.Pressable,
                    !hasFeatures || features.GoalieTwistEnabled));
                _actionsSection.Add(MakeBindRow("SLIDE DI LEFT", () => _goalie.slideinfluenceleftkey, v => _goalie.slideinfluenceleftkey = v,
                    () => _goalie.slideinfluenceleftkeytype, v => _goalie.slideinfluenceleftkeytype = v, BindRowType.Holdable,
                    !hasFeatures || features.GoalieSlideInfluenceEnabled));
                _actionsSection.Add(MakeBindRow("SLIDE DI RIGHT", () => _goalie.slideinfluencerightkey, v => _goalie.slideinfluencerightkey = v,
                    () => _goalie.slideinfluencerightkeytype, v => _goalie.slideinfluencerightkeytype = v, BindRowType.Holdable,
                    !hasFeatures || features.GoalieSlideInfluenceEnabled));
                _actionsSection.Add(MakeBindRow("SLIDE DI FORWARD", () => _goalie.slideinfluenceforwardkey, v => _goalie.slideinfluenceforwardkey = v,
                    () => _goalie.slideinfluenceforwardkeytype, v => _goalie.slideinfluenceforwardkeytype = v, BindRowType.Holdable,
                    !hasFeatures || features.GoalieSlideInfluenceEnabled));
                _actionsSection.Add(MakeBindRow("SLIDE DI BACKWARD", () => _goalie.slideinfluencebackwardkey, v => _goalie.slideinfluencebackwardkey = v,
                    () => _goalie.slideinfluencebackwardkeytype, v => _goalie.slideinfluencebackwardkeytype = v, BindRowType.Holdable,
                    !hasFeatures || features.GoalieSlideInfluenceEnabled));
            }
            else if (_activeTab == ActiveTab.Server)
            {
                // Server config display
                BuildServerConfigUI();
            }
            else if (_activeTab == ActiveTab.Settings)
            {
                // Settings tab
                BuildSettingsUI();
            }
        }

        private void BuildServerConfigUI()
        {
            var features = PoncePuck.Keybinds.ServerBridge.ReceivedFeatures;
            bool hasFeatures = PoncePuck.Keybinds.ServerBridge.HasReceivedFeatures;

            if (!hasFeatures)
            {
                var noDataLabel = new UITK.Label("Not connected to a server with CompetitiveAdjustments.");
                noDataLabel.style.fontSize = 24;
                noDataLabel.style.marginTop = 20;
                noDataLabel.style.marginBottom = 20;
                noDataLabel.style.unityTextAlign = new UITK.StyleEnum<TextAnchor>(TextAnchor.MiddleCenter);
                MakeReadable(noDataLabel);
                _actionsSection.Add(noDataLabel);
                return;
            }

            // --- DashFall Feature Flags ---
            AddSectionHeader("DASHFALL FLAGS");

            AddSubHeader("SKATER");
            // _actionsSection.Add(MakeServerConfigRow("Dash", features.SkaterDashEnabled));
            _actionsSection.Add(MakeServerConfigRow("Dive", features.SkaterDiveEnabled));
            _actionsSection.Add(MakeServerConfigRow("Twist While Sliding", features.SkaterTwistEnabled));
            _actionsSection.Add(MakeServerConfigRow("Slide Influence", features.SkaterSlideInfluenceEnabled));

            AddSubHeader("GOALIE");
            _actionsSection.Add(MakeServerConfigRow("Dive", features.GoalieDiveEnabled));
            _actionsSection.Add(MakeServerConfigRow("Standing Dash", features.GoalieStandingDashEnabled));
            _actionsSection.Add(MakeServerConfigRow("Twist While Sliding", features.GoalieTwistEnabled));
            _actionsSection.Add(MakeServerConfigRow("Slide Influence", features.GoalieSlideInfluenceEnabled));
            _actionsSection.Add(MakeServerConfigRow("Dash Extend", features.GoalieDashExtendEnabled));
            _actionsSection.Add(MakeServerConfigRow("Stances", features.GoalieStancesEnabled));

            AddSectionHeader("COMPADJUST FLAGS");
            _actionsSection.Add(MakeServerConfigRow("Sprint Shoulder Trail", features.SprintShoulderTrailEnabled));
            _actionsSection.Add(MakeServerConfigRow("Skater Torso Adjustments", CompetitiveAdjustments.ConfigManager.Config.CompAdjust.EnableCustomSkaterTorsoModel));
            _actionsSection.Add(MakeServerConfigRow("Disable Torso Model", CompetitiveAdjustments.ConfigManager.Config.CompAdjust.DisableCustomTorsoVisual));
            _actionsSection.Add(MakeServerConfigRow("Goal Net Tweaks", CompetitiveAdjustments.ConfigManager.Config.CompAdjust.EnableGoalNetTweaks));
            _actionsSection.Add(MakeServerConfigRow("Arena Tweaks", CompetitiveAdjustments.ConfigManager.Config.CompAdjust.EnableArenaTweaks));

            AddSubHeader("STICK MODIFIERS");
            _actionsSection.Add(MakeServerConfigRow("Free Blade", CompetitiveAdjustments.ConfigManager.Config.CompAdjust.FreeBladeEnabled));
            _actionsSection.Add(MakeServerConfigRow("Higher Stick", CompetitiveAdjustments.ConfigManager.Config.CompAdjust.HighStickingEnabled));

            // --- CompTweaks Flags ---
            // These are read from the synced config (CompetitivePuckTweaks values)
            var tweaksCfg = CompetitivePuckTweaks.src.PluginCore.config;
            if (tweaksCfg != null)
            {
                AddSectionHeader("COMPTWEAKS FLAGS");

                AddSubHeader("Player");
                _actionsSection.Add(MakeServerConfigRow("Thin Skater Bodies", tweaksCfg.ThinSkaterBodies));
                _actionsSection.Add(MakeServerConfigRow("Smaller Models", tweaksCfg.EnableSmallerModels));
                _actionsSection.Add(MakeServerConfigRow("Goalie Microdash", tweaksCfg.EnableGoalieMicrodash));

                AddSubHeader("Puck");
                _actionsSection.Add(MakeServerConfigRow("Random Puck Drop", tweaksCfg.RandomPuckDrop));
                _actionsSection.Add(MakeServerConfigRow("Puck Through Bodies", tweaksCfg.EnablePuckThroughBodies));
                _actionsSection.Add(MakeServerConfigRow("Puck Through Groin", tweaksCfg.EnablePuckThroughGroin));
                _actionsSection.Add(MakeServerConfigRow("Puck Drag Speed Dependence", tweaksCfg.PuckDragSpeedDependence));
                _actionsSection.Add(MakeServerConfigRow("Puck Height Dependent Drag", tweaksCfg.PuckHeightDependentDrag));

                AddSubHeader("Stick");
                _actionsSection.Add(MakeServerConfigRow("Disable Stick Collision", tweaksCfg.DisableStickCollision));
                _actionsSection.Add(MakeServerConfigRow("Disable Shaft Collision", tweaksCfg.DisableShaftCollision));
                _actionsSection.Add(MakeServerConfigRow("Mid Stick Collider", tweaksCfg.EnableMidStickCollider));
                _actionsSection.Add(MakeServerConfigRow("Alter Stick Positioner", tweaksCfg.AlterStickPositionerOutput));
                _actionsSection.Add(MakeServerConfigRow("Stick Speed Decay", tweaksCfg.EnableStickSpeedDecay));

                AddSubHeader("Arena");
                _actionsSection.Add(MakeServerConfigRow("Soft Boards", tweaksCfg.EnableSoftBoards));
                _actionsSection.Add(MakeServerConfigRow("Board Bounce Tweak", tweaksCfg.EnableJohnBoardBounceTweak));

                AddSubHeader("Physics");
                _actionsSection.Add(MakeServerConfigRow("Physics Mod Events", tweaksCfg.UsePhysicsModificationEvents));

                AddSubHeader("Misc");
                _actionsSection.Add(MakeServerConfigRow("Banana Mode", tweaksCfg.BananaMode));
            }

            AddSectionHeader("ALL DASHFALL BOOLS");
            foreach (var pair in EnumerateBoolFields(CompetitiveAdjustments.ConfigManager.Config.Dashfall))
            {
                _actionsSection.Add(MakeServerConfigRow(pair.Key, pair.Value));
            }

            AddSectionHeader("ALL COMPADJUST BOOLS");
            foreach (var pair in EnumerateBoolFields(CompetitiveAdjustments.ConfigManager.Config.CompAdjust))
            {
                _actionsSection.Add(MakeServerConfigRow(pair.Key, pair.Value));
            }

            AddSectionHeader("ALL COMPTWEAKS BOOLS");
            foreach (var pair in EnumerateBoolFields(CompetitiveAdjustments.ConfigManager.Config.CompTweaks))
            {
                _actionsSection.Add(MakeServerConfigRow(pair.Key, pair.Value));
            }
        }

        private void BuildSettingsUI()
        {
            var header = new UITK.Label("SETTINGS");
            header.style.fontSize = 24;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 16;
            header.style.marginTop = 8;
            MakeReadable(header);
            _actionsSection.Add(header);

            var clientConfig = DashFallConfigLoader.ClientConfig;

            _actionsSection.Add(MakeToggleRow("CUSTOM TORSO MESH", "Show custom skater torso mesh", clientConfig.ShowCustomTorsoMesh, (val) =>
            {
                clientConfig.ShowCustomTorsoMesh = val;
                DashFallConfigLoader.SaveClientConfig(clientConfig);
                CompetitivePuckTweaks.src.PluginCore.RefreshTorsoVisualsForClient();
            }));

            _actionsSection.Add(MakeToggleRow("MINIMAP TWEAKS", "Apply arena-scale minimap rescaling (disable if you prefer default minimap)", clientConfig.EnableMinimapTweaks, (val) =>
            {
                clientConfig.EnableMinimapTweaks = val;
                DashFallConfigLoader.SaveClientConfig(clientConfig);
                DashFallClientRunner.RefreshMinimap();
            }));

            _actionsSection.Add(MakeFloatRow("PUCK SCALE", "Companion visual puck scale (server-synced)", clientConfig.PuckScale, 0.5f, 2f, (val) =>
            {
                clientConfig.PuckScale = val;
                DashFallConfigLoader.SaveClientConfig(clientConfig);
                ApplyLocalPuckScale(val);
            }));

            _actionsSection.Add(MakeFloatRow("BUTTERFLY PAD OFFSET", "Companion leg pad offset (server-synced)", clientConfig.ButterflyPadOffset, 0f, 0.25f, (val) =>
            {
                clientConfig.ButterflyPadOffset = val;
                DashFallConfigLoader.SaveClientConfig(clientConfig);
            }));

            // Sprint shoulder trail toggle (client preference)
            _actionsSection.Add(MakeToggleRow("SPRINT SHOULDER TRAIL", "Show white shoulder trails while sprinting", clientConfig.EnableSprintShoulderTrail, (val) =>
            {
                clientConfig.EnableSprintShoulderTrail = val;
                DashFallConfigLoader.SaveClientConfig(clientConfig);
            }));

            _actionsSection.Add(MakeFloatRow("TRAIL TIME", "Seconds the trail persists", clientConfig.SprintShoulderTrailTime, 0.05f, 3f, (val) =>
            {
                clientConfig.SprintShoulderTrailTime = val;
                DashFallConfigLoader.SaveClientConfig(clientConfig);
            }));

            _actionsSection.Add(MakeFloatRow("TRAIL WIDTH", "Trail width in meters", clientConfig.SprintShoulderTrailWidth, 0.01f, 0.5f, (val) =>
            {
                clientConfig.SprintShoulderTrailWidth = val;
                DashFallConfigLoader.SaveClientConfig(clientConfig);
            }));

            _actionsSection.Add(MakeHexColorRow("TRAIL START COLOR", "Hex color (#RRGGBB) at trail head", clientConfig.SprintShoulderTrailStartColorHex, (val) =>
            {
                clientConfig.SprintShoulderTrailStartColorHex = val;
                DashFallConfigLoader.SaveClientConfig(clientConfig);
            }));

            _actionsSection.Add(MakeSliderRow("TRAIL START ALPHA", "Opacity at trail head", clientConfig.SprintShoulderTrailStartAlpha, 0f, 1f, (val) =>
            {
                clientConfig.SprintShoulderTrailStartAlpha = val;
                DashFallConfigLoader.SaveClientConfig(clientConfig);
            }));

            _actionsSection.Add(MakeHexColorRow("TRAIL END COLOR", "Hex color (#RRGGBB) at trail tail", clientConfig.SprintShoulderTrailEndColorHex, (val) =>
            {
                clientConfig.SprintShoulderTrailEndColorHex = val;
                DashFallConfigLoader.SaveClientConfig(clientConfig);
            }));

            _actionsSection.Add(MakeSliderRow("TRAIL END ALPHA", "Opacity at trail tail", clientConfig.SprintShoulderTrailEndAlpha, 0f, 1f, (val) =>
            {
                clientConfig.SprintShoulderTrailEndAlpha = val;
                DashFallConfigLoader.SaveClientConfig(clientConfig);
            }));

            // Debug / clip brush toggles (moved to bottom)
            _actionsSection.Add(MakeToggleRow("CLIENT DEBUG LOG", "Enable debug logging to console", clientConfig.EnableClientDebug, (val) =>
            {
                clientConfig.EnableClientDebug = val;
                DashFallConfigLoader.SaveClientConfig(clientConfig);
            }));

            _actionsSection.Add(MakeToggleRow("SHOW ARENA CLIP BRUSHES", "Visualise arena/board collider geometry (debug)", clientConfig.ShowArenaClipBrushes, (val) =>
            {
                clientConfig.ShowArenaClipBrushes = val;
                DashFallConfigLoader.SaveClientConfig(clientConfig);
                CompetitivePuckTweaks.src.ClientClipBrushes.ApplyArena(val);
            }));

            _actionsSection.Add(MakeToggleRow("SHOW PLAYER CLIP BRUSHES", "Visualise player body collider geometry (debug)", clientConfig.ShowPlayerClipBrushes, (val) =>
            {
                clientConfig.ShowPlayerClipBrushes = val;
                DashFallConfigLoader.SaveClientConfig(clientConfig);
                CompetitivePuckTweaks.src.ClientClipBrushes.ApplyPlayer(val);
            }));

            // Check if connected to server
            var features = PoncePuck.Keybinds.ServerBridge.ReceivedFeatures;
            bool hasFeatures = PoncePuck.Keybinds.ServerBridge.HasReceivedFeatures;
            
            if (!hasFeatures)
            {
                var noServerLabel = new UITK.Label("Connect to a server to see settings.");
                noServerLabel.style.fontSize = 18;
                noServerLabel.style.marginTop = 20;
                noServerLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                MakeReadable(noServerLabel);
                _actionsSection.Add(noServerLabel);
            }
            else
            {
                var infoLabel = new UITK.Label("Keybinds for features are in the\nSKATER and GOALIE tabs.\n\nSee SERVER tab for enabled features.");
                infoLabel.style.fontSize = 18;
                infoLabel.style.marginTop = 20;
                infoLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                infoLabel.style.whiteSpace = UITK.WhiteSpace.Normal;
                MakeReadable(infoLabel);
                _actionsSection.Add(infoLabel);
            }
        }

        private UITK.VisualElement MakeToggleRow(string title, string description, bool currentValue, Action<bool> onChanged)
        {
            var row = new UITK.VisualElement();
            row.style.flexDirection = UITK.FlexDirection.Row;
            row.style.alignItems = UITK.Align.Center;
            row.style.height = 50;
            row.style.marginBottom = 4;
            row.style.backgroundColor = new UITK.StyleColor(RowBg);
            row.style.paddingLeft = 12;
            row.style.paddingRight = 12;
            row.style.borderTopLeftRadius = 4;
            row.style.borderTopRightRadius = 4;
            row.style.borderBottomLeftRadius = 4;
            row.style.borderBottomRightRadius = 4;

            var textContainer = new UITK.VisualElement();
            textContainer.style.flexGrow = 1;
            textContainer.style.flexDirection = UITK.FlexDirection.Column;
            textContainer.style.justifyContent = UITK.Justify.Center;

            var label = new UITK.Label(title);
            label.style.fontSize = 24;
            MakeReadable(label);
            textContainer.Add(label);

            if (!string.IsNullOrEmpty(description))
            {
                var descLabel = new UITK.Label(description);
                descLabel.style.fontSize = 16;
                descLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                ForceUIFont(descLabel);
                textContainer.Add(descLabel);
            }
            
            row.Add(textContainer);

            var toggle = new Toggle();
            toggle.value = currentValue;
            toggle.style.width = 40;
            toggle.RegisterValueChangedCallback(evt => {
                onChanged?.Invoke(evt.newValue);
                RefreshActionsUI(); // Refresh to show/hide keybind section
            });
            row.Add(toggle);

            return row;
        }

        private UITK.VisualElement MakeFloatRow(string title, string description, float currentValue, float min, float max, Action<float> onChanged)
        {
            var row = new UITK.VisualElement();
            row.style.flexDirection = UITK.FlexDirection.Row;
            row.style.alignItems = UITK.Align.Center;
            row.style.height = 50;
            row.style.marginBottom = 4;
            row.style.backgroundColor = new UITK.StyleColor(RowBg);
            row.style.paddingLeft = 12;
            row.style.paddingRight = 12;
            row.style.borderTopLeftRadius = 4;
            row.style.borderTopRightRadius = 4;
            row.style.borderBottomLeftRadius = 4;
            row.style.borderBottomRightRadius = 4;

            var textContainer = new UITK.VisualElement();
            textContainer.style.flexGrow = 1;
            textContainer.style.flexShrink = 1;
            textContainer.style.minWidth = 0;
            textContainer.style.flexDirection = UITK.FlexDirection.Column;
            textContainer.style.justifyContent = UITK.Justify.Center;

            var label = new UITK.Label(title);
            label.style.fontSize = 24;
            MakeReadable(label);
            textContainer.Add(label);

            if (!string.IsNullOrEmpty(description))
            {
                var descLabel = new UITK.Label(description);
                descLabel.style.fontSize = 16;
                descLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                descLabel.style.whiteSpace = UITK.WhiteSpace.NoWrap;
                descLabel.style.textOverflow = UITK.TextOverflow.Ellipsis;
                ForceUIFont(descLabel);
                textContainer.Add(descLabel);
            }

            row.Add(textContainer);

            var input = new TextField();
            input.value = currentValue.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            input.style.width = 110;
            input.style.minWidth = 110;
            input.style.flexShrink = 0;
            input.style.height = 34;
            input.style.unityTextAlign = new UITK.StyleEnum<TextAnchor>(TextAnchor.MiddleCenter);
            input.style.backgroundColor = new UITK.StyleColor(TextFieldBg);
            input.style.color = Color.white;
            ForceUIFont(input);
            input.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (!float.TryParse(input.value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                {
                    input.value = currentValue.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                    return;
                }

                parsed = Mathf.Clamp(parsed, min, max);
                input.value = parsed.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                onChanged?.Invoke(parsed);
            });
            row.Add(input);

            return row;
        }

        private UITK.VisualElement MakeSliderRow(string title, string description, float currentValue, float min, float max, Action<float> onChanged)
        {
            var row = new UITK.VisualElement();
            row.style.flexDirection = UITK.FlexDirection.Row;
            row.style.alignItems = UITK.Align.Center;
            row.style.height = 58;
            row.style.marginBottom = 4;
            row.style.backgroundColor = new UITK.StyleColor(RowBg);
            row.style.paddingLeft = 12;
            row.style.paddingRight = 12;
            row.style.borderTopLeftRadius = 4;
            row.style.borderTopRightRadius = 4;
            row.style.borderBottomLeftRadius = 4;
            row.style.borderBottomRightRadius = 4;

            var textContainer = new UITK.VisualElement();
            textContainer.style.flexGrow = 1;
            textContainer.style.flexShrink = 1;
            textContainer.style.minWidth = 0;
            textContainer.style.flexDirection = UITK.FlexDirection.Column;
            textContainer.style.justifyContent = UITK.Justify.Center;

            var label = new UITK.Label(title);
            label.style.fontSize = 24;
            MakeReadable(label);
            textContainer.Add(label);

            if (!string.IsNullOrEmpty(description))
            {
                var descLabel = new UITK.Label(description);
                descLabel.style.fontSize = 16;
                descLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                descLabel.style.whiteSpace = UITK.WhiteSpace.NoWrap;
                descLabel.style.textOverflow = UITK.TextOverflow.Ellipsis;
                ForceUIFont(descLabel);
                textContainer.Add(descLabel);
            }

            row.Add(textContainer);

            var input = new TextField();
            input.value = currentValue.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            input.style.width = 72;
            input.style.minWidth = 72;
            input.style.flexShrink = 0;
            input.style.height = 34;
            input.style.marginRight = 8;
            input.style.unityTextAlign = new UITK.StyleEnum<TextAnchor>(TextAnchor.MiddleCenter);
            input.style.backgroundColor = new UITK.StyleColor(TextFieldBg);
            input.style.color = Color.white;
            ForceUIFont(input);
            row.Add(input);

            var slider = new UITK.Slider(min, max);
            slider.style.width = 170;
            slider.style.minWidth = 170;
            slider.style.flexShrink = 0;
            slider.style.height = 24;
            slider.value = Mathf.Clamp(currentValue, min, max);
            StyleSliderControl(slider);
            bool syncing = false;
            slider.RegisterValueChangedCallback(evt =>
            {
                if (syncing) return;
                float v = Mathf.Clamp(evt.newValue, min, max);
                syncing = true;
                input.value = v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                syncing = false;
                onChanged?.Invoke(v);
            });
            input.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (!float.TryParse(input.value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                {
                    input.value = slider.value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                    return;
                }

                float v = Mathf.Clamp(parsed, min, max);
                syncing = true;
                slider.value = v;
                input.value = v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                syncing = false;
                onChanged?.Invoke(v);
            });
            row.Add(slider);

            return row;
        }

        private static void StyleSliderControl(UITK.Slider slider)
        {
            var tracker = slider.Q<UITK.VisualElement>(className: "unity-base-slider__tracker");
            if (tracker != null)
            {
                tracker.style.backgroundColor = new UITK.StyleColor(new Color(0.18f, 0.18f, 0.18f, 0.95f));
                tracker.style.height = 6;
                tracker.style.borderTopLeftRadius = 3;
                tracker.style.borderTopRightRadius = 3;
                tracker.style.borderBottomLeftRadius = 3;
                tracker.style.borderBottomRightRadius = 3;
            }

            var dragger = slider.Q<UITK.VisualElement>(className: "unity-base-slider__dragger");
            if (dragger != null)
            {
                dragger.style.backgroundColor = new UITK.StyleColor(new Color(0.9f, 0.9f, 0.9f, 1f));
                dragger.style.width = 12;
                dragger.style.height = 12;
                dragger.style.borderTopLeftRadius = 6;
                dragger.style.borderTopRightRadius = 6;
                dragger.style.borderBottomLeftRadius = 6;
                dragger.style.borderBottomRightRadius = 6;
            }
        }

        private UITK.VisualElement MakeHexColorRow(string title, string description, string currentHex, Action<string> onChanged)
        {
            var row = new UITK.VisualElement();
            row.style.flexDirection = UITK.FlexDirection.Row;
            row.style.alignItems = UITK.Align.Center;
            row.style.height = 50;
            row.style.marginBottom = 4;
            row.style.backgroundColor = new UITK.StyleColor(RowBg);
            row.style.paddingLeft = 12;
            row.style.paddingRight = 12;
            row.style.borderTopLeftRadius = 4;
            row.style.borderTopRightRadius = 4;
            row.style.borderBottomLeftRadius = 4;
            row.style.borderBottomRightRadius = 4;

            var textContainer = new UITK.VisualElement();
            textContainer.style.flexGrow = 1;
            textContainer.style.flexShrink = 1;
            textContainer.style.minWidth = 0;
            textContainer.style.flexDirection = UITK.FlexDirection.Column;
            textContainer.style.justifyContent = UITK.Justify.Center;

            var label = new UITK.Label(title);
            label.style.fontSize = 24;
            MakeReadable(label);
            textContainer.Add(label);

            if (!string.IsNullOrEmpty(description))
            {
                var descLabel = new UITK.Label(description);
                descLabel.style.fontSize = 16;
                descLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                descLabel.style.whiteSpace = UITK.WhiteSpace.NoWrap;
                descLabel.style.textOverflow = UITK.TextOverflow.Ellipsis;
                ForceUIFont(descLabel);
                textContainer.Add(descLabel);
            }

            row.Add(textContainer);

            var input = new TextField();
            input.value = string.IsNullOrWhiteSpace(currentHex) ? "#FFFFFF" : currentHex;
            input.style.width = 120;
            input.style.minWidth = 120;
            input.style.flexShrink = 0;
            input.style.height = 34;
            input.style.unityTextAlign = new UITK.StyleEnum<TextAnchor>(TextAnchor.MiddleCenter);
            input.style.backgroundColor = new UITK.StyleColor(TextFieldBg);
            input.style.color = Color.white;
            input.style.whiteSpace = UITK.WhiteSpace.NoWrap;
            ForceUIFont(input);
            input.RegisterCallback<FocusOutEvent>(_ =>
            {
                string normalized = NormalizeHex(input.value);
                if (normalized == null)
                {
                    input.value = string.IsNullOrWhiteSpace(currentHex) ? "#FFFFFF" : currentHex;
                    return;
                }

                input.value = normalized;
                onChanged?.Invoke(normalized);
            });
            row.Add(input);

            return row;
        }

        private static void ApplyLocalPuckScale(float scale)
        {
            if (CompetitiveCompanion.PluginCore.config != null)
            {
                CompetitiveCompanion.PluginCore.config.PuckScale = scale;
            }

            if (PuckManager.Instance == null) return;

            var pucks = PuckManager.Instance.GetPucks();
            if (pucks == null) return;

            foreach (var puck in pucks)
            {
                if (puck == null) continue;
                puck.transform.localScale = Vector3.one * scale;
            }
        }

        private static string NormalizeHex(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string normalized = value.Trim();
            if (!normalized.StartsWith("#")) normalized = "#" + normalized;
            if (!ColorUtility.TryParseHtmlString(normalized, out var parsed)) return null;
            return "#" + ColorUtility.ToHtmlStringRGB(parsed);
        }

        private void AddSectionHeader(string text)
        {
            var header = new UITK.Label(text);
            header.style.fontSize = 24;
            header.style.marginTop = 16;
            header.style.marginBottom = 8;
            header.style.color = new Color(0.9f, 0.9f, 0.5f);
            ForceUIFont(header);
            _actionsSection.Add(header);
        }

        private void AddSubHeader(string text)
        {
            var header = new UITK.Label(text);
            header.style.fontSize = 24;
            header.style.marginTop = 10;
            header.style.marginBottom = 6;
            header.style.marginLeft = 8;
            header.style.color = new Color(0.7f, 0.7f, 0.9f);
            ForceUIFont(header);
            _actionsSection.Add(header);
        }

        private UITK.VisualElement MakeServerConfigRow(string featureName, bool enabled)
        {
            var row = new UITK.VisualElement();
            row.style.flexDirection = UITK.FlexDirection.Row;
            row.style.alignItems = UITK.Align.Center;
            row.style.height = 40;
            row.style.marginBottom = 4;
            row.style.backgroundColor = new UITK.StyleColor(RowBg);
            row.style.paddingLeft = 12;
            row.style.paddingRight = 12;

            var label = new UITK.Label(featureName);
            label.style.flexGrow = 1;
            label.style.fontSize = 24;
            MakeReadable(label);
            row.Add(label);

            var statusLabel = new UITK.Label(enabled ? "ENABLED" : "DISABLED");
            statusLabel.style.fontSize = 24;
            statusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            statusLabel.style.color = enabled ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.9f, 0.4f, 0.4f);
            ForceUIFont(statusLabel);
            row.Add(statusLabel);

            return row;
        }

        private IEnumerable<KeyValuePair<string, bool>> EnumerateBoolFields(object configObject)
        {
            if (configObject == null) yield break;

            var fields = configObject.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(field => field.FieldType == typeof(bool))
                .OrderBy(field => field.Name, StringComparer.Ordinal);

            foreach (var field in fields)
            {
                bool value = false;
                try
                {
                    value = (bool)field.GetValue(configObject);
                }
                catch
                {
                    continue;
                }

                yield return new KeyValuePair<string, bool>(HumanizeBoolFieldName(field.Name), value);
            }
        }

        private static string HumanizeBoolFieldName(string fieldName)
        {
            if (string.IsNullOrWhiteSpace(fieldName)) return "Unknown";
            string text = fieldName;
            if (text.StartsWith("Enable", StringComparison.Ordinal)) text = text.Substring("Enable".Length);
            if (text.StartsWith("Disable", StringComparison.Ordinal)) text = "Disable " + text.Substring("Disable".Length);

            var chars = new List<char>(text.Length + 8);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (i > 0 && char.IsUpper(c) && (char.IsLower(text[i - 1]) || (i + 1 < text.Length && char.IsLower(text[i + 1]))))
                    chars.Add(' ');
                chars.Add(c);
            }

            return new string(chars.ToArray()).Trim();
        }

        private void RefreshActionsUI()
        {
            BuildActionsUI();
        }

        // Enum to distinguish between pressable (action) and holdable (movement) controls
        private enum BindRowType { Pressable, Holdable }

        private UITK.VisualElement MakeBindRow(string action, Func<List<string>> getter, Action<List<string>> setter, 
            Func<string> typeGetter, Action<string> typeSetter, BindRowType rowType, bool enabled = true)
        {
            var row = new UITK.VisualElement();
            row.style.flexDirection = UITK.FlexDirection.Row;
            row.style.alignItems = UITK.Align.Center;
            row.style.height = 50;
            row.style.marginBottom = 8;
            row.style.backgroundColor = new UITK.StyleColor(enabled ? RowBg : DisabledRowBg);
            row.style.paddingLeft = 12; row.style.paddingRight = 10;
            row.style.paddingTop = 8; row.style.paddingBottom = 8;
            row.style.opacity = enabled ? 1f : 0.5f;

            // Label
            var lab = new UITK.Label(action + (enabled ? "" : " <size=12><color=red><b>DISABLED BY SERVER</b></color></size>"));
            lab.style.minWidth = 220;
            lab.style.maxWidth = 220;
            lab.style.unityTextAlign = new UITK.StyleEnum<TextAnchor>(TextAnchor.MiddleLeft);
            lab.style.color = enabled ? Color.white : new Color(0.6f, 0.6f, 0.6f);
            lab.style.whiteSpace = UITK.WhiteSpace.NoWrap;
            lab.style.textOverflow = UITK.TextOverflow.Ellipsis;
            ForceUIFont(lab);
            row.Add(lab);

            // Chips container (shows bound keys)
            var chipsRoot = new UITK.VisualElement();
            chipsRoot.style.flexDirection = UITK.FlexDirection.Row;
            chipsRoot.style.justifyContent = UITK.Justify.FlexEnd;
            chipsRoot.style.alignItems = UITK.Align.Center;
            chipsRoot.style.flexGrow = 1;
            chipsRoot.style.flexShrink = 1;
            chipsRoot.style.minWidth = 0;
            chipsRoot.style.marginLeft = 4;
            chipsRoot.style.marginRight = 8;
            row.Add(chipsRoot);

            // Buttons container
            var right = new UITK.VisualElement();
            right.style.flexDirection = UITK.FlexDirection.Row;
            right.style.alignItems = UITK.Align.Center;
            right.style.flexShrink = 0;
            row.Add(right);

            // BIND button
            var bindBtn = new UITK.Button(() =>
            {
                if (!enabled) return;
                StartChordCapture($"Press keys for {action}", spec =>
                {
                    if (string.IsNullOrEmpty(spec)) return;
                    var cur = getter() ?? new List<string>();
                    if (!cur.Contains(spec))
                    {
                        cur.Add(spec);
                        setter(cur);
                        RefreshChips();
                    }
                });
            });
            StyleRowButton(bindBtn, BTN_W, "BIND");
            bindBtn.SetEnabled(enabled);
            right.Add(bindBtn);

            // Create the appropriate dropdown based on row type
            List<string> choices;
            if (rowType == BindRowType.Pressable)
            {
                choices = new List<string> { "PRESS", "RELEASE", "DOUBLE PRESS", "HOLD" };
            }
            else
            {
                choices = new List<string> { "CONTINUOUS", "TOGGLE" };
            }

            // Get current value and find its index
            var currentType = typeGetter() ?? choices[0];
            int currentIndex = choices.IndexOf(currentType);
            if (currentIndex < 0) currentIndex = 0;

            var dropdown = new UITK.DropdownField(choices, currentIndex);
            dropdown.style.width = 206;
            dropdown.style.height = 34;
            dropdown.style.marginLeft = 4;
            dropdown.SetEnabled(enabled);
            StyleDropdown(dropdown);
            
            // Wire up value change using INotifyValueChanged interface
            dropdown.RegisterCallback<UITK.ChangeEvent<string>>(evt =>
            {
                typeSetter(evt.newValue);
            });
            right.Add(dropdown);

            void RefreshChips()
            {
                chipsRoot.Clear();
                var list = getter() ?? new List<string>();
                for (int i = 0; i < list.Count; i++)
                {
                    var idx = i;
                    chipsRoot.Add(MakeChip(list[i], enabled, () =>
                    {
                        if (!enabled) return;
                        var cur = getter() ?? new List<string>();
                        if (idx >= 0 && idx < cur.Count) cur.RemoveAt(idx);
                        setter(cur);
                        RefreshChips();
                    }));
                }
            }
            RefreshChips();

            return row;
        }

        private void StyleRowButton(UITK.Button btn, int width, string text)
        {
            btn.text = text;
            btn.style.width = width;
            btn.style.height = 34;
            btn.style.marginLeft = 4;
            btn.style.backgroundColor = new UITK.StyleColor(ButtonBg);
            btn.style.unityTextAlign = new UITK.StyleEnum<TextAnchor>(TextAnchor.MiddleCenter);
            MakeReadable(btn);
            AddButtonFlash(btn);
        }

        private void StyleDropdown(UITK.DropdownField dropdown)
        {
            dropdown.style.backgroundColor = new UITK.StyleColor(TextFieldBg);
            dropdown.style.color = Color.white;
            ForceUIFont(dropdown);
            
            // Style the label inside the dropdown using Query
            var label = UITK.UQueryExtensions.Q<UITK.Label>(dropdown);
            if (label != null)
            {
                label.style.color = Color.white;
                ForceUIFont(label);
            }
        }

        private UITK.VisualElement MakeChip(string text, bool enabled, Action onRemove)
        {
            var chip = new UITK.VisualElement();
            chip.style.flexDirection = UITK.FlexDirection.Row;
            chip.style.alignItems = UITK.Align.Center;
            chip.style.backgroundColor = new UITK.StyleColor(new Color32(80, 80, 80, 255));
            chip.style.paddingLeft = 8; chip.style.paddingRight = 4;
            chip.style.paddingTop = 4; chip.style.paddingBottom = 4;
            chip.style.marginRight = 4;
            chip.style.borderTopLeftRadius = 4; chip.style.borderTopRightRadius = 4;
            chip.style.borderBottomLeftRadius = 4; chip.style.borderBottomRightRadius = 4;
            chip.style.opacity = enabled ? 1f : 0.6f;

            var label = new UITK.Label(text);
            label.style.fontSize = 14;
            MakeReadable(label);
            chip.Add(label);

            var xBtn = new UITK.Button(onRemove) { text = "×" };
            xBtn.style.width = 20; xBtn.style.height = 20;
            xBtn.style.marginLeft = 4;
            xBtn.style.backgroundColor = new UITK.StyleColor(new Color32(100, 100, 100, 255));
            xBtn.style.fontSize = 14;
            xBtn.style.unityTextAlign = new UITK.StyleEnum<TextAnchor>(TextAnchor.MiddleCenter);
            xBtn.style.paddingLeft = 0; xBtn.style.paddingRight = 0;
            xBtn.style.paddingTop = 0; xBtn.style.paddingBottom = 0;
            xBtn.SetEnabled(enabled);
            MakeReadable(xBtn);
            if (enabled) AddChipButtonFlash(xBtn);
            chip.Add(xBtn);

            return chip;
        }

        private static readonly Color32 ChipXBg = new Color32(100, 100, 100, 255);
        
        private static void AddChipButtonFlash(UITK.Button btn)
        {
            btn.RegisterCallback<UITK.PointerEnterEvent>(_ =>
            {
                btn.style.backgroundColor = new UITK.StyleColor(new Color32(180, 80, 80, 255));
                btn.style.color = Color.white;
            });
            btn.RegisterCallback<UITK.PointerLeaveEvent>(_ =>
            {
                btn.style.backgroundColor = new UITK.StyleColor(ChipXBg);
                btn.style.color = Color.white;
            });
        }

        private static void AddButtonFlash(UITK.Button btn)
        {
            btn.RegisterCallback<UITK.PointerEnterEvent>(_ =>
            {
                btn.style.backgroundColor = Color.white;
                btn.style.color = Color.black;
            });
            btn.RegisterCallback<UITK.PointerLeaveEvent>(_ =>
            {
                btn.style.backgroundColor = new UITK.StyleColor(ButtonBg);
                btn.style.color = Color.white;
            });
        }

        private void ResetToDefaults()
        {
            // Skater keybinds
            _skater.divekey = new List<string> { "F" };
            _skater.twistleftkey = new List<string> { "Z" };
            _skater.twistrightkey = new List<string> { "C" };
            _skater.slideinfluenceleftkey = new List<string> { "Z" };
            _skater.slideinfluencerightkey = new List<string> { "C" };
            _skater.slideinfluenceforwardkey = new List<string> { "W" };
            _skater.slideinfluencebackwardkey = new List<string> { "S" };
            
            // Skater action types
            _skater.divekeytype = "PRESS";
            _skater.twistleftkeytype = "DOUBLE PRESS";
            _skater.twistrightkeytype = "DOUBLE PRESS";
            _skater.slideinfluenceleftkeytype = "CONTINUOUS";
            _skater.slideinfluencerightkeytype = "CONTINUOUS";
            _skater.slideinfluenceforwardkeytype = "CONTINUOUS";
            _skater.slideinfluencebackwardkeytype = "CONTINUOUS";
            
            // Goalie keybinds
            _goalie.divekey = new List<string> { "F" };
            _goalie.standingdashleftkey = new List<string> { "Q" };
            _goalie.standingdashrightkey = new List<string> { "E" };
            _goalie.twistleftkey = new List<string> { "Z" };
            _goalie.twistrightkey = new List<string> { "C" };
            _goalie.slideinfluenceleftkey = new List<string> { "Z" };
            _goalie.slideinfluencerightkey = new List<string> { "C" };
            _goalie.slideinfluenceforwardkey = new List<string> { "W" };
            _goalie.slideinfluencebackwardkey = new List<string> { "S" };
            
            // Goalie action types
            _goalie.divekeytype = "PRESS";
            _goalie.standingdashleftkeytype = "PRESS";
            _goalie.standingdashrightkeytype = "PRESS";
            _goalie.twistleftkeytype = "DOUBLE PRESS";
            _goalie.twistrightkeytype = "DOUBLE PRESS";
            _goalie.slideinfluenceleftkeytype = "CONTINUOUS";
            _goalie.slideinfluencerightkeytype = "CONTINUOUS";
            _goalie.slideinfluenceforwardkeytype = "CONTINUOUS";
            _goalie.slideinfluencebackwardkeytype = "CONTINUOUS";
        }

        // ========== PANEL OPEN/CLOSE ==========
        private void OpenDashFallPanel()
        {
            BuildDashFallPanel();
            if (_dfPanel == null) return;

            _dfBackdrop.style.display = UITK.DisplayStyle.Flex;
            _dfPanel.style.display = UITK.DisplayStyle.Flex;

            // Refresh chips to show current bindings
            RefreshActionsUI();

            // Unlock cursor
            SaveCursorState();
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            // Menu buttons are already hidden by hub, no need to hide them again
        }

        private void CloseDashFallPanel()
        {
            if (_isCapturing)
            {
                CancelChordCapture();
                return;
            }

            // Check if panel is already closed - don't re-open hub if so
            bool wasVisible = _dfPanel != null && _dfPanel.style.display == UITK.DisplayStyle.Flex;

            if (_dfPanel != null) _dfPanel.style.display = UITK.DisplayStyle.None;
            if (_dfBackdrop != null) _dfBackdrop.style.display = UITK.DisplayStyle.None;

            // Only return to hub if the panel was actually visible
            if (!wasVisible) return;

            // Don't restore cursor state or menu buttons - hub will manage them
            ConfigManager.Dbg("Panel closed, returning to hub");
            
            // Return to ModMenuHub
            try
            {
                PonceMods.Shared.ModMenuHub.OpenPanel();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[COMPADJUST] Failed to open hub: {e}");
                // Fallback: restore state manually
                RestoreCursorState();
                HideBackgroundMenuButtons(false);
            }
        }
        
        /// <summary>
        /// Fully close panel without returning to hub (ESC behavior).
        /// </summary>
        private void FullCloseDashFallPanel()
        {
            if (_isCapturing)
            {
                CancelChordCapture();
                return;
            }

            if (_dfPanel != null) _dfPanel.style.display = UITK.DisplayStyle.None;
            if (_dfBackdrop != null) _dfBackdrop.style.display = UITK.DisplayStyle.None;

            ConfigManager.Dbg("Panel fully closed via ESC");
            
            // Use ModMenuHub's FullClose to handle cursor and menu buttons properly
            try
            {
                PonceMods.Shared.ModMenuHub.FullClose();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[COMPADJUST] Failed to full close: {e}");
                // Fallback: restore state manually
                RestoreCursorState();
                HideBackgroundMenuButtons(false);
            }
        }

        private void HideBackgroundMenuButtons(bool hide)
        {
            if (hide)
            {
                var root = _doc?.rootVisualElement ?? _lastRoot;
                if (root == null) return;

                _hiddenMenuButtons.Clear();
                foreach (var b in UITK.UQueryExtensions.Query<UITK.Button>(root).ToList())
                {
                    if (b == null) continue;
                    if ((_dfPanel != null && IsUnder(b, _dfPanel)) ||
                        (_dfBackdrop != null && IsUnder(b, _dfBackdrop)) ||
                        (_captureOverlay != null && IsUnder(b, _captureOverlay)))
                        continue;

                    if (b.resolvedStyle.display != UITK.DisplayStyle.None)
                    {
                        _hiddenMenuButtons.Add(b);
                        b.style.display = UITK.DisplayStyle.None;
                    }
                }
            }
            else
            {
                // Restore hidden buttons - don't need root for this
                foreach (var b in _hiddenMenuButtons)
                    if (b != null) b.style.display = UITK.DisplayStyle.Flex;
                _hiddenMenuButtons.Clear();
            }
        }

        private static bool IsUnder(UITK.VisualElement child, UITK.VisualElement ancestor)
        {
            for (var p = child; p != null; p = p.parent)
                if (p == ancestor) return true;
            return false;
        }

        // ========== CHORD CAPTURE ==========
        private void EnsureCaptureOverlay()
        {
            if (_captureOverlay != null) return;

            var root = _doc?.rootVisualElement ?? _lastRoot;
            if (root == null) return;

            _captureOverlay = new UITK.VisualElement();
            _captureOverlay.style.position = UITK.Position.Absolute;
            _captureOverlay.style.left = 0; _captureOverlay.style.right = 0;
            _captureOverlay.style.top = 0; _captureOverlay.style.bottom = 0;
            _captureOverlay.style.backgroundColor = new UITK.StyleColor(new Color(0.1f, 0.1f, 0.15f, 0.95f));
            _captureOverlay.style.display = UITK.DisplayStyle.None;
            _captureOverlay.pickingMode = UITK.PickingMode.Position;
            ForceUIFont(_captureOverlay);

            var centerContainer = new UITK.VisualElement();
            centerContainer.style.position = UITK.Position.Absolute;
            centerContainer.style.left = new UITK.Length(50, UITK.LengthUnit.Percent);
            centerContainer.style.top = new UITK.Length(50, UITK.LengthUnit.Percent);
            centerContainer.style.translate = new UITK.Translate(
                new UITK.Length(-50, UITK.LengthUnit.Percent),
                new UITK.Length(-50, UITK.LengthUnit.Percent), 0);
            centerContainer.style.alignItems = UITK.Align.Center;
            centerContainer.style.justifyContent = UITK.Justify.Center;
            centerContainer.style.flexDirection = UITK.FlexDirection.Column;

            var title = new UITK.Label("KEY REBIND");
            title.style.fontSize = 72;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.unityTextAlign = new UITK.StyleEnum<TextAnchor>(TextAnchor.MiddleCenter);
            title.style.marginBottom = 32;
            MakeReadable(title);
            centerContainer.Add(title);

            _captureLabel = new UITK.Label("Press a key or combination to bind.");
            _captureLabel.style.fontSize = 24;
            _captureLabel.style.unityTextAlign = new UITK.StyleEnum<TextAnchor>(TextAnchor.MiddleCenter);
            _captureLabel.style.whiteSpace = UITK.WhiteSpace.Normal;
            _captureLabel.style.maxWidth = 600;
            MakeReadable(_captureLabel);
            centerContainer.Add(_captureLabel);

            _captureOverlay.Add(centerContainer);
            root.Add(_captureOverlay);
            _captureOverlay.BringToFront();
        }

        private void StartChordCapture(string prompt, Action<string> onCaptured)
        {
            _onChordCaptured = onCaptured;
            _isCapturing = true;

            EnsureCaptureOverlay();
            HidePanelDuringCapture(true);
            // Don't call HideBackgroundMenuButtons here - they're already hidden from panel open

            if (_captureLabel != null) _captureLabel.text = "Press a key or combination to bind.";
            _captureOverlay.style.display = UITK.DisplayStyle.Flex;
            StartCoroutine(CaptureChordRoutine());
        }

        private void CancelChordCapture()
        {
            _isCapturing = false;
            if (_captureOverlay != null) _captureOverlay.style.display = UITK.DisplayStyle.None;
            HidePanelDuringCapture(false);
            _onChordCaptured = null;

            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        private void HidePanelDuringCapture(bool hide)
        {
            if (_dfPanel == null) return;

            if (hide)
            {
                _panelHiddenForCapture = (_dfPanel.style.display == UITK.DisplayStyle.Flex);
                _dfPanel.style.display = UITK.DisplayStyle.None;
                if (_dfBackdrop != null) _dfBackdrop.style.display = UITK.DisplayStyle.Flex;
            }
            else
            {
                if (_panelHiddenForCapture)
                {
                    _dfPanel.style.display = UITK.DisplayStyle.Flex;
                    _panelHiddenForCapture = false;
                }
            }
        }

        private static bool IsModifierKey(KeyCode k) =>
            k == KeyCode.LeftShift || k == KeyCode.RightShift ||
            k == KeyCode.LeftControl || k == KeyCode.RightControl ||
            k == KeyCode.LeftAlt || k == KeyCode.RightAlt;

        private KeyChord SnapshotChord()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            bool ctrl = (kb?.leftCtrlKey?.isPressed ?? false) || (kb?.rightCtrlKey?.isPressed ?? false);
            bool shift = (kb?.leftShiftKey?.isPressed ?? false) || (kb?.rightShiftKey?.isPressed ?? false);
            bool alt = (kb?.leftAltKey?.isPressed ?? false) || (kb?.rightAltKey?.isPressed ?? false);

            var keys = new List<KeyCode>();
            foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
            {
                if (!IsAllowedKey(k) || IsModifierKey(k)) continue;
                if (IsKeyDown(k)) keys.Add(k);
            }
            keys.Sort((a, b) => a.CompareTo(b));
            return new KeyChord { Keys = keys.ToArray(), Ctrl = ctrl, Shift = shift, Alt = alt };
        }

        private static bool IsAllowedKey(KeyCode k)
        {
            if (k == KeyCode.None || k == KeyCode.Escape) return false;
            // Allow mouse buttons (Mouse0-Mouse6 = 323-329)
            return true;
        }

        private static string KeyChordToSpec(KeyChord kc)
        {
            var sb = new System.Text.StringBuilder();
            if (kc.Ctrl) sb.Append("Ctrl+");
            if (kc.Shift) sb.Append("Shift+");
            if (kc.Alt) sb.Append("Alt+");
            if (kc.Keys != null && kc.Keys.Length > 0)
                sb.Append(string.Join("+", kc.Keys.Select(k => GetFriendlyKeyName(k))));
            return sb.ToString();
        }
        
        private static string GetFriendlyKeyName(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.Mouse0: return "LMB";
                case KeyCode.Mouse1: return "RMB";
                case KeyCode.Mouse2: return "MMB";
                case KeyCode.Mouse3: return "MB4";
                case KeyCode.Mouse4: return "MB5";
                default: return k.ToString();
            }
        }

        private IEnumerator CaptureChordRoutine()
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            var kb = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;

            float startTimeout = Time.unscaledTime + 5f;
            bool started = false;
            float windowEnd = 2f;

            KeyChord best = default;
            int bestWeight = -1;
            float lastBestAt = 0f;

            bool HasAnyInputDown() =>
                (kb != null && kb.anyKey.isPressed) ||
                (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed ||
                                   (mouse.forwardButton?.isPressed ?? false) || (mouse.backButton?.isPressed ?? false)));

            int Weight(KeyChord kc)
            {
                int w = (kc.Keys?.Length ?? 0);
                if (kc.Ctrl) w++;
                if (kc.Shift) w++;
                if (kc.Alt) w++;
                return w;
            }

            while (_isCapturing && Time.unscaledTime < startTimeout)
            {
                if (kb?.escapeKey?.wasPressedThisFrame ?? false)
                {
                    CancelChordCapture();
                    yield break;
                }

                if (!started)
                {
                    if ((kb?.anyKey.wasPressedThisFrame ?? false) ||
                        (mouse?.leftButton.wasPressedThisFrame ?? false) ||
                        (mouse?.rightButton.wasPressedThisFrame ?? false) ||
                        (mouse?.middleButton.wasPressedThisFrame ?? false) ||
                        (mouse?.forwardButton?.wasPressedThisFrame ?? false) ||
                        (mouse?.backButton?.wasPressedThisFrame ?? false))
                    {
                        started = true;
                        windowEnd = Time.unscaledTime + 1.0f;
                        if (_captureLabel != null) _captureLabel.text = "Release keys to confirm...";
                    }
                }
                else
                {
                    var kc = SnapshotChord();
                    bool any = (kc.Keys?.Length ?? 0) > 0 || kc.Ctrl || kc.Shift || kc.Alt;
                    if (any)
                    {
                        int w = Weight(kc);
                        if (w > bestWeight)
                        {
                            best = kc; bestWeight = w; lastBestAt = Time.unscaledTime;
                            if (_captureLabel != null) _captureLabel.text = KeyChordToSpec(kc) + " - Release to confirm";
                        }
                    }

                    bool allReleased = !HasAnyInputDown();
                    if (bestWeight >= 0 && (allReleased || Time.unscaledTime >= windowEnd || Time.unscaledTime - lastBestAt > 0.15f))
                    {
                        _onChordCaptured?.Invoke(KeyChordToSpec(best));
                        _isCapturing = false;
                        if (_captureOverlay != null) _captureOverlay.style.display = UITK.DisplayStyle.None;
                        HidePanelDuringCapture(false);
                        yield break;
                    }
                }

                yield return null;
            }

            CancelChordCapture();
        }
    }
}

