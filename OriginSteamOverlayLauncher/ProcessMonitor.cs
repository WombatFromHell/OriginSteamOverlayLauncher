using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace OriginSteamOverlayLauncher
{
    public class ProcessEventArgs : EventArgs
    {
        public Process TargetProcess { get; set; } = null;
        public int ProcessType { get; set; } = -1;
        public string ProcessName { get; set; } = "";
        public double Elapsed { get; set; } = 0;
        public int Timeout { get; set; } = 0;
    }

    /// <summary>
    /// Provides asynchronous monitoring and synchronization of Process objects
    /// Depends on: ProcessLauncher class
    /// </summary>
    public class ProcessMonitor : IDisposable
    {
        #region Initialization
        public delegate void ProcessAcquiredHandler(ProcessMonitor m, ProcessEventArgs e);
        public delegate void ProcessSoftExitHandler(ProcessMonitor m, ProcessEventArgs e);
        public delegate void ProcessHardExitHandler(ProcessMonitor m, ProcessEventArgs e);
        public event EventHandler<ProcessEventArgs> ProcessAcquired;
        public event EventHandler<ProcessEventArgs> ProcessSoftExit;
        public event EventHandler<ProcessEventArgs> ProcessHardExit;

        public bool TimeoutCancelled { get; private set; }
        public int WindowType { get => TargetLauncher?.GetProcessType() ?? -1; }

        private int GlobalTimeout { get; set; }
        private int InnerTimeout { get; set; }
        private Timer MonitorTimer { get; set; }
        private Timer SearchTimer { get; set; }
        private ProcessLauncher TargetLauncher { get; set; }
        private ProcessLauncher LastKnown { get; set; }
        private string MonitorName { get; set; }

        private readonly int Interval = 1000; // tick interval
        private SemaphoreSlim MonitorLock { get; }
        private bool Disposed { get; set; }
        private bool HasAcquired { get; set; }

        /// <summary>
        /// Threaded timer for monitoring and validating a process heuristically
        /// </summary>
        public ProcessMonitor(ProcessLauncher procLauncher, int globalTimeout, int innerTimeout, string altProcName = "")
        {// constructor for GamePath + MonitorPath instances
            TargetLauncher = procLauncher;
            GlobalTimeout = globalTimeout;
            InnerTimeout = innerTimeout;
            MonitorName = !string.IsNullOrWhiteSpace(altProcName) ? altProcName : "";

            MonitorLock = new SemaphoreSlim(1, 1);
            MonitorTimer = new Timer(MonitorProcess);
            SearchTimer = new Timer(SearchProcess);
            MonitorTimer.Change(0, Interval);
        }
        #endregion

        #region Dispose
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Disposed)
                return;

            if (disposing)
            {
                MonitorTimer.Change(Timeout.Infinite, Timeout.Infinite);
                SearchTimer.Change(Timeout.Infinite, Timeout.Infinite);
                MonitorTimer.Dispose();
                SearchTimer.Dispose();
                MonitorLock.Dispose();
            }
            Disposed = true;
        }

        ~ProcessMonitor()
        {
            Dispose(false);
        }
        #endregion

        private void UpdateRef()
        {// tell ProcessLauncher to target the Monitor if applicable
            if (MonitorName.Length > 0)
                TargetLauncher?.Refresh(MonitorName);
            else
                TargetLauncher?.Refresh();
        }

        public bool IsRunning()
        {
            return !TimeoutCancelled && TargetLauncher != null &&
                TargetLauncher.TargetProcess != null &&
                TargetLauncher.IsRunning() &&
                TargetLauncher.TargetPID > 0;
        }

        public void Restart()
        {
            TimeoutCancelled = false;
            HasAcquired = false;
            MonitorTimer.Change(0, Interval);
            SearchTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public void Stop()
        {// attempt to gracefully exit threads
            TimeoutCancelled = true;
            HasAcquired = false;
            MonitorTimer.Change(Timeout.Infinite, Timeout.Infinite);
            SearchTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private async Task TimeoutWatcher(int timeout)
        {// workhorse for our timer delegates
            UpdateRef(); // make sure to update before and during our loop
            if (HasAcquired && IsRunning())
                return; // bail while process is healthy

            Stopwatch sw = Stopwatch.StartNew();
            double lastTime = sw.ElapsedMilliseconds, elapsedTimer = 0;
            while (!TimeoutCancelled && elapsedTimer < timeout * 1000)
            {
                await Task.Delay(Interval);
                elapsedTimer += sw.ElapsedMilliseconds - lastTime;
                lastTime = sw.ElapsedMilliseconds;

                UpdateRef();
                if (TargetLauncher != null && IsRunning())
                    LastKnown = TargetLauncher;

                if (!HasAcquired && IsRunning())
                {
                    OnProcessAcquired(this, new ProcessEventArgs
                    {
                        TargetProcess = TargetLauncher.TargetProcess,
                        ProcessName = TargetLauncher.ProcessName,
                        ProcessType = TargetLauncher.GetProcessType(),
                        Elapsed = elapsedTimer
                    });
                    return;
                }
                else if (HasAcquired && !IsRunning())
                {
                    OnProcessSoftExit(this, new ProcessEventArgs
                    {
                        TargetProcess = LastKnown?.TargetProcess,
                        ProcessName = MonitorName.Length > 0 ? MonitorName : LastKnown?.ProcessName,
                        Timeout = timeout
                    });
                }
            }
            // timed out
            if (!TimeoutCancelled)
                OnProcessHardExit(this, new ProcessEventArgs
                {
                    TargetProcess = LastKnown?.TargetProcess,
                    ProcessName = MonitorName.Length > 0 ? MonitorName : LastKnown?.ProcessName ?? "",
                    Elapsed = elapsedTimer,
                    Timeout = timeout
                });
        }

        #region Delegates
        /// <summary>
        /// TimerCallback delegate for threaded timer to monitor a named process
        /// </summary>
        private async void MonitorProcess(object stateInfo)
        {// only used when initially acquiring a process
            await MonitorLock.WaitAsync();
            try
            {// monitor with a long initial timeout (for loading/updates)
                if (!TimeoutCancelled)
                    await TimeoutWatcher(GlobalTimeout);
                else
                    MonitorTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            finally
            {
                MonitorLock.Release();
            }
        }

        /// <summary>
        /// TimerCallback delegate for threaded timer to reacquire a valid process by name
        /// </summary>
        private async void SearchProcess(object stateInfo)
        {// used when searching for a valid process within a timeout
            await MonitorLock.WaitAsync();
            try
            {// monitor with a shorter timeout for the search
                if (!TimeoutCancelled)
                    await TimeoutWatcher(InnerTimeout);
                else
                    SearchTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            finally
            {
                MonitorLock.Release();
            }
        }
        #endregion

        #region Event Handlers
        private void OnProcessAcquired(ProcessMonitor m, ProcessEventArgs e)
        {
            ProcessUtils.Logger("MONITOR",
                $"Process acquired in {ProcessUtils.ElapsedToString(e.Elapsed)}: {e.ProcessName}.exe [{e.TargetProcess.Id}]");

            HasAcquired = true;
            MonitorTimer.Change(Timeout.Infinite, Timeout.Infinite);
            SearchTimer.Change(0, Interval);
            ProcessAcquired?.Invoke(m, e);
        }

        private void OnProcessSoftExit(ProcessMonitor m, ProcessEventArgs e)
        {// attempt to gracefully switch modes (monitor -> search)
            if (HasAcquired && !string.IsNullOrWhiteSpace(e.ProcessName))
                ProcessUtils.Logger("MONITOR", $"Process exited, attempting to reacquire within {e.Timeout}s: {e.ProcessName}.exe");
            else if (HasAcquired)  // can't get process details?
                ProcessUtils.Logger("MONITOR", $"Process exited, attempting to reacquire within {e.Timeout}s");

            HasAcquired = false;
            // transition from monitoring -> searching
            MonitorTimer.Change(Timeout.Infinite, Timeout.Infinite);
            SearchTimer.Change(0, Interval);

            ProcessSoftExit?.Invoke(m, e);
        }

        private void OnProcessHardExit(ProcessMonitor m, ProcessEventArgs e)
        {
            if (e.TargetProcess != null)
                ProcessUtils.Logger("MONITOR",
                    $"Timed out after {ProcessUtils.ElapsedToString(e.Elapsed)} searching for a matching process: {e.ProcessName}.exe");
            else
                ProcessUtils.Logger("MONITOR",
                    $"Could not detect a running process after waiting {ProcessUtils.ElapsedToString(e.Elapsed)}...");

            Stop();
            ProcessHardExit?.Invoke(m, e);
        }
        #endregion
    }
}
