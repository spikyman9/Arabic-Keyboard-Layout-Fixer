// Arabic Keyboard Fixer - fixes text typed with the wrong Arabic/English keyboard layout (Alt+Q).
// Build: see build.ps1 (uses the C# compiler that ships with .NET Framework 4.x, C# 5 syntax only).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Media;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Arabic Keyboard Fixer")]
[assembly: AssemblyDescription("Fix text typed with the wrong Arabic/English keyboard layout (Alt+Q)")]
[assembly: AssemblyProduct("Arabic Keyboard Fixer")]
[assembly: AssemblyCompany("Mohamed Rashad")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Mohamed Rashad")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

namespace ArabicKeyboardFixer
{
    // ------------------------------------------------------------------ entry point

    static class Program
    {
        private const string MutexName = "ArabicKeyboardFixer_Global_SingleInstance_Mutex_2026";

        [STAThread]
        static void Main(string[] args)
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show(
                        "البرنامج شغال بالفعل (الأيقونة جنب الساعة).\n\nاضغط Alt + Q في أي مكان كتابة لتحويل النص.",
                        AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Logger.Error("UI thread exception", e.Exception); };
                AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Logger.Error("Unhandled exception", e.ExceptionObject as Exception); };

                bool silent = Array.Exists(args, delegate(string a) { return string.Equals(a, "--autostart", StringComparison.OrdinalIgnoreCase); });

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Logger.Info("Started v" + AppInfo.Version + (silent ? " (autostart)" : ""));
                Application.Run(new TrayApp(silent));
                Logger.Info("Exited");
            }
        }
    }

    public static class AppInfo
    {
        public const string Name = "مُحوّل الكيبورد (Alt + Q)";

        public static string Version
        {
            get
            {
                System.Version v = Assembly.GetExecutingAssembly().GetName().Version;
                return v.Major + "." + v.Minor + "." + v.Build;
            }
        }

        public static string DataDir
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArabicKeyboardFixer");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return dir;
            }
        }
    }

    // ------------------------------------------------------------------ logging (never logs the user's text)

    public static class Logger
    {
        private static readonly object Sync = new object();
        private const long MaxSize = 512 * 1024;

        public static string LogPath { get { return Path.Combine(AppInfo.DataDir, "log.txt"); } }

        public static void Info(string message) { Write("INFO", message, null); }
        public static void Error(string message, Exception ex) { Write("ERROR", message, ex); }

        private static void Write(string level, string message, Exception ex)
        {
            try
            {
                lock (Sync)
                {
                    string path = LogPath;
                    FileInfo fi = new FileInfo(path);
                    if (fi.Exists && fi.Length > MaxSize)
                    {
                        string old = path + ".old";
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(path, old);
                    }
                    string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + level + "] " + message;
                    if (ex != null) line += " :: " + ex.GetType().Name + ": " + ex.Message;
                    File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // Logging must never crash the app.
            }
        }
    }

    // ------------------------------------------------------------------ settings

    public class AppSettings
    {
        public bool AutoSwitchLanguage { get; set; }
        public bool AutoSelectLine { get; set; }
        public bool PlaySound { get; set; }

        private static string ConfigPath { get { return Path.Combine(AppInfo.DataDir, "settings.ini"); } }

        public AppSettings()
        {
            AutoSwitchLanguage = true;
            AutoSelectLine = true;
            PlaySound = false;
        }

        public static AppSettings Load()
        {
            AppSettings s = new AppSettings();
            try
            {
                if (!File.Exists(ConfigPath)) return s;
                foreach (string line in File.ReadAllLines(ConfigPath))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    bool val;
                    if (!bool.TryParse(line.Substring(eq + 1).Trim(), out val)) continue;
                    if (key == "AutoSwitchLanguage") s.AutoSwitchLanguage = val;
                    else if (key == "AutoSelectLine") s.AutoSelectLine = val;
                    else if (key == "PlaySound") s.PlaySound = val;
                }
            }
            catch (Exception ex) { Logger.Error("Failed to load settings", ex); }
            return s;
        }

        public void Save()
        {
            try
            {
                File.WriteAllText(ConfigPath,
                    "AutoSwitchLanguage=" + AutoSwitchLanguage + Environment.NewLine +
                    "AutoSelectLine=" + AutoSelectLine + Environment.NewLine +
                    "PlaySound=" + PlaySound + Environment.NewLine);
            }
            catch (Exception ex) { Logger.Error("Failed to save settings", ex); }
        }
    }

    // ------------------------------------------------------------------ start with Windows (registry is the single source of truth)

    public static class StartupManager
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        private const string ValueName = "ArabicKeyboardFixer";

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (k == null || k.GetValue(ValueName) == null) return false;
                }
                // The user can disable an entry from Task Manager / Settings without deleting it.
                using (RegistryKey a = Registry.CurrentUser.OpenSubKey(ApprovedKey, false))
                {
                    if (a != null)
                    {
                        byte[] data = a.GetValue(ValueName) as byte[];
                        if (data != null && data.Length > 0 && (data[0] & 1) == 1) return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to read startup state", ex);
                return false;
            }
        }

        public static bool SetEnabled(bool enable)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (enable) k.SetValue(ValueName, "\"" + Application.ExecutablePath + "\" --autostart");
                    else k.DeleteValue(ValueName, false);
                }
                using (RegistryKey a = Registry.CurrentUser.OpenSubKey(ApprovedKey, true))
                {
                    if (a != null) a.DeleteValue(ValueName, false);
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to change startup state", ex);
                return false;
            }
        }
    }

    // ------------------------------------------------------------------ layout conversion (pure logic, unit-tested)

    public enum Script { None, Latin, Arabic }

    public static class KeyboardConverter
    {
        // English key -> character produced by the Arabic (101) layout.
        private static readonly Dictionary<char, string> ToArabic = new Dictionary<char, string>
        {
            { 'q', "ض" }, { 'w', "ص" }, { 'e', "ث" }, { 'r', "ق" }, { 't', "ف" }, { 'y', "غ" }, { 'u', "ع" }, { 'i', "ه" }, { 'o', "خ" }, { 'p', "ح" }, { '[', "ج" }, { ']', "د" },
            { 'a', "ش" }, { 's', "س" }, { 'd', "ي" }, { 'f', "ب" }, { 'g', "ل" }, { 'h', "ا" }, { 'j', "ت" }, { 'k', "ن" }, { 'l', "م" }, { ';', "ك" }, { '\'', "ط" },
            { 'z', "ئ" }, { 'x', "ء" }, { 'c', "ؤ" }, { 'v', "ر" }, { 'b', "لا" }, { 'n', "ى" }, { 'm', "ة" }, { ',', "و" }, { '.', "ز" }, { '/', "ظ" }, { '`', "ذ" },
            { 'Q', "َ" }, { 'W', "ً" }, { 'E', "ُ" }, { 'R', "ٌ" }, { 'T', "لإ" }, { 'Y', "إ" }, { 'U', "‘" }, { 'I', "÷" }, { 'O', "×" }, { 'P', "؛" },
            { 'A', "ِ" }, { 'S', "ٍ" }, { 'D', "]" }, { 'F', "[" }, { 'G', "لأ" }, { 'H', "أ" }, { 'J', "ـ" }, { 'K', "،" }, { 'L', "/" },
            { 'Z', "~" }, { 'X', "ْ" }, { 'C', "}" }, { 'V', "{" }, { 'B', "لآ" }, { 'N', "آ" }, { 'M', "’" }, { '<', "," }, { '>', "." }, { '?', "؟" }, { '~', "ّ" }
        };

        // Arabic character -> English key that produces it.
        private static readonly Dictionary<char, string> ToEnglish = new Dictionary<char, string>
        {
            { 'ض', "q" }, { 'ص', "w" }, { 'ث', "e" }, { 'ق', "r" }, { 'ف', "t" }, { 'غ', "y" }, { 'ع', "u" }, { 'ه', "i" }, { 'خ', "o" }, { 'ح', "p" }, { 'ج', "[" }, { 'د', "]" },
            { 'ش', "a" }, { 'س', "s" }, { 'ي', "d" }, { 'ب', "f" }, { 'ل', "g" }, { 'ا', "h" }, { 'ت', "j" }, { 'ن', "k" }, { 'م', "l" }, { 'ك', ";" }, { 'ط', "'" },
            { 'ئ', "z" }, { 'ء', "x" }, { 'ؤ', "c" }, { 'ر', "v" }, { 'ى', "n" }, { 'ة', "m" }, { 'و', "," }, { 'ز', "." }, { 'ظ', "/" }, { 'ذ', "`" },
            { 'أ', "H" }, { 'إ', "Y" }, { 'آ', "N" }, { '؟', "?" }, { '،', "K" }, { '؛', "P" },
            { 'َ', "Q" }, { 'ً', "W" }, { 'ُ', "E" }, { 'ٌ', "R" }, { 'ِ', "A" }, { 'ٍ', "S" }, { 'ْ', "X" }, { 'ـ', "J" },
            { '‘', "U" }, { '÷', "I" }, { '×', "O" }, { '’', "M" }, { 'ّ', "~" }
        };

        public static bool IsLatinLetter(char c) { return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'); }
        public static bool IsArabicLetter(char c) { return (c >= '\u0621' && c <= '\u064A') || (c >= '\u0671' && c <= '\u06D3'); }

        /// <summary>Script of the letters in the text (majority wins, a tie counts as Latin).</summary>
        public static Script DetectScript(string text)
        {
            if (string.IsNullOrEmpty(text)) return Script.None;
            int latin = 0, arabic = 0;
            foreach (char c in text)
            {
                if (IsLatinLetter(c)) latin++;
                else if (IsArabicLetter(c)) arabic++;
            }
            if (latin == 0 && arabic == 0) return Script.None;
            return arabic > latin ? Script.Arabic : Script.Latin;
        }

        public static bool FirstStrongIsArabic(string text)
        {
            if (text == null) return false;
            foreach (char c in text)
            {
                if (IsArabicLetter(c)) return true;
                if (IsLatinLetter(c)) return false;
            }
            return false;
        }

        /// <summary>Converts a selection. Only words written in the wrong (majority) script are changed;
        /// words that are already correct in the other script are left untouched.</summary>
        public static string ConvertSelection(string text)
        {
            Script source = DetectScript(text);
            if (source == Script.None) return text;
            StringBuilder sb = new StringBuilder(text.Length + 8);
            foreach (string token in Tokenize(text))
            {
                sb.Append(ShouldConvert(token, source) ? ConvertToken(token, source) : token);
            }
            return sb.ToString();
        }

        /// <summary>Used when nothing is selected: converts only the trailing run of words written in the
        /// script of the last word, and keeps everything before it (e.g. correct Arabic) unchanged.</summary>
        public static bool ConvertTrailing(string line, out string prefix, out string converted)
        {
            prefix = line ?? "";
            converted = "";
            List<string> tokens = Tokenize(line);

            Script source = Script.None;
            for (int i = tokens.Count - 1; i >= 0; i--)
            {
                Script s = DetectScript(tokens[i]);
                if (s != Script.None) { source = s; break; }
            }
            if (source == Script.None) return false;

            int start = tokens.Count;
            for (int i = tokens.Count - 1; i >= 0; i--)
            {
                string token = tokens[i];
                if (string.IsNullOrWhiteSpace(token)) continue;
                Script s = DetectScript(token);
                if (s == source || s == Script.None) { start = i; continue; }
                break; // reached a word already written in the other script
            }
            if (start >= tokens.Count) return false;

            StringBuilder p = new StringBuilder();
            StringBuilder original = new StringBuilder();
            StringBuilder c = new StringBuilder();
            for (int i = 0; i < tokens.Count; i++)
            {
                if (i < start) { p.Append(tokens[i]); continue; }
                original.Append(tokens[i]);
                c.Append(ShouldConvert(tokens[i], source) ? ConvertToken(tokens[i], source) : tokens[i]);
            }
            prefix = p.ToString();
            converted = c.ToString();
            return converted != original.ToString();
        }

        private static List<string> Tokenize(string text)
        {
            List<string> tokens = new List<string>();
            if (string.IsNullOrEmpty(text)) return tokens;
            StringBuilder current = new StringBuilder();
            bool currentIsSpace = char.IsWhiteSpace(text[0]);
            foreach (char ch in text)
            {
                bool isSpace = char.IsWhiteSpace(ch);
                if (isSpace != currentIsSpace && current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Length = 0;
                }
                currentIsSpace = isSpace;
                current.Append(ch);
            }
            if (current.Length > 0) tokens.Add(current.ToString());
            return tokens;
        }

        private static bool ShouldConvert(string token, Script source)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;
            Script s = DetectScript(token);
            if (s == source) return true;
            if (s != Script.None) return false;
            // No letters at all (e.g. "," which is "و" on the Arabic layout): convert only if every char maps.
            Dictionary<char, string> map = source == Script.Latin ? ToArabic : ToEnglish;
            foreach (char ch in token) if (!map.ContainsKey(ch)) return false;
            return true;
        }

        private static string ConvertToken(string token, Script source)
        {
            StringBuilder sb = new StringBuilder(token.Length + 4);
            string replacement;
            if (source == Script.Latin)
            {
                // "GHL;" typed with Caps Lock on: Caps Lock does not change Arabic letters, so undo it.
                if (LooksLikeCapsLock(token)) token = token.ToLowerInvariant();
                foreach (char ch in token) sb.Append(ToArabic.TryGetValue(ch, out replacement) ? replacement : ch.ToString());
            }
            else
            {
                string normalized = token.Replace("لآ", "B").Replace("لأ", "G").Replace("لإ", "T").Replace("لا", "b");
                foreach (char ch in normalized) sb.Append(ToEnglish.TryGetValue(ch, out replacement) ? replacement : ch.ToString());
            }
            return sb.ToString();
        }

        private static bool LooksLikeCapsLock(string token)
        {
            int letters = 0;
            foreach (char ch in token)
            {
                if (!IsLatinLetter(ch)) continue;
                if (char.IsLower(ch)) return false;
                letters++;
            }
            return letters >= 2;
        }
    }

    // ------------------------------------------------------------------ keyboard input (SendInput)

    public static class Input
    {
        public const ushort VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LMENU = 0xA4, VK_RMENU = 0xA5;
        public const ushort VK_LWIN = 0x5B, VK_RWIN = 0x5C, VK_HOME = 0x24, VK_END = 0x23, VK_LEFT = 0x25, VK_UP = 0x26, VK_RIGHT = 0x27, VK_DOWN = 0x28;
        public const ushort VK_PRIOR = 0x21, VK_NEXT = 0x22, VK_INSERT = 0x2D, VK_DELETE = 0x2E, VK_C = 0x43, VK_V = 0x56, VK_Q = 0x51;
        // Unassigned virtual key. Tapping it while Alt is held stops apps from opening their menu bar on Alt release.
        public const ushort VK_MASK = 0xE8;

        // Marks keys injected by this app so our own keyboard hook can ignore them.
        public static readonly IntPtr Signature = new IntPtr(0x41524B46);

        private const uint INPUT_KEYBOARD = 1, KEYEVENTF_EXTENDEDKEY = 0x0001, KEYEVENTF_KEYUP = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT { public uint type; public InputUnion U; }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint count, INPUT[] inputs, int size);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint code, uint mapType);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        public static bool IsDown(int vk) { return (GetAsyncKeyState(vk) & 0x8000) != 0; }

        private static INPUT Make(ushort vk, bool up)
        {
            INPUT input = new INPUT();
            input.type = INPUT_KEYBOARD;
            input.U.ki.wVk = vk;
            input.U.ki.wScan = (ushort)MapVirtualKey(vk, 0);
            uint flags = up ? KEYEVENTF_KEYUP : 0;
            if (vk == VK_HOME || vk == VK_END || vk == VK_LEFT || vk == VK_RIGHT || vk == VK_UP || vk == VK_DOWN ||
                vk == VK_PRIOR || vk == VK_NEXT || vk == VK_INSERT || vk == VK_DELETE)
            {
                flags |= KEYEVENTF_EXTENDEDKEY;
            }
            input.U.ki.dwFlags = flags;
            input.U.ki.dwExtraInfo = Signature;
            return input;
        }

        private static void Send(params INPUT[] inputs)
        {
            uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            if (sent != inputs.Length)
            {
                Logger.Info("SendInput sent " + sent + "/" + inputs.Length + " (error " + Marshal.GetLastWin32Error() + ")");
            }
        }

        public static void Combo(ushort modifier, ushort key) { Send(Make(modifier, false), Make(key, false), Make(key, true), Make(modifier, true)); }
        public static void Tap(ushort key) { Send(Make(key, false), Make(key, true)); }
        public static void KeyUp(ushort key) { Send(Make(key, true)); }
    }

    // ------------------------------------------------------------------ clipboard (must be used from an STA thread)

    public static class ClipboardOps
    {
        [DllImport("user32.dll")]
        private static extern uint GetClipboardSequenceNumber();

        public static uint Sequence { get { return GetClipboardSequenceNumber(); } }

        /// <summary>Waits until another app changes the clipboard, then until it stops changing.</summary>
        public static bool WaitForChange(uint oldSequence, int timeoutMs)
        {
            Stopwatch sw = Stopwatch.StartNew();
            while (GetClipboardSequenceNumber() == oldSequence)
            {
                if (sw.ElapsedMilliseconds > timeoutMs) return false;
                Thread.Sleep(10);
            }
            uint last = GetClipboardSequenceNumber();
            Stopwatch stable = Stopwatch.StartNew();
            while (stable.ElapsedMilliseconds < 30 && sw.ElapsedMilliseconds < timeoutMs + 200)
            {
                Thread.Sleep(10);
                uint now = GetClipboardSequenceNumber();
                if (now != last) { last = now; stable.Restart(); }
            }
            return true;
        }

        /// <summary>Copies every format currently on the clipboard (text, images, files, rich text...).</summary>
        public static DataObject Backup()
        {
            DataObject copy = new DataObject();
            IDataObject source = null;
            for (int i = 0; i < 5 && source == null; i++)
            {
                try { source = Clipboard.GetDataObject(); }
                catch (ExternalException) { Thread.Sleep(30); }
            }
            if (source == null) return copy;

            string[] formats;
            try { formats = source.GetFormats(false); }
            catch (Exception ex) { Logger.Error("Clipboard backup: cannot list formats", ex); return copy; }

            foreach (string format in formats)
            {
                try
                {
                    object data = source.GetData(format, false);
                    if (data != null) copy.SetData(format, false, data);
                }
                catch (Exception ex) { Logger.Info("Clipboard backup skipped format '" + format + "': " + ex.Message); }
            }
            return copy;
        }

        public static void Restore(DataObject backup)
        {
            if (backup == null) return;
            if (backup.GetFormats(false).Length == 0)
            {
                try { Clipboard.Clear(); } catch (Exception ex) { Logger.Error("Clipboard clear failed", ex); }
                return;
            }
            if (TrySet(backup)) return;

            // Some exotic formats cannot be re-rendered; fall back to the common ones.
            DataObject basic = new DataObject();
            string[] common = { DataFormats.UnicodeText, DataFormats.Text, DataFormats.Rtf, DataFormats.Html, DataFormats.FileDrop, DataFormats.Bitmap, DataFormats.Dib };
            foreach (string format in common)
            {
                try { if (backup.GetDataPresent(format, false)) basic.SetData(format, false, backup.GetData(format, false)); }
                catch (Exception ex) { Logger.Info("Clipboard restore skipped '" + format + "': " + ex.Message); }
            }
            if (!TrySet(basic)) Logger.Info("Clipboard restore failed");
        }

        /// <summary>Puts text on the clipboard and asks Windows clipboard history / cloud sync to ignore it.</summary>
        public static bool SetTemporaryText(string text)
        {
            DataObject obj = new DataObject();
            obj.SetData(DataFormats.UnicodeText, true, text);
            obj.SetData("ExcludeClipboardContentFromMonitorProcessing", false, new MemoryStream(new byte[4]));
            obj.SetData("CanIncludeInClipboardHistory", false, new MemoryStream(BitConverter.GetBytes(0)));
            obj.SetData("CanUploadToCloudClipboard", false, new MemoryStream(BitConverter.GetBytes(0)));
            return TrySet(obj);
        }

        private static bool TrySet(DataObject obj)
        {
            try
            {
                Clipboard.SetDataObject(obj, true, 10, 50);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("SetDataObject failed", ex);
                return false;
            }
        }

        public static string GetText()
        {
            for (int i = 0; i < 6; i++)
            {
                try
                {
                    if (Clipboard.ContainsText(TextDataFormat.UnicodeText)) return Clipboard.GetText(TextDataFormat.UnicodeText);
                    if (Clipboard.ContainsText()) return Clipboard.GetText();
                    return null;
                }
                catch (ExternalException) { Thread.Sleep(25); }
            }
            return null;
        }

        /// <summary>VS Code, Visual Studio and Notepad++ copy the whole current line when nothing is selected.</summary>
        public static bool IsLineCopyWithoutSelection(string text, string processName)
        {
            try
            {
                IDataObject data = Clipboard.GetDataObject();
                if (data != null)
                {
                    foreach (string format in data.GetFormats(false))
                    {
                        if (format == "MSDEVLineSelect" || format == "VisualStudioEditorOperationsLineCutCopyClipboardTag") return true;
                        if (format.IndexOf("vscode-editor-data", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string json = ReadAsString(data.GetData(format, false));
                            if (json != null && json.Replace(" ", "").Contains("\"isFromEmptySelection\":true")) return true;
                        }
                    }
                }
            }
            catch (Exception ex) { Logger.Error("Line-copy detection failed", ex); }

            if (text != null && text.EndsWith("\n") && text.IndexOf('\n') == text.Length - 1)
            {
                string p = (processName ?? "").ToLowerInvariant();
                if (p == "code" || p == "code - insiders" || p == "cursor" || p == "windsurf" || p == "notepad++" || p == "devenv") return true;
            }
            return false;
        }

        private static string ReadAsString(object value)
        {
            string s = value as string;
            if (s != null) return s;
            MemoryStream ms = value as MemoryStream;
            if (ms != null) return Encoding.UTF8.GetString(ms.ToArray()).TrimEnd('\0');
            return null;
        }
    }

    // ------------------------------------------------------------------ foreground window information

    public static class WindowInfo
    {
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, StringBuilder name, int max);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);
        [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
        [DllImport("advapi32.dll", SetLastError = true)] private static extern bool GetTokenInformation(IntPtr token, int infoClass, out int info, int length, out int returnLength);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000, TOKEN_QUERY = 0x0008;
        private const int TokenElevation = 20, ERROR_ACCESS_DENIED = 5;

        private static readonly HashSet<string> TerminalClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ConsoleWindowClass", "CASCADIA_HOSTING_WINDOW_CLASS", "mintty", "PuTTY", "VirtualConsoleClass", "org.wezfurlong.wezterm"
        };

        private static readonly HashSet<string> TerminalProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "WindowsTerminal", "wt", "cmd", "powershell", "pwsh", "conhost", "OpenConsole", "mintty", "putty", "kitty",
            "alacritty", "wezterm-gui", "ConEmu", "ConEmu64", "Hyper", "Tabby", "MobaXterm"
        };

        public static IntPtr Foreground() { return GetForegroundWindow(); }

        public static uint GetThreadAndProcess(IntPtr hWnd, out uint processId) { return GetWindowThreadProcessId(hWnd, out processId); }

        public static string ClassName(IntPtr hWnd)
        {
            StringBuilder sb = new StringBuilder(256);
            GetClassName(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public static string ProcessName(uint processId)
        {
            try { using (Process p = Process.GetProcessById((int)processId)) return p.ProcessName; }
            catch { return ""; }
        }

        public static bool IsTerminal(string className, string processName)
        {
            return TerminalClasses.Contains(className) || TerminalProcesses.Contains(processName);
        }

        public static bool IsBrowserLike(string className)
        {
            return className.StartsWith("Chrome_WidgetWin", StringComparison.Ordinal) || className == "MozillaWindowClass";
        }

        /// <summary>True if the process runs as administrator (Windows blocks sending keys to it from a normal app).</summary>
        public static bool IsProcessElevated(uint processId)
        {
            IntPtr process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
            if (process == IntPtr.Zero) return false; // unknown: do not block the user
            try
            {
                IntPtr token;
                if (!OpenProcessToken(process, TOKEN_QUERY, out token)) return Marshal.GetLastWin32Error() == ERROR_ACCESS_DENIED;
                try
                {
                    int elevation, length;
                    return GetTokenInformation(token, TokenElevation, out elevation, 4, out length) && elevation != 0;
                }
                finally { CloseHandle(token); }
            }
            finally { CloseHandle(process); }
        }
    }

    // ------------------------------------------------------------------ switching between the user's installed layouts

    public static class KeyboardLayouts
    {
        [DllImport("user32.dll")] private static extern int GetKeyboardLayoutList(int count, IntPtr[] list);
        [DllImport("user32.dll")] private static extern IntPtr GetKeyboardLayout(uint threadId);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;
        private const int LANG_ARABIC = 0x01, LANG_ENGLISH = 0x09;

        private static int PrimaryLanguage(IntPtr hkl) { return (int)(hkl.ToInt64() & 0x3FF); }

        /// <summary>Switches only between layouts the user already has installed; never adds new ones.</summary>
        public static void SwitchTo(IntPtr hWnd, uint threadId, Script script)
        {
            int wanted = script == Script.Arabic ? LANG_ARABIC : LANG_ENGLISH;
            if (PrimaryLanguage(GetKeyboardLayout(threadId)) == wanted) return;

            int count = GetKeyboardLayoutList(0, null);
            if (count <= 0) return;
            IntPtr[] layouts = new IntPtr[count];
            count = GetKeyboardLayoutList(count, layouts);
            for (int i = 0; i < count; i++)
            {
                if (PrimaryLanguage(layouts[i]) == wanted)
                {
                    PostMessage(hWnd, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, layouts[i]);
                    return;
                }
            }
            Logger.Info("No installed keyboard layout for " + script + "; not switching");
        }
    }

    // ------------------------------------------------------------------ Alt+Q hotkey

    public sealed class HotkeyListener : NativeWindow, IDisposable
    {
        private const int WM_HOTKEY = 0x0312, HOTKEY_ID = 8821, WH_KEYBOARD_LL = 13, WM_KEYDOWN = 0x0100, WM_SYSKEYDOWN = 0x0104;
        private const uint MOD_ALT = 0x0001, MOD_NOREPEAT = 0x4000;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc proc, IntPtr hMod, uint threadId);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)] private static extern IntPtr GetModuleHandle(string moduleName);

        public event Action HotkeyPressed;
        public bool Registered { get; private set; }
        public bool UsingFallbackHook { get; private set; }

        private IntPtr _hook = IntPtr.Zero;
        private LowLevelKeyboardProc _proc; // keep a reference so the GC does not collect the delegate

        public HotkeyListener()
        {
            CreateHandle(new CreateParams());
            Registered = RegisterHotKey(Handle, HOTKEY_ID, MOD_ALT | MOD_NOREPEAT, Input.VK_Q);
            if (Registered) return;

            Logger.Info("RegisterHotKey(Alt+Q) failed with error " + Marshal.GetLastWin32Error() + "; another app owns it. Using keyboard hook.");
            _proc = HookCallback;
            using (Process p = Process.GetCurrentProcess())
            using (ProcessModule m = p.MainModule)
            {
                _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(m.ModuleName), 0);
            }
            UsingFallbackHook = _hook != IntPtr.Zero;
            if (!UsingFallbackHook) Logger.Info("SetWindowsHookEx failed with error " + Marshal.GetLastWin32Error());
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID) Raise();
            base.WndProc(ref m);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                int vk = Marshal.ReadInt32(lParam);
                IntPtr extra = Marshal.ReadIntPtr(lParam, 16); // KBDLLHOOKSTRUCT.dwExtraInfo
                if ((msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN) && vk == Input.VK_Q && extra != Input.Signature &&
                    Input.IsDown(Input.VK_MENU) && !Input.IsDown(Input.VK_CONTROL) && !Input.IsDown(Input.VK_LWIN) && !Input.IsDown(Input.VK_RWIN))
                {
                    Raise();
                    return (IntPtr)1;
                }
            }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private void Raise()
        {
            Action handler = HotkeyPressed;
            if (handler != null) handler();
        }

        public void Dispose()
        {
            if (Registered) UnregisterHotKey(Handle, HOTKEY_ID);
            if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
            DestroyHandle();
        }
    }

    // ------------------------------------------------------------------ tray application

    public class TrayApp : ApplicationContext
    {
        private readonly NotifyIcon _tray;
        private readonly HotkeyListener _hotkey;
        private readonly AppSettings _settings;
        private readonly Icon _icon;
        private readonly SynchronizationContext _ui;
        private readonly bool _selfElevated;
        private int _busy;
        private bool _terminalWarned;

        public TrayApp(bool silentStart)
        {
            _settings = AppSettings.Load();
            _icon = LoadIcon();

            ContextMenuStrip menu = CreateMenu();
            _ui = SynchronizationContext.Current;
            if (_ui == null) _ui = new WindowsFormsSynchronizationContext();

            _tray = new NotifyIcon();
            _tray.Icon = _icon;
            _tray.Text = AppInfo.Name;
            _tray.ContextMenuStrip = menu;
            _tray.Visible = true;
            _tray.DoubleClick += delegate { ShowAbout(); };

            try { _selfElevated = WindowInfo.IsProcessElevated((uint)Process.GetCurrentProcess().Id); }
            catch (Exception ex) { Logger.Error("Elevation check failed", ex); }

            _hotkey = new HotkeyListener();
            _hotkey.HotkeyPressed += OnHotkey;

            if (!_hotkey.Registered && !_hotkey.UsingFallbackHook)
            {
                Notify("تعذر تفعيل الاختصار", "مقدرتش أسجل الاختصار Alt + Q. راجع ملف السجل من قائمة البرنامج.", ToolTipIcon.Error);
            }
            else if (!silentStart)
            {
                Notify("مُحوّل الكيبورد شغال", "اضغط Alt + Q في أي مكان كتابة لتحويل النص بين العربي والإنجليزي.", ToolTipIcon.Info);
            }
        }

        private static Icon LoadIcon()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.ico");
                if (File.Exists(path)) return new Icon(path, SystemInformation.SmallIconSize);
                Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (icon != null) return icon;
            }
            catch (Exception ex) { Logger.Error("Icon load failed", ex); }
            return SystemIcons.Application;
        }

        private ContextMenuStrip CreateMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.RightToLeft = RightToLeft.Yes;

            ToolStripMenuItem title = new ToolStripMenuItem("مُحوّل الكيبورد v" + AppInfo.Version + "  —  Alt + Q");
            title.Enabled = false;
            menu.Items.Add(title);
            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add(MakeToggle("تبديل لغة الكيبورد تلقائياً بعد التحويل", _settings.AutoSwitchLanguage,
                delegate(bool v) { _settings.AutoSwitchLanguage = v; _settings.Save(); }));
            menu.Items.Add(MakeToggle("تحويل آخر كلام مكتوب لو مفيش نص متحدد", _settings.AutoSelectLine,
                delegate(bool v) { _settings.AutoSelectLine = v; _settings.Save(); }));
            menu.Items.Add(MakeToggle("صوت خفيف عند التحويل", _settings.PlaySound,
                delegate(bool v) { _settings.PlaySound = v; _settings.Save(); }));

            ToolStripMenuItem startup = new ToolStripMenuItem("التشغيل مع بدء ويندوز");
            startup.CheckOnClick = true;
            startup.Checked = StartupManager.IsEnabled();
            startup.Click += delegate
            {
                if (!StartupManager.SetEnabled(startup.Checked)) startup.Checked = StartupManager.IsEnabled();
            };
            menu.Items.Add(startup);
            menu.Opening += delegate { startup.Checked = StartupManager.IsEnabled(); };

            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem log = new ToolStripMenuItem("فتح مجلد السجل");
            log.Click += delegate
            {
                try { Process.Start("explorer.exe", "\"" + AppInfo.DataDir + "\""); }
                catch (Exception ex) { Logger.Error("Open log folder failed", ex); }
            };
            menu.Items.Add(log);

            ToolStripMenuItem about = new ToolStripMenuItem("عن البرنامج...");
            about.Click += delegate { ShowAbout(); };
            menu.Items.Add(about);

            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem exit = new ToolStripMenuItem("خروج");
            exit.Click += delegate { ExitThread(); };
            menu.Items.Add(exit);
            return menu;
        }

        private static ToolStripMenuItem MakeToggle(string text, bool initial, Action<bool> onChange)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.CheckOnClick = true;
            item.Checked = initial;
            item.CheckedChanged += delegate { onChange(item.Checked); };
            return item;
        }

        private void ShowAbout()
        {
            MessageBox.Show(
                "مُحوّل الكيبورد - الإصدار " + AppInfo.Version + "\n\n" +
                "الاختصار: Alt + Q\n\n" +
                "• لو محدد نص: يتحول الكلام المكتوب باللغة الغلط بس، والكلام الصح يفضل زي ما هو.\n" +
                "• لو مش محدد حاجة: يتحول آخر كلام كتبته باللغة الغلط في السطر الحالي.\n" +
                "• محتوى الحافظة (Clipboard) بيرجع زي ما كان بعد التحويل.\n\n" +
                "ملاحظات:\n" +
                "• لا يعمل داخل نوافذ الأوامر (CMD / PowerShell / Terminal).\n" +
                "• لا يعمل داخل البرامج اللي شغالة كمسؤول (Administrator) إلا لو شغلته هو كمان كمسؤول.\n" +
                "• التحويل مبني على لوحة المفاتيح العربية القياسية (Arabic 101).",
                "عن " + AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
        }

        private void Notify(string title, string text, ToolTipIcon icon)
        {
            _ui.Post(delegate(object state)
            {
                try { _tray.ShowBalloonTip(4000, title, text, icon); }
                catch (Exception ex) { Logger.Error("Balloon failed", ex); }
            }, null);
        }

        private void OnHotkey()
        {
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return;
            Thread worker = new Thread(DoConvert);
            worker.SetApartmentState(ApartmentState.STA); // required by the WinForms clipboard API
            worker.IsBackground = true;
            worker.Start();
        }

        private void DoConvert()
        {
            try
            {
                IntPtr window = WindowInfo.Foreground();
                if (window == IntPtr.Zero) return;
                uint processId;
                uint threadId = WindowInfo.GetThreadAndProcess(window, out processId);
                if (processId == (uint)Process.GetCurrentProcess().Id) return;

                // Alt is still physically down: stop the target app from opening its menu bar when it is released.
                Input.Tap(Input.VK_MASK);

                string className = WindowInfo.ClassName(window);
                string processName = WindowInfo.ProcessName(processId);

                if (WindowInfo.IsTerminal(className, processName))
                {
                    // Ctrl+C would interrupt (kill) the program running in the terminal.
                    Logger.Info("Skipped terminal window (" + processName + ")");
                    if (!_terminalWarned)
                    {
                        _terminalWarned = true;
                        Notify("غير مدعوم هنا", "البرنامج مش بيشتغل جوه نوافذ الأوامر (CMD / PowerShell / Terminal) لأن Ctrl+C هناك بيوقف البرامج الشغالة.", ToolTipIcon.Info);
                    }
                    return;
                }

                if (!_selfElevated && WindowInfo.IsProcessElevated(processId))
                {
                    Logger.Info("Skipped elevated window (" + processName + ")");
                    Notify("البرنامج ده شغال كمسؤول", "ويندوز بيمنع التحكم في البرامج اللي شغالة كمسؤول (Administrator). شغّل مُحوّل الكيبورد كمسؤول لو محتاجه هنا.", ToolTipIcon.Warning);
                    return;
                }

                WaitForKeysReleased();
                Convert(window, threadId, className, processName);
            }
            catch (Exception ex)
            {
                Logger.Error("Conversion failed", ex);
            }
            finally
            {
                Interlocked.Exchange(ref _busy, 0);
            }
        }

        private void Convert(IntPtr window, uint threadId, string className, string processName)
        {
            DataObject backup = ClipboardOps.Backup();
            bool restoreClipboard = false;
            try
            {
                string newText;
                Script target;

                // 1) Is something selected?
                uint sequence = ClipboardOps.Sequence;
                Input.Combo(Input.VK_CONTROL, Input.VK_C);
                string selected = null;
                if (ClipboardOps.WaitForChange(sequence, 350))
                {
                    restoreClipboard = true;
                    selected = ClipboardOps.GetText();
                    if (!string.IsNullOrEmpty(selected) && ClipboardOps.IsLineCopyWithoutSelection(selected, processName))
                    {
                        Logger.Info("Editor copied the whole line without a selection");
                        selected = null;
                    }
                }

                if (!string.IsNullOrEmpty(selected))
                {
                    newText = KeyboardConverter.ConvertSelection(selected);
                    if (newText == selected) { Logger.Info("Selection: nothing to convert"); return; }
                    target = KeyboardConverter.DetectScript(newText);
                }
                else
                {
                    if (!_settings.AutoSelectLine) return;

                    // 2) Nothing selected: take the text between the line start and the caret.
                    Input.Combo(Input.VK_SHIFT, Input.VK_HOME);
                    Thread.Sleep(40);
                    sequence = ClipboardOps.Sequence;
                    Input.Combo(Input.VK_CONTROL, Input.VK_C);
                    string line = null;
                    if (ClipboardOps.WaitForChange(sequence, 600))
                    {
                        restoreClipboard = true;
                        line = ClipboardOps.GetText();
                    }

                    string prefix, converted;
                    if (string.IsNullOrEmpty(line) || !KeyboardConverter.ConvertTrailing(line, out prefix, out converted))
                    {
                        Logger.Info("Line: nothing to convert");
                        CollapseSelection(className, line);
                        return;
                    }
                    newText = prefix + converted;
                    target = KeyboardConverter.DetectScript(converted);
                }

                // 3) Paste the converted text over the selection.
                if (!ClipboardOps.SetTemporaryText(newText)) return;
                restoreClipboard = true;
                uint ours = ClipboardOps.Sequence;
                Input.Combo(Input.VK_CONTROL, Input.VK_V);
                Thread.Sleep(450); // give the app time to read the clipboard before we restore it

                if (ClipboardOps.Sequence != ours)
                {
                    restoreClipboard = false; // the user or another app copied something meanwhile
                    Logger.Info("Clipboard changed by another app; not restoring");
                }

                Logger.Info("Converted " + newText.Length + " chars (" + processName + ") -> " + target);
                if (_settings.AutoSwitchLanguage && target != Script.None) KeyboardLayouts.SwitchTo(window, threadId, target);
                if (_settings.PlaySound) SystemSounds.Asterisk.Play();
            }
            finally
            {
                if (restoreClipboard) ClipboardOps.Restore(backup);
            }
        }

        /// <summary>Removes the Shift+Home selection and puts the caret back at the end of it.</summary>
        private static void CollapseSelection(string className, string selectedText)
        {
            // In right-to-left web editors (Facebook, WhatsApp Web...) the arrow keys are mirrored.
            bool rtlWeb = WindowInfo.IsBrowserLike(className) && KeyboardConverter.FirstStrongIsArabic(selectedText);
            Input.Tap(rtlWeb ? Input.VK_LEFT : Input.VK_RIGHT);
        }

        private static void WaitForKeysReleased()
        {
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 1500)
            {
                if (!Input.IsDown(Input.VK_MENU) && !Input.IsDown(Input.VK_SHIFT) && !Input.IsDown(Input.VK_CONTROL) &&
                    !Input.IsDown(Input.VK_LWIN) && !Input.IsDown(Input.VK_RWIN) && !Input.IsDown(Input.VK_Q))
                {
                    return;
                }
                Thread.Sleep(10);
            }
            // Still held: release Alt logically so our Ctrl+C is not seen as Ctrl+Alt+C.
            Logger.Info("Keys still held after 1.5s; releasing Alt");
            Input.Tap(Input.VK_MASK);
            Input.KeyUp(Input.VK_MENU);
            Input.KeyUp(Input.VK_LMENU);
            Input.KeyUp(Input.VK_RMENU);
        }

        protected override void ExitThreadCore()
        {
            _tray.Visible = false;
            base.ExitThreadCore();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_hotkey != null) _hotkey.Dispose();
                if (_tray != null) _tray.Dispose();
                if (_icon != null) _icon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
