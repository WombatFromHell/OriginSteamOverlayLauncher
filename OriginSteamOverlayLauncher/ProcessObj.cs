using System;
using System.Diagnostics;
using System.Threading;

namespace OriginSteamOverlayLauncher
{
    /// <summary>
    ///  Helper object for Processes: a pName gets you a pId, pRef, and pType.
    /// </summary>
    public class ProcessObj
    {
        public string ProcessName { get; private set; }
        public int ProcessId { get; private set; } = 0;
        public int ProcessType { get; private set; } = -1;
        public Process ProcessRef { get; private set; } = null;
        public bool IsValid { get; private set; } = false;

        public bool Refresh()
        {
            var _procRefs = ProcessUtils.GetProcessesByName(this.ProcessName);
            if (_procRefs != null && _procRefs.Count > 0)
            {
                foreach (Process p in _procRefs)
                {// check each returned process for validity
                    if (p.Id > 0 && (p.MainWindowHandle != IntPtr.Zero && p.MainWindowTitle.Length > 0) ||
                        p.Id > 0)
                    {// prefer a process with a title and handle
                        ProcessRef = p;
                        ProcessId = ProcessRef.Id;
                        ProcessType = WindowUtils.DetectWindowType(ProcessRef);
                        IsValid = ProcessUtils.IsValidProcess(ProcessRef);
                        return true;
                    }
                }
            }
            return false;
        }

        public ProcessObj(string processName)
        {// search for a Process by name
            ProcessName = processName;
            this.Refresh();
        }

        public static bool Equals(ProcessObj procA, ProcessObj procB)
        {// helper for basic equality of ProcessObj objects
            if (procA != null && procB != null &&
                procA.ProcessId == procB.ProcessId &&
                procA.ProcessName.Equals(procB.ProcessName))
                return true;
            return false;
        }

        /// <summary>
        /// Helper function for validating a process within a timeout
        /// </summary>
        /// <param name="procObj"></param>
        /// <param name="timeout"></param>
        /// <param name="maxTimeout"></param>
        /// <param name="reattempts"></param>
        /// <returns></returns>
        public static ProcessObj ValidateProcessByName(string procName, int timer, int maxTimeout, int reattempts)
        {// check every x seconds (up to y seconds) for z iterations to determine valid running proc by name
            int timeoutCounter = 0, _pid = 0, prevPID = 0, elapsedTime = 0;
            ProcessUtils.Logger("OSOL", $"Searching for valid process by name: {procName}");

            while (timeoutCounter < (maxTimeout * 1000))
            {// wait up to maxTimeout (secs)
                ProcessObj _ret = internalLoop();
                if (_ret != null && _ret.IsValid) {
                    timeoutCounter += elapsedTime;
                    ProcessUtils.Logger("OSOL", $"Found a valid process in {timeoutCounter / 1000}s: {_ret.ProcessName}.exe [{_ret.ProcessId}]");
                    return _ret;
                }
                timeoutCounter += elapsedTime;
            }
            ProcessUtils.Logger("WARNING", $"Could not bind to a valid process after waiting {timeoutCounter / 1000}s");
            return null;

            // nest our internal loop so we can break out early if necessary
            ProcessObj internalLoop()
            {
                Stopwatch _sw = new Stopwatch();
                _sw.Start();
                for (int i = 0; i <= reattempts; i++)
                {// try up to the specified number of times
                    ProcessObj _proc = new ProcessObj(procName);
                    _pid = _proc.ProcessId;

                    if (prevPID > 0 && _pid != prevPID)
                    {
                        Thread.Sleep(timer * 1000);
                        _sw.Stop();
                        elapsedTime = Convert.ToInt32(_sw.ElapsedMilliseconds);
                        prevPID = 0;
                        break; // restart the search if we lose our target
                    }

                    if (i == reattempts && _pid == prevPID && _proc.IsValid)
                    {// wait for attempts to elapse (~15s by default) before validating the PID
                        _sw.Stop();
                        elapsedTime = Convert.ToInt32(_sw.ElapsedMilliseconds);
                        return _proc;
                    }

                    prevPID = _pid;
                    Thread.Sleep(timer * 1000);
                }
                _sw.Stop();
                elapsedTime = Convert.ToInt32(_sw.ElapsedMilliseconds);
                return null;
            }
        }

        /// <summary>
        /// Helper function for returning a validated processObj
        /// </summary>
        /// <param name="setHnd"></param>
        /// <param name="procName"></param>
        /// <returns></returns>
        public static ProcessObj GetProcessObj(Settings setHnd, string procName)
        {// wrapper function to return a validated ProcessObj from a procName
            ProcessObj _procObj = ValidateProcessByName(
                procName, setHnd.ProxyTimeout, setHnd.ProcessAcquisitionTimeout, setHnd.ProcessAcquisitionAttempts
            );

            if (_procObj != null && _procObj.ProcessId > 0)
                return _procObj;
            return null;
        }
    }
}
