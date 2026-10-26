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

namespace ArabicKeyboardFixer
{
    static class Program
    {
        private const string AppGuid = "ArabicKeyboardFixer_Global_SingleInstance_Mutex_2026";

        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, AppGuid, out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show(
                        "البرنامج يعمل بالفعل في شريط المهام بجوار الساعة (System Tray).\n\nاضغط Alt + Q في أي مكان لتحويل النص مباشرة.",
                        "مُحوّل الكيبورد (Alt + Q)",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new AppContext());
            }
        }
    }

    public class AppSettings
    {
        public bool AutoSwitchLanguage { get; set; }
        public bool AutoSelectLine { get; set; }
        public bool PlaySound { get; set; }
        public bool AutoStart { get; set; }

        private static string ConfigPath
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArabicKeyboardFixer");
                if (!Directory.Exists(dir))
                {
                    try { Directory.CreateDirectory(dir); } catch { }
                }
                return Path.Combine(dir, "settings.ini");
            }
        }

        public AppSettings()
        {
            AutoSwitchLanguage = true;
            AutoSelectLine = true;
            PlaySound = false;
            AutoStart = true;
        }

        public static AppSettings Load()
        {
            AppSettings settings = new AppSettings();
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string[] lines = File.ReadAllLines(ConfigPath);
                    foreach (string line in lines)
                    {
                        string[] parts = line.Split('=');
                        if (parts.Length == 2)
                        {
                            string key = parts[0].Trim();
                            string val = parts[1].Trim();
                            bool boolVal;
                            if (bool.TryParse(val, out boolVal))
                            {
                                if (key == "AutoSwitchLanguage") settings.AutoSwitchLanguage = boolVal;
                                else if (key == "AutoSelectLine") settings.AutoSelectLine = boolVal;
                                else if (key == "PlaySound") settings.PlaySound = boolVal;
                                else if (key == "AutoStart") settings.AutoStart = boolVal;
                            }
                        }
                    }
                }
            }
            catch { }
            return settings;
        }

        public void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("AutoSwitchLanguage=" + AutoSwitchLanguage);
                sb.AppendLine("AutoSelectLine=" + AutoSelectLine);
                sb.AppendLine("PlaySound=" + PlaySound);
                sb.AppendLine("AutoStart=" + AutoStart);
                File.WriteAllText(ConfigPath, sb.ToString());
            }
            catch { }
        }
    }

    public class AppContext : ApplicationContext
    {
        private readonly NotifyIcon _trayIcon;
        private readonly HotkeyListener _hotkeyListener;
        private readonly AppSettings _settings;
        private readonly Icon _appIcon;
        private int _isProcessing = 0;

        public AppContext()
        {
            _settings = AppSettings.Load();

            // Load application icon
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.ico");
                if (File.Exists(iconPath))
                {
                    _appIcon = new Icon(iconPath);
                }
                else
                {
                    _appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                }
            }
            catch
            {
                _appIcon = SystemIcons.Application;
            }

            // Create context menu
            ContextMenuStrip menu = CreateContextMenu();

            _trayIcon = new NotifyIcon
            {
                Icon = _appIcon,
                ContextMenuStrip = menu,
                Text = "مُحوّل الكيبورد (Alt + Q)",
                Visible = true
            };

            _trayIcon.DoubleClick += (s, e) => ShowAboutDialog();

            // Show startup balloon tip once
            _trayIcon.ShowBalloonTip(
                3000,
                "مُحوّل الكيبورد يعمل الآن",
                "اضغط (Alt + Q) في أي مكان لتحويل النص المكتوب بين العربي والإنجليزي.",
                ToolTipIcon.Info
            );

            // Ensure auto-start with Windows is active if setting is enabled
            if (_settings.AutoStart && !IsAutoStartEnabled())
            {
                SetAutoStart(true);
            }

            // Initialize Hotkey
            _hotkeyListener = new HotkeyListener();
            _hotkeyListener.HotkeyPressed += OnHotkeyPressed;
        }

        private ContextMenuStrip CreateContextMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.RightToLeft = RightToLeft.Yes;
            menu.Font = new Font("Segoe UI", 9.5f);

            ToolStripMenuItem convertItem = new ToolStripMenuItem("تحويل النص الآن (Alt + Q)");
            convertItem.Font = new Font(menu.Font, FontStyle.Bold);
            convertItem.Click += (s, e) => OnHotkeyPressed();
            menu.Items.Add(convertItem);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem autoSwitchItem = new ToolStripMenuItem("تبديل لغة الكيبورد تلقائياً");
            autoSwitchItem.CheckOnClick = true;
            autoSwitchItem.Checked = _settings.AutoSwitchLanguage;
            autoSwitchItem.CheckedChanged += (s, e) =>
            {
                _settings.AutoSwitchLanguage = autoSwitchItem.Checked;
                _settings.Save();
            };
            menu.Items.Add(autoSwitchItem);

            ToolStripMenuItem autoSelectItem = new ToolStripMenuItem("تحديد السطر تلقائياً عند عدم التحديد");
            autoSelectItem.CheckOnClick = true;
            autoSelectItem.Checked = _settings.AutoSelectLine;
            autoSelectItem.CheckedChanged += (s, e) =>
            {
                _settings.AutoSelectLine = autoSelectItem.Checked;
                _settings.Save();
            };
            menu.Items.Add(autoSelectItem);

            ToolStripMenuItem soundItem = new ToolStripMenuItem("تشغيل صوت خفيف عند التحويل");
            soundItem.CheckOnClick = true;
            soundItem.Checked = _settings.PlaySound;
            soundItem.CheckedChanged += (s, e) =>
            {
                _settings.PlaySound = soundItem.Checked;
                _settings.Save();
            };
            menu.Items.Add(soundItem);

            ToolStripMenuItem startupItem = new ToolStripMenuItem("التشغيل التلقائي مع بدء ويندوز");
            startupItem.CheckOnClick = true;
            startupItem.Checked = _settings.AutoStart && IsAutoStartEnabled();
            startupItem.CheckedChanged += (s, e) =>
            {
                _settings.AutoStart = startupItem.Checked;
                _settings.Save();
                SetAutoStart(startupItem.Checked);
            };
            menu.Items.Add(startupItem);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem aboutItem = new ToolStripMenuItem("عن البرنامج والمساعدة...");
            aboutItem.Click += (s, e) => ShowAboutDialog();
            menu.Items.Add(aboutItem);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem exitItem = new ToolStripMenuItem("خروج");
            exitItem.Click += (s, e) => Exit();
            menu.Items.Add(exitItem);

            return menu;
        }

        private bool IsAutoStartEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("ArabicKeyboardFixer");
                        return val != null;
                    }
                }
            }
            catch { }
            return false;
        }

        private void SetAutoStart(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            key.SetValue("ArabicKeyboardFixer", "\"" + Application.ExecutablePath + "\"");
                        }
                        else
                        {
                            key.DeleteValue("ArabicKeyboardFixer", false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("تعذر تعديل إعدادات بدء التشغيل: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ShowAboutDialog()
        {
            string msg =
                "مُحوّل الكيبورد (Arabic Keyboard Layout Fixer)\n" +
                "الإصدار 5.0 - نسخة الويندوز المتكاملة\n\n" +
                "الاختصار السريع: Alt + Q\n\n" +
                "كيف يعمل البرنامج؟\n" +
                "1. إذا قمت بتحديد أي نص وضغطت Alt + Q سيتم تحويل النص المحدد فوراً بين العربية والإنجليزية.\n" +
                "2. إذا كنت تكتب في أي برنامج وضغطت Alt + Q دون تحديد نص، سيقوم البرنامج تلقائياً بتحويل السطر الذي كتبته للتو!\n" +
                "3. يقوم البرنامج تلقائياً بتبديل لغة الكيبورد في ويندوز حتى تستمر في الكتابة بدون الحاجة للضغط على Alt+Shift.\n\n" +
                "يعمل في جميع البرامج: المتصفحات (كروم، إيدج، فايرفوكس)، مايكروسوفت وورد، أوفيس، نوت باد، محادثات واتساب وتليجرام وديسكورد، وغيرها.";

            MessageBox.Show(msg, "عن برنامج تحويل الكيبورد", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnHotkeyPressed()
        {
            // Avoid overlapping runs
            if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) != 0)
            {
                return;
            }

            Thread worker = new Thread(DoConvert);
            worker.SetApartmentState(ApartmentState.STA);
            worker.IsBackground = true;
            worker.Start();
        }

        private void DoConvert()
        {
            try
            {
                IntPtr fgWnd = NativeMethods.GetForegroundWindow();

                // 1. Release modifier keys logically with mask key so Chrome never loses focus
                NativeMethods.ReleaseModifiers();

                // 2. Backup current clipboard content
                string oldClipboard = SafeClipboard.GetText();

                // 3. Clear clipboard so we can reliably detect newly copied text
                SafeClipboard.Clear();

                // 4. Send Ctrl+C
                NativeMethods.SendKeyCombo(NativeMethods.VK_CONTROL, NativeMethods.VK_C);

                // 5. Check if user had highlighted text (give Chrome/Electron up to 250ms)
                string selectedText = SafeClipboard.WaitForText(250);
                bool hadSelection = !string.IsNullOrEmpty(selectedText);

                if (!hadSelection && _settings.AutoSelectLine)
                {
                    // Select backwards to the beginning of the line
                    NativeMethods.SendShiftHome();
                    // Wait 60ms for Chrome / React / Lexical (Facebook) / WhatsApp to update DOM selection
                    Thread.Sleep(60);

                    SafeClipboard.Clear();
                    NativeMethods.SendKeyCombo(NativeMethods.VK_CONTROL, NativeMethods.VK_C);
                    // Wait up to 300ms for Chrome IPC to write to clipboard
                    selectedText = SafeClipboard.WaitForText(300);
                }

                if (string.IsNullOrEmpty(selectedText))
                {
                    // Nothing to convert
                    if (oldClipboard != null) SafeClipboard.SetText(oldClipboard);
                    return;
                }

                // 6. Convert text
                string converted = KeyboardConverter.Convert(selectedText);

                if (converted == selectedText)
                {
                    // Nothing changed
                    if (!hadSelection && _settings.AutoSelectLine)
                    {
                        // Unselect by moving caret to the end
                        NativeMethods.SendRightArrow();
                    }
                    if (oldClipboard != null) SafeClipboard.SetText(oldClipboard);
                    return;
                }

                // 7. Put converted text on clipboard (with full OLE data object)
                SafeClipboard.SetText(converted);
                // Allow clipboard to propagate before pasting into Chrome
                Thread.Sleep(50);

                // Send Ctrl+V
                NativeMethods.SendKeyCombo(NativeMethods.VK_CONTROL, NativeMethods.VK_V);
                Thread.Sleep(60);

                // 8. Auto switch keyboard language if enabled
                if (_settings.AutoSwitchLanguage && fgWnd != IntPtr.Zero)
                {
                    bool isArabic = KeyboardConverter.IsArabicDominant(converted);
                    NativeMethods.SwitchKeyboardLayout(fgWnd, isArabic);
                }

                // 9. Play sound if enabled
                if (_settings.PlaySound)
                {
                    try { SystemSounds.Asterisk.Play(); } catch { }
                }
            }
            catch { }
            finally
            {
                Interlocked.Exchange(ref _isProcessing, 0);
            }
        }

        private void Exit()
        {
            _trayIcon.Visible = false;
            _hotkeyListener.Dispose();
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_trayIcon != null) _trayIcon.Dispose();
                if (_hotkeyListener != null) _hotkeyListener.Dispose();
                if (_appIcon != null) _appIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public static class KeyboardConverter
    {
        private static readonly Dictionary<char, string> AR = new Dictionary<char, string>
        {
            { 'q', "ض" }, { 'w', "ص" }, { 'e', "ث" }, { 'r', "ق" }, { 't', "ف" }, { 'y', "غ" }, { 'u', "ع" }, { 'i', "ه" }, { 'o', "خ" }, { 'p', "ح" }, { '[', "ج" }, { ']', "د" },
            { 'a', "ش" }, { 's', "س" }, { 'd', "ي" }, { 'f', "ب" }, { 'g', "ل" }, { 'h', "ا" }, { 'j', "ت" }, { 'k', "ن" }, { 'l', "م" }, { ';', "ك" }, { '\'', "ط" },
            { 'z', "ئ" }, { 'x', "ء" }, { 'c', "ؤ" }, { 'v', "ر" }, { 'b', "لا" }, { 'n', "ى" }, { 'm', "ة" }, { ',', "و" }, { '.', "ز" }, { '/', "ظ" }, { '`', "ذ" },
            { 'Q', "َ" }, { 'W', "ً" }, { 'E', "ُ" }, { 'R', "ٌ" }, { 'T', "لإ" }, { 'Y', "إ" }, { 'U', "‘" }, { 'I', "÷" }, { 'O', "×" }, { 'P', "؛" },
            { 'A', "ِ" }, { 'S', "ٍ" }, { 'D', "]" }, { 'F', "[" }, { 'G', "لأ" }, { 'H', "أ" }, { 'J', "ـ" }, { 'K', "،" }, { 'L', "/" },
            { 'Z', "~" }, { 'X', "ْ" }, { 'C', "}" }, { 'V', "{" }, { 'B', "لآ" }, { 'N', "آ" }, { 'M', "’" }, { '<', "," }, { '>', "." }, { '?', "؟" }, { '~', "ّ" }
        };

        private static readonly Dictionary<char, string> EN = new Dictionary<char, string>
        {
            { 'ض', "q" }, { 'ص', "w" }, { 'ث', "e" }, { 'ق', "r" }, { 'ف', "t" }, { 'غ', "y" }, { 'ع', "u" }, { 'ه', "i" }, { 'خ', "o" }, { 'ح', "p" }, { 'ج', "[" }, { 'د', "]" },
            { 'ش', "a" }, { 'س', "s" }, { 'ي', "d" }, { 'ب', "f" }, { 'ل', "g" }, { 'ا', "h" }, { 'ت', "j" }, { 'ن', "k" }, { 'م', "l" }, { 'ك', ";" }, { 'ط', "\'" },
            { 'ئ', "z" }, { 'ء', "x" }, { 'ؤ', "c" }, { 'ر', "v" }, { 'ى', "n" }, { 'ة', "m" }, { 'و', "," }, { 'ز', "." }, { 'ظ', "/" }, { 'ذ', "`" },
            { 'أ', "H" }, { 'إ', "Y" }, { 'آ', "N" }, { '؟', "?" }, { '،', "K" }, { '؛', "P" },
            { 'َ', "Q" }, { 'ً', "W" }, { 'ُ', "E" }, { 'ٌ', "R" }, { 'ِ', "A" }, { 'ٍ', "S" }, { 'ْ', "X" }, { 'ـ', "J" },
            { '‘', "U" }, { '÷', "I" }, { '×', "O" }, { '’', "M" }, { 'ّ', "~" }
        };

        public static bool IsArabicDominant(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            int arabic = 0;
            int english = 0;
            foreach (char ch in text)
            {
                if (ch >= 0x0600 && ch <= 0x06FF) arabic++;
                else if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z')) english++;
            }
            return arabic > english;
        }

        public static string Convert(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            if (IsArabicDominant(text))
            {
                // Arabic to English
                string normalized = text
                    .Replace("لآ", "B")
                    .Replace("لأ", "G")
                    .Replace("لإ", "T")
                    .Replace("لا", "b");

                StringBuilder sb = new StringBuilder(normalized.Length);
                foreach (char ch in normalized)
                {
                    string replacement;
                    if (EN.TryGetValue(ch, out replacement))
                    {
                        sb.Append(replacement);
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                }
                return sb.ToString();
            }
            else
            {
                // English to Arabic
                StringBuilder sb = new StringBuilder(text.Length);
                foreach (char ch in text)
                {
                    string replacement;
                    if (AR.TryGetValue(ch, out replacement))
                    {
                        sb.Append(replacement);
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                }
                return sb.ToString();
            }
        }
    }

    public class HotkeyListener : NativeWindow, IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_NOREPEAT = 0x4000;
        private const int HOTKEY_ID = 8821;

        public event Action HotkeyPressed;

        private IntPtr _hookId = IntPtr.Zero;
        private NativeMethods.LowLevelKeyboardProc _hookProc;

        public HotkeyListener()
        {
            CreateParams cp = new CreateParams();
            CreateHandle(cp);

            // Register global hotkey Alt + Q
            bool registered = NativeMethods.RegisterHotKey(this.Handle, HOTKEY_ID, MOD_ALT | MOD_NOREPEAT, (uint)Keys.Q);
            if (!registered)
            {
                // Try without MOD_NOREPEAT for compatibility
                registered = NativeMethods.RegisterHotKey(this.Handle, HOTKEY_ID, MOD_ALT, (uint)Keys.Q);
            }

            if (!registered)
            {
                // Fallback to low-level keyboard hook if another app registered Alt+Q
                _hookProc = HookCallback;
                using (Process curProcess = Process.GetCurrentProcess())
                using (ProcessModule curModule = curProcess.MainModule)
                {
                    _hookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _hookProc, NativeMethods.GetModuleHandle(curModule.ModuleName), 0);
                }
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                TriggerHotkey();
            }
            base.WndProc(ref m);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                if (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN)
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    if (vkCode == (int)Keys.Q)
                    {
                        bool altDown = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_MENU) & 0x8000) != 0;
                        bool ctrlDown = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0;
                        bool winDown = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LWIN) & 0x8000) != 0 ||
                                       (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RWIN) & 0x8000) != 0;

                        if (altDown && !ctrlDown && !winDown)
                        {
                            TriggerHotkey();
                            return (IntPtr)1; // Consume key
                        }
                    }
                }
            }
            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private void TriggerHotkey()
        {
            if (HotkeyPressed != null)
            {
                HotkeyPressed();
            }
        }

        public void Dispose()
        {
            NativeMethods.UnregisterHotKey(this.Handle, HOTKEY_ID);
            if (_hookId != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
            DestroyHandle();
        }
    }

    public static class SafeClipboard
    {
        public static string GetText()
        {
            for (int i = 0; i < 8; i++)
            {
                try
                {
                    if (Clipboard.ContainsText())
                    {
                        string t = Clipboard.GetText();
                        if (!string.IsNullOrEmpty(t)) return t;
                    }
                }
                catch
                {
                    Thread.Sleep(20);
                }
            }
            return Win32Clipboard.GetText();
        }

        public static bool SetText(string text)
        {
            if (text == null) return false;
            for (int i = 0; i < 8; i++)
            {
                try
                {
                    Clipboard.SetDataObject(text, true);
                    return true;
                }
                catch
                {
                    Thread.Sleep(20);
                }
            }
            return Win32Clipboard.SetText(text);
        }

        public static void Clear()
        {
            for (int i = 0; i < 6; i++)
            {
                try
                {
                    Clipboard.Clear();
                    return;
                }
                catch
                {
                    Thread.Sleep(15);
                }
            }
            Win32Clipboard.Clear();
        }

        public static string WaitForText(int timeoutMs)
        {
            int elapsed = 0;
            const int interval = 25;
            while (elapsed < timeoutMs)
            {
                Thread.Sleep(interval);
                elapsed += interval;
                string text = GetText();
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }
            return null;
        }
    }

    public static class Win32Clipboard
    {
        private const uint CF_UNICODETEXT = 13;
        private const uint GMEM_MOVEABLE = 0x0002;

        public static string GetText()
        {
            for (int i = 0; i < 4; i++)
            {
                if (NativeMethods.OpenClipboard(IntPtr.Zero))
                {
                    try
                    {
                        IntPtr handle = NativeMethods.GetClipboardData(CF_UNICODETEXT);
                        if (handle != IntPtr.Zero)
                        {
                            IntPtr ptr = NativeMethods.GlobalLock(handle);
                            if (ptr != IntPtr.Zero)
                            {
                                try
                                {
                                    return Marshal.PtrToStringUni(ptr);
                                }
                                finally
                                {
                                    NativeMethods.GlobalUnlock(handle);
                                }
                            }
                        }
                        return null;
                    }
                    finally
                    {
                        NativeMethods.CloseClipboard();
                    }
                }
                Thread.Sleep(15);
            }
            return null;
        }

        public static bool SetText(string text)
        {
            if (text == null) return false;
            for (int i = 0; i < 4; i++)
            {
                if (NativeMethods.OpenClipboard(IntPtr.Zero))
                {
                    try
                    {
                        NativeMethods.EmptyClipboard();
                        int byteCount = (text.Length + 1) * 2;
                        IntPtr hGlobal = NativeMethods.GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)byteCount);
                        if (hGlobal == IntPtr.Zero) return false;

                        IntPtr target = NativeMethods.GlobalLock(hGlobal);
                        if (target == IntPtr.Zero) return false;

                        try
                        {
                            Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
                            Marshal.WriteInt16(target, text.Length * 2, 0);
                        }
                        finally
                        {
                            NativeMethods.GlobalUnlock(hGlobal);
                        }

                        return NativeMethods.SetClipboardData(CF_UNICODETEXT, hGlobal) != IntPtr.Zero;
                    }
                    finally
                    {
                        NativeMethods.CloseClipboard();
                    }
                }
                Thread.Sleep(15);
            }
            return false;
        }

        public static void Clear()
        {
            for (int i = 0; i < 4; i++)
            {
                if (NativeMethods.OpenClipboard(IntPtr.Zero))
                {
                    try
                    {
                        NativeMethods.EmptyClipboard();
                        return;
                    }
                    finally
                    {
                        NativeMethods.CloseClipboard();
                    }
                }
                Thread.Sleep(15);
            }
        }
    }

    public static class NativeMethods
    {
        public const byte VK_SHIFT = 0x10;
        public const byte VK_CONTROL = 0x11;
        public const byte VK_MENU = 0x12; // Alt
        public const byte VK_LMENU = 0xA4;
        public const byte VK_RMENU = 0xA5;
        public const byte VK_LWIN = 0x5B;
        public const byte VK_RWIN = 0x5C;
        public const byte VK_HOME = 0x24;
        public const byte VK_RIGHT = 0x27;
        public const byte VK_C = 0x43;
        public const byte VK_V = 0x56;

        public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        public const uint KEYEVENTF_KEYUP = 0x0002;

        public const int WH_KEYBOARD_LL = 13;
        public const int WM_KEYDOWN = 0x0100;
        public const int WM_SYSKEYDOWN = 0x0104;

        public const uint KLF_ACTIVATE = 1;
        public const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;

        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("user32.dll")]
        public static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint Flags);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        public static void ReleaseModifiers()
        {
            // 1. Send Ctrl mask key to prevent Windows/Chrome from opening the 3-dots/Alt menu (SC_KEYMENU)
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            Thread.Sleep(5);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

            // 2. Release Alt keys logically
            keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_LMENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_RMENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

            // 3. Wait until physical Alt is released
            int count = 0;
            while ((GetAsyncKeyState(VK_MENU) & 0x8000) != 0 && count < 20)
            {
                Thread.Sleep(10);
                count++;
            }

            // 4. Send mask key again to ensure menu bar state is completely neutralized
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            Thread.Sleep(5);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

            keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_LMENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_RMENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

            Thread.Sleep(20);
        }

        public static void SendKeyCombo(byte modifier, byte key)
        {
            keybd_event(modifier, 0, 0, UIntPtr.Zero);
            Thread.Sleep(15);
            keybd_event(key, 0, 0, UIntPtr.Zero);
            Thread.Sleep(30);
            keybd_event(key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            Thread.Sleep(15);
            keybd_event(modifier, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            Thread.Sleep(15);
        }

        public static void SendShiftHome()
        {
            keybd_event(VK_SHIFT, 0, 0, UIntPtr.Zero);
            Thread.Sleep(15);
            keybd_event(VK_HOME, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
            Thread.Sleep(30);
            keybd_event(VK_HOME, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
            Thread.Sleep(15);
            keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            Thread.Sleep(15);
        }

        public static void SendRightArrow()
        {
            keybd_event(VK_RIGHT, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
            Thread.Sleep(10);
            keybd_event(VK_RIGHT, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public static void SwitchKeyboardLayout(IntPtr hWnd, bool toArabic)
        {
            string langCode = toArabic ? "00000401" : "00000409";
            IntPtr hkl = LoadKeyboardLayout(langCode, KLF_ACTIVATE);
            if (hkl != IntPtr.Zero && hWnd != IntPtr.Zero)
            {
                PostMessage(hWnd, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, hkl);
            }
        }
    }
}
