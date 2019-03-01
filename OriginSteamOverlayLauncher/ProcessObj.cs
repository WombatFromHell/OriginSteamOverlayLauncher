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
        public int ProcessId { get; private set; }
        public int ProcessType { get; private set; }
        public Process ProcessRef { get; private set; }
        public bool IsValid { get; private set; }

        public void Refresh()
        {
            var _procRef = ProcessUtils.GetLastProcessChildByName(this.ProcessName);
            ProcessId = _procRef != null ? _procRef.Id : 0;
            if (this.ProcessId > 0)
            {
                ProcessRef = _procRef;
                ProcessType = WindowUtils.DetectWindowType(ProcessRef);
                IsValid = ProcessUtils.IsValidProcess(ProcessRef);
            }
        }

        public ProcessObj(string processName)
        {
            ProcessName = processName;
            this.Refresh();
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
            ProcessUtils.Logger("OSOL", String.Format("Searching for valid process by name: {0}", procName));

            while (timeoutCounter < (maxTimeout * 1000))
            {// wait up to maxTimeout (secs)
                ProcessObj _ret = internalLoop();
                if (_ret != null) { return _ret; }
                timeoutCounter += elapsedTime;
            }
            ProcessUtils.Logger("WARNING", String.Format("Could not bind to a valid process after waiting {0} seconds",
                timeoutCounter/1000)
            );
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
                        ProcessUtils.Logger("OSOL", String.Format("Found a valid process at PID: {0} [{1}] in {2}s",
                            _pid,
                            String.Format("{0}.exe", procName),
                            (elapsedTime / 1000))
                        );
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
