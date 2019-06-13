using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

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
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        // for BringToFront() support
        [DllImport("user32.dll")]
        public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
        public const int SW_SHOWDEFAULT = 10, SW_MINIMIZE = 2, SW_SHOW = 5;
        // for SendMessage support (use readonly to prevent IDE0044)
        private readonly static uint WM_KEYDOWN = 0x100, WM_KEYUP = 0x101;
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

        public static int GetWindowType(Process proc)
        {
            if (proc != null)
            {
                var _hwnd = ProcessWrapper.GetHWND(proc);
                var _result = DetectWindowType(_hwnd);
                return _result;
            }
            return -1;
        }

        public static int DetectWindowType(IntPtr hWnd)
        {// case testing for window class and title matching
            if (hWnd != IntPtr.Zero)
            {
                // excluded windows
                if (MatchWindowDetails("Blizzard Battle.net Login", "Qt5QWindowIcon", hWnd))
                    return -1; // Battle.net Login Window
                if (MatchWindowDetails("Uplay", "uplay_start", hWnd))
                    return -1; // Uplay Startup/Login Window

                if (MatchWindowDetails("Epic Games Launcher", "UnrealWindow", hWnd))
                    return 4; // Epic Games Launcher [Type 4]
                else if (MatchWindowDetails("Uplay", "uplay_main", hWnd))
                    return 3; // Uplay [Type 3]
                else if (MatchWindowDetails("Origin", "Qt5QWindowIcon", hWnd))
                    return 2; // Origin [Type 2]
                else if (MatchWindowDetails("Blizzard Battle.net", "Qt5QWindowOwnDCIcon", hWnd))
                    return 1; // Blizzard Battle.net [Type 1]
                else if (WindowHasDetails(hWnd))
                    return 0; // all other valid foreground windows
            }
            return -1;
        }

        public static bool MessageSendKey(IntPtr hWnd, char key)
        {// some windows don't take SendKeys so use the Window Message API instead
            try
            {
                // we need to send two messages per key, KEYDOWN and KEYUP respectively
                SendMessage(hWnd, WM_KEYDOWN, (IntPtr)key, IntPtr.Zero);
                SendMessage(hWnd, WM_KEYUP, (IntPtr)key, IntPtr.Zero);

                return true;
            }
            catch (Exception ex)
            {
                ProcessUtils.Logger("WARNING", ex.Message);
                return false;
            }
        }
    }
}
