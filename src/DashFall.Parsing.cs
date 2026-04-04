// DashFall.Parsing.cs - Keybind chord parsing utilities
// Based on PoncePlayerInput parsing system

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DashFallMod.Client
{
    public struct KeyChord : IEquatable<KeyChord>
    {
        public KeyCode[] Keys;
        public bool Ctrl, Shift, Alt;

        public bool Equals(KeyChord other)
        {
            if (Ctrl != other.Ctrl || Shift != other.Shift || Alt != other.Alt) return false;
            if (Keys == null && other.Keys == null) return true;
            if (Keys == null || other.Keys == null) return false;
            if (Keys.Length != other.Keys.Length) return false;
            for (int i = 0; i < Keys.Length; i++)
                if (Keys[i] != other.Keys[i]) return false;
            return true;
        }

        public override bool Equals(object o) => o is KeyChord && Equals((KeyChord)o);

        public override int GetHashCode()
        {
            int h = (Ctrl ? 1 : 0) ^ (Shift ? 2 : 0) ^ (Alt ? 4 : 0);
            if (Keys != null)
                for (int i = 0; i < Keys.Length; i++)
                    h = (h * 397) ^ (int)Keys[i];
            return h;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            if (Ctrl) sb.Append("Ctrl+");
            if (Shift) sb.Append("Shift+");
            if (Alt) sb.Append("Alt+");
            if (Keys != null)
                sb.Append(string.Join("+", Keys.Select(k => GetFriendlyKeyName(k)).ToArray()));
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
    }

    public static class DashFallParsing
    {
        private static string NormalizeKeySpec(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            return s.Replace("\"", "").Trim();
        }

        private static bool IsAllowedKey(KeyCode k)
        {
            if (k >= KeyCode.A && k <= KeyCode.Z) return true;
            if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) return true;
            if (k >= KeyCode.F1 && k <= KeyCode.F12) return true;

            switch (k)
            {
                case KeyCode.Space: case KeyCode.Tab: case KeyCode.Escape:
                case KeyCode.LeftShift: case KeyCode.RightShift:
                case KeyCode.LeftControl: case KeyCode.RightControl:
                case KeyCode.LeftAlt: case KeyCode.RightAlt:
                case KeyCode.UpArrow: case KeyCode.DownArrow:
                case KeyCode.LeftArrow: case KeyCode.RightArrow:
                case KeyCode.BackQuote: case KeyCode.Minus: case KeyCode.Equals:
                case KeyCode.LeftBracket: case KeyCode.RightBracket:
                case KeyCode.Semicolon: case KeyCode.Quote:
                case KeyCode.Comma: case KeyCode.Period:
                case KeyCode.Slash: case KeyCode.Backslash:
                case KeyCode.Mouse0: case KeyCode.Mouse1: case KeyCode.Mouse2: case KeyCode.Mouse3:
                case KeyCode.Mouse4:
                case KeyCode.Keypad0: case KeyCode.Keypad1: case KeyCode.Keypad2:
                case KeyCode.Keypad3: case KeyCode.Keypad4: case KeyCode.Keypad5:
                case KeyCode.Keypad6: case KeyCode.Keypad7: case KeyCode.Keypad8:
                case KeyCode.Keypad9:
                    return true;
            }
            return false;
        }

        private static bool TryParseKeyCode(string s, out KeyCode key)
        {
            key = KeyCode.None;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();

            // Single char
            if (s.Length == 1)
            {
                char c = s[0];
                char up = char.ToUpperInvariant(c);
                
                if (up >= 'A' && up <= 'Z')
                {
                    key = (KeyCode)Enum.Parse(typeof(KeyCode), up.ToString());
                    return true;
                }

                if (c >= '0' && c <= '9')
                {
                    key = (KeyCode)Enum.Parse(typeof(KeyCode), "Alpha" + c);
                    return true;
                }

                // Punctuation
                switch (c)
                {
                    case '`': key = KeyCode.BackQuote; return true;
                    case '-': key = KeyCode.Minus; return true;
                    case '=': key = KeyCode.Equals; return true;
                    case '[': key = KeyCode.LeftBracket; return true;
                    case ']': key = KeyCode.RightBracket; return true;
                    case ';': key = KeyCode.Semicolon; return true;
                    case '\'': key = KeyCode.Quote; return true;
                    case ',': key = KeyCode.Comma; return true;
                    case '.': key = KeyCode.Period; return true;
                    case '/': key = KeyCode.Slash; return true;
                    case '\\': key = KeyCode.Backslash; return true;
                    case ' ': key = KeyCode.Space; return true;
                }
            }

            // Aliases
            string us = s.ToUpperInvariant();
            if (us == "NUM0" || us == "NP0" || us == "KP0") { key = KeyCode.Keypad0; return true; }
            if (us == "NUM1" || us == "NP1" || us == "KP1") { key = KeyCode.Keypad1; return true; }
            if (us == "NUM2" || us == "NP2" || us == "KP2") { key = KeyCode.Keypad2; return true; }
            if (us == "NUM3" || us == "NP3" || us == "KP3") { key = KeyCode.Keypad3; return true; }
            if (us == "NUM4" || us == "NP4" || us == "KP4") { key = KeyCode.Keypad4; return true; }
            if (us == "NUM5" || us == "NP5" || us == "KP5") { key = KeyCode.Keypad5; return true; }
            if (us == "NUM6" || us == "NP6" || us == "KP6") { key = KeyCode.Keypad6; return true; }
            if (us == "NUM7" || us == "NP7" || us == "KP7") { key = KeyCode.Keypad7; return true; }
            if (us == "NUM8" || us == "NP8" || us == "KP8") { key = KeyCode.Keypad8; return true; }
            if (us == "NUM9" || us == "NP9" || us == "KP9") { key = KeyCode.Keypad9; return true; }

            // mouse aliases - convert to correct KeyCode values
            // LMB/RMB/MMB are standard aliases
            if (string.Equals(s, "LMB", StringComparison.OrdinalIgnoreCase)) { key = KeyCode.Mouse0; return true; }
            if (string.Equals(s, "RMB", StringComparison.OrdinalIgnoreCase)) { key = KeyCode.Mouse1; return true; }
            if (string.Equals(s, "MMB", StringComparison.OrdinalIgnoreCase)) { key = KeyCode.Mouse2; return true; }
            // MB4/MB5 for side buttons (user-friendly naming: MB4=forward, MB5=back)
            if (string.Equals(s, "MB4", StringComparison.OrdinalIgnoreCase)) { key = KeyCode.Mouse3; return true; }
            if (string.Equals(s, "MB5", StringComparison.OrdinalIgnoreCase)) { key = KeyCode.Mouse4; return true; }
            // Allow "Mouse4"/"Mouse5" as aliases for Mouse3/Mouse4 (1-indexed user expectation)
            if (string.Equals(s, "Mouse4", StringComparison.OrdinalIgnoreCase)) { key = KeyCode.Mouse3; return true; }
            if (string.Equals(s, "Mouse5", StringComparison.OrdinalIgnoreCase)) { key = KeyCode.Mouse4; return true; }

            if (s.Equals("LCTRL", StringComparison.OrdinalIgnoreCase)) { key = KeyCode.LeftControl; return true; }
            if (s.Equals("RCTRL", StringComparison.OrdinalIgnoreCase)) { key = KeyCode.RightControl; return true; }
            if (s.Equals("LSHIFT", StringComparison.OrdinalIgnoreCase)) { key = KeyCode.LeftShift; return true; }
            if (s.Equals("RSHIFT", StringComparison.OrdinalIgnoreCase)) { key = KeyCode.RightShift; return true; }
            if (s.Equals("LALT", StringComparison.OrdinalIgnoreCase)) { key = KeyCode.LeftAlt; return true; }
            if (s.Equals("RALT", StringComparison.OrdinalIgnoreCase)) { key = KeyCode.RightAlt; return true; }

            if (Enum.TryParse<KeyCode>(s, true, out key))
                return IsAllowedKey(key);
            
            return false;
        }

        public static bool TryParseChord(string spec, out KeyChord chord)
        {
            chord = default;
            spec = NormalizeKeySpec(spec);
            if (string.IsNullOrEmpty(spec)) return false;

            var tokens = spec.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            bool ctrl = false, shift = false, alt = false;
            var keys = new List<KeyCode>();

            for (int i = 0; i < tokens.Length; i++)
            {
                var t = tokens[i].Trim();
                var up = t.ToUpperInvariant();

                if (up == "CTRL" || up == "CONTROL" || up == "CTL") { ctrl = true; continue; }
                if (up == "SHIFT") { shift = true; continue; }
                if (up == "ALT" || up == "OPTION" || up == "OPT") { alt = true; continue; }

                if (TryParseKeyCode(t, out KeyCode k))
                    keys.Add(k);
            }

            // Allow modifier-only chords
            if (keys.Count == 0 && (ctrl || shift || alt))
            {
                chord = new KeyChord { Keys = Array.Empty<KeyCode>(), Ctrl = ctrl, Shift = shift, Alt = alt };
                return true;
            }

            if (keys.Count == 0) return false;

            chord = new KeyChord { Keys = keys.ToArray(), Ctrl = ctrl, Shift = shift, Alt = alt };
            Array.Sort(chord.Keys, (a, b) => a.CompareTo(b));
            return true;
        }

        public static bool TryGetInputPath(KeyCode kc, out string path)
        {
            path = null;

            if (kc >= KeyCode.A && kc <= KeyCode.Z)
            {
                path = "<Keyboard>/" + kc.ToString().ToLowerInvariant();
                return true;
            }

            if (kc >= KeyCode.Alpha0 && kc <= KeyCode.Alpha9)
            {
                int d = (int)kc - (int)KeyCode.Alpha0;
                path = "<Keyboard>/" + d;
                return true;
            }

            if (kc >= KeyCode.Keypad0 && kc <= KeyCode.Keypad9)
            {
                int d = (int)kc - (int)KeyCode.Keypad0;
                path = "<Keyboard>/numpad" + d;
                return true;
            }

            if (kc >= KeyCode.F1 && kc <= KeyCode.F12)
            {
                int n = (int)kc - (int)KeyCode.F1 + 1;
                path = "<Keyboard>/f" + n;
                return true;
            }

            switch (kc)
            {
                case KeyCode.Space: path = "<Keyboard>/space"; return true;
                case KeyCode.Tab: path = "<Keyboard>/tab"; return true;
                case KeyCode.Escape: path = "<Keyboard>/escape"; return true;
                case KeyCode.LeftShift: path = "<Keyboard>/leftShift"; return true;
                case KeyCode.RightShift: path = "<Keyboard>/rightShift"; return true;
                case KeyCode.LeftControl: path = "<Keyboard>/leftCtrl"; return true;
                case KeyCode.RightControl: path = "<Keyboard>/rightCtrl"; return true;
                case KeyCode.LeftAlt: path = "<Keyboard>/leftAlt"; return true;
                case KeyCode.RightAlt: path = "<Keyboard>/rightAlt"; return true;
                case KeyCode.UpArrow: path = "<Keyboard>/upArrow"; return true;
                case KeyCode.DownArrow: path = "<Keyboard>/downArrow"; return true;
                case KeyCode.LeftArrow: path = "<Keyboard>/leftArrow"; return true;
                case KeyCode.RightArrow: path = "<Keyboard>/rightArrow"; return true;
                case KeyCode.BackQuote: path = "<Keyboard>/backquote"; return true;
                case KeyCode.Minus: path = "<Keyboard>/minus"; return true;
                case KeyCode.Equals: path = "<Keyboard>/equals"; return true;
                case KeyCode.LeftBracket: path = "<Keyboard>/leftBracket"; return true;
                case KeyCode.RightBracket: path = "<Keyboard>/rightBracket"; return true;
                case KeyCode.Semicolon: path = "<Keyboard>/semicolon"; return true;
                case KeyCode.Quote: path = "<Keyboard>/quote"; return true;
                case KeyCode.Comma: path = "<Keyboard>/comma"; return true;
                case KeyCode.Period: path = "<Keyboard>/period"; return true;
                case KeyCode.Slash: path = "<Keyboard>/slash"; return true;
                case KeyCode.Backslash: path = "<Keyboard>/backslash"; return true;
                case KeyCode.Mouse0: path = "<Mouse>/leftButton"; return true;
                case KeyCode.Mouse1: path = "<Mouse>/rightButton"; return true;
                case KeyCode.Mouse2: path = "<Mouse>/middleButton"; return true;
                case KeyCode.Mouse3: path = "<Mouse>/forwardButton"; return true;
                case KeyCode.Mouse4: path = "<Mouse>/backButton"; return true;
            }
            return false;
        }
    }
}
