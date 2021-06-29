using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OriginSteamOverlayLauncher
{
    public class WindowUtils
    {
        #region Imports
        // borrowed from: https://code.msdn.microsoft.com/windowsapps/C-Getting-the-Windows-da1bd524
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern long GetWindowText(IntPtr hwnd, StringBuilder lpString, long cch);
        [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern long GetClassName(IntPtr hwnd, StringBuilder lpClassName, long nMaxCount);
        [DllImport("User32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindow(IntPtr hWnd);

        // for BringToFront() support
        [DllImport("user32.dll")]
        public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
        public const int SW_SHOWDEFAULT = 10, SW_MINIMIZE = 2, SW_SHOW = 5;
        // for PostMessage support (use readonly to prevent IDE0044)
        private readonly static uint WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101;
        public readonly static char KEY_ENTER = (char)13;
        #endregion

        public static void BringToFront(IntPtr hWnd)
        {// force the window handle owner to restore and activate to focus
            ShowWindowAsync(hWnd, SW_SHOWDEFAULT);
            ShowWindowAsync(hWnd, SW_SHOW);
            SetForegroundWindow(hWnd);
        }

        public static void MinimizeWindow(IntPtr hWnd)
        {// force the window handle to minimize
            ShowWindowAsync(hWnd, SW_MINIMIZE);
        }

        private static string GetCaptionOfWindow(IntPtr hWnd)
        {
            string caption = "";
            StringBuilder windowText = null;
            try
            {
                int max_length = GetWindowTextLength(hWnd);
                windowText = new StringBuilder("", max_length + 5);
                GetWindowText(hWnd, windowText, max_length + 2);

                if (!string.IsNullOrWhiteSpace(windowText.ToString()))
                    caption = windowText.ToString();
            }
            catch (Exception ex)
            {
                ProcessUtils.Logger("EXCEPTION", ex.Message);
            }
            return caption;
        }

        private static string GetClassNameOfWindow(IntPtr hWnd)
        {
            string className = "";
            StringBuilder classText = null;
            try
            {
                int cls_max_length = 1000;
                classText = new StringBuilder("", cls_max_length + 5);
                GetClassName(hWnd, classText, cls_max_length + 2);

                if (!string.IsNullOrWhiteSpace(classText.ToString()))
                    className = classText.ToString();
            }
            catch (Exception ex)
            {
                ProcessUtils.Logger("EXCEPTION", ex.Message);
            }
            return className;
        }

        public static bool MatchWindowDetails(string windowTitle, string windowClass, IntPtr hWnd)
        {// match exact ordinal title and class using the provided hWnd
            var _windowTitleTrg = GetCaptionOfWindow(hWnd);
            var _windowClassTrg = GetClassNameOfWindow(hWnd);

            if (ProcessUtils.FuzzyEquals(windowTitle, _windowTitleTrg) &&
                ProcessUtils.FuzzyEquals(windowClass, _windowClassTrg))
                return true;

            return false;
        }

        public static bool WindowHasDetails(IntPtr hWnd)
        {
            var _windowTitle = GetCaptionOfWindow(hWnd);
            var _windowClass = GetClassNameOfWindow(hWnd);

            if (_windowTitle.Length > 0 && _windowClass.Length > 0)
                return true;

            return false;
        }

        public static IntPtr GetHWND(Process proc)
        {
            try
            {
                if (proc != null && !proc.HasExited && proc.MainWindowHandle != IntPtr.Zero)
                    return proc.MainWindowHandle;
                return IntPtr.Zero;
            }
            catch (Exception) { return IntPtr.Zero; }
        }

        public static int DetectWindowType(Process proc)
        {
            int result = -1;
            if (proc != null)
            {
                var _hWnd = GetHWND(proc);
                string haystack = NativeProcessUtils.GetProcessModuleName(proc.Id);
                if (_hWnd != IntPtr.Zero)
                {
                    // excluded windows
                    if (ProcessUtils.OrdinalContains("EasyAntiCheat_launcher", haystack))
                        return -1;
                    else if (ProcessUtils.OrdinalContains("GOG Galaxy Notifications Renderer", haystack))
                        return -1;

                    // initially try to resolve by looking up className and windowTitle
                    if (MatchWindowDetails("Epic Games Launcher", "UnrealWindow", _hWnd))
                        result = 4; // Epic Games Launcher [Type 4]
                    else if (MatchWindowDetails("Uplay", "uplay_main", _hWnd))
                        result = 3; // Uplay [Type 3]
                    else if (MatchWindowDetails("Origin", "Qt5QWindowIcon", _hWnd))
                        result = 2; // Origin [Type 2]
                    else if (MatchWindowDetails("Blizzard Battle.net", "Qt5QWindowOwnDCIcon", _hWnd))
                        result = 1; // Blizzard Battle.net [Type 1]
                    else if (WindowHasDetails(_hWnd) || IsWindow(_hWnd))
                        result = 0; // catch all other obvious windows
                    if (result > -1)
                        return result;
                }
                // fallback to detection by module name
                if (ProcessUtils.OrdinalContains("EpicGamesLauncher", haystack))
                    result = 4;
                else if (ProcessUtils.OrdinalContains("upc", haystack))
                    result = 3;
                else if (ProcessUtils.OrdinalContains("Origin", haystack))
                    result = 2;
                else if (ProcessUtils.OrdinalContains("Battle.net", haystack))
                    result = 1;
            }
            return result;
        }

        private static bool MessageSendKey(IntPtr hWnd, char key)
        {// some windows don't take SendKeys so use the Async Window Message API instead
            try
            {
                BringToFront(hWnd); // we need window focus for sending
                PostMessage(hWnd, WM_KEYDOWN, (IntPtr)key, IntPtr.Zero);
                PostMessage(hWnd, WM_KEYUP, (IntPtr)key, IntPtr.Zero);
                return true;
            }
            catch (Exception ex)
            {
                ProcessUtils.Logger("WARNING", ex.Message);
                return false;
            }
        }

        public static bool SendEnterToForeground(IntPtr hWnd)
        {
            return MessageSendKey(hWnd, KEY_ENTER);
        }
    }
}
