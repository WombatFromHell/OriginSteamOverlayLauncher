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

        public static void BringToFront(IntPtr wHnd)
        {// force the window handle owner to restore and activate to focus
            ShowWindowAsync(wHnd, SW_SHOWDEFAULT);
            ShowWindowAsync(wHnd, SW_SHOW);
            SetForegroundWindow(wHnd);
        }

        public static void MinimizeWindow(IntPtr wHnd)
        {// force the window handle to minimize
            ShowWindowAsync(wHnd, SW_MINIMIZE);
        }

        private static string GetCaptionOfWindow(IntPtr hwnd)
        {
            string caption = "";
            StringBuilder windowText = null;

            try
            {
                int max_length = GetWindowTextLength(hwnd);
                windowText = new StringBuilder("", max_length + 5);
                GetWindowText(hwnd, windowText, max_length + 2);

                if (!String.IsNullOrEmpty(windowText.ToString()) && !String.IsNullOrWhiteSpace(windowText.ToString()))
                    caption = windowText.ToString();
            }
            catch (Exception ex)
            {
                ProcessUtils.Logger("EXCEPTION", ex.Message);
            }
            finally
            {
                windowText = null;
            }

            return caption;
        }

        private static string GetClassNameOfWindow(IntPtr hwnd)
        {
            string className = "";
            StringBuilder classText = null;

            try
            {
                int cls_max_length = 1000;
                classText = new StringBuilder("", cls_max_length + 5);
                GetClassName(hwnd, classText, cls_max_length + 2);

                if (!String.IsNullOrEmpty(classText.ToString()) && !String.IsNullOrWhiteSpace(classText.ToString()))
                    className = classText.ToString();
            }
            catch (Exception ex)
            {
                ProcessUtils.Logger("EXCEPTION", ex.Message);
            }
            finally
            {
                classText = null;
            }

            return className;
        }
        //

        public static bool MatchWindowDetails(String windowTitle, String windowClass, IntPtr hWnd)
        {// match exact ordinal title and class using the provided hWnd
            var _windowTitleTrg = GetCaptionOfWindow(hWnd);
            var _windowClassTrg = GetClassNameOfWindow(hWnd);

            if (ProcessUtils.OrdinalContains(windowTitle, _windowTitleTrg)
                    && ProcessUtils.StringEquals(windowClass, _windowClassTrg))
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

        public static IntPtr HwndFromProc(Process procHandle)
        {// just a helper to return an hWnd from a given Process (if it has a window handle)
            return procHandle.MainWindowHandle != null ? procHandle.MainWindowHandle : (IntPtr)null;
        }

        public static int DetectWindowType(Process procHnd)
        {// case testing for window class and title matching
            var processType = -1;
            var _hWnd = HwndFromProc(procHnd);

            if (_hWnd != null)
            {// since we've got a window handle let's pass on what we find
                var _titleComp = "";
                var _classComp = "";

                //
                // test for QT5 based launchers
                //

                // Origin Launcher [Type 2]
                _titleComp = "Origin";
                _classComp = "Qt5QWindowIcon";

                // class must match but title may differ
                if (MatchWindowDetails(_titleComp, _classComp, _hWnd))
                    return processType = 2;

                // Blizzard Battle.net Launcher [Type 1]
                _titleComp = "Blizzard Battle.net";

                if (MatchWindowDetails(_titleComp, _classComp, _hWnd))
                    return processType = 1;

                //
                // catch-all for everything else [Type 0]
                //

                // just check if we've got a window class and title
                if (WindowHasDetails(_hWnd))
                    return processType = 0;
            }

            return processType;
        }

        public static bool MessageSendKey(Process proc, char key)
        {// some windows don't take SendKeys so use the Window Message API instead
            try
            {
                // we need to send two messages per key, KEYDOWN and KEYUP respectively
                SendMessage(WindowUtils.HwndFromProc(proc), WM_KEYDOWN, (IntPtr)key, IntPtr.Zero);
                SendMessage(WindowUtils.HwndFromProc(proc), WM_KEYUP, (IntPtr)key, IntPtr.Zero);

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
