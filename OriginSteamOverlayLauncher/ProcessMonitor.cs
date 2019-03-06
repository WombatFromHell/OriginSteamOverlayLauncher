using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace OriginSteamOverlayLauncher
{
    public class ProcessMonitor
    {
        private bool IsRunning { get; set; }
        private int GlobalElapsedTimer { get; set; } = 0;
        private ProcessObj Process { get; set; } = null;
        private int Timeout { get; set; } = 10;

        /// <summary>
        /// Takes a ProcessObj and a Timeout (in seconds, 10s by default)
        /// </summary>
        /// <param name="procObj"></param>
        /// <param name="timeout"></param>
        public ProcessMonitor(ProcessObj procObj, int timeout)
        {
            if (procObj != null && procObj.ProcessRef != null)
            {
                this.GlobalElapsedTimer = 0;
                this.Process = procObj;
                this.Timeout = timeout;
            }
        }

        public async Task<ProcessObj> InterruptibleMonitorAsync()
        {// always returns a ProcessObj after a monitoring timeout (could be old if unsuccessful)
            ProcessUtils.Logger("MONITOR", $"Monitoring process {this.Process.ProcessName}.exe for {this.Timeout}s");
            while (this.GlobalElapsedTimer < this.Timeout * 1000)
            {// reacquire within a timeout (non-blocking)
                await this.IncrementTimerAsync();
                if (this.Process.ProcessRef.HasExited)
                {// try to reacquire process by name
                    var _proc = new ProcessObj(this.Process.ProcessName);
                    if (_proc != null && _proc.IsValid)
                    {
                        this.GlobalElapsedTimer = 0;
                        ProcessUtils.Logger("MONITOR", $"Reacquired a matching target process ({_proc.ProcessName}.exe [{_proc.ProcessId}])");
                        this.Process = _proc; // don't assign a dead process
                        this.IsRunning = true;
                    }
                    else
                        this.IsRunning = false;
                }
                else
                    this.IsRunning = true;
            }
            // report our results once the timer expires
            if (this.IsRunning)
                ProcessUtils.Logger("MONITOR", $"Target process ({this.Process.ProcessName}.exe [{this.Process.ProcessId}]) is still running after {this.Timeout}s");
            else
                ProcessUtils.Logger("MONITOR", $"Timed out after {this.Timeout}s while monitoring the target process: {this.Process.ProcessName}.exe");
            return this.Process;
        }

        public async Task MonitorAsync()
        {
            await this.SpinnerAsync();
            // use InterruptibleMonitorAsync() for process reacquisition
            while (this.GlobalElapsedTimer < this.Timeout * 1000)
            {
                // reacquire if process exits within the timeout
                var _proc = await InterruptibleMonitorAsync();
                if (this.IsRunning) {
                    this.GlobalElapsedTimer = 0;
                    await this.SpinnerAsync();
                }
            }
            // timeout if our timer expires
        }

        private async Task<bool> SpinnerAsync()
        {
            Stopwatch _isw = new Stopwatch();
            _isw.Start();
            if (this.Process == null || this.Process.ProcessRef != null && this.Process.ProcessRef.HasExited)
            {// sanity check
                ProcessUtils.Logger("MONITOR", $"The target process {this.Process.ProcessName}.exe could not be found!");
                this.IsRunning = false;
                return true;
            }

            while (!this.Process.ProcessRef.HasExited)
            {
                this.IsRunning = true;
                await Task.Delay(1000);
            }
            _isw.Stop();
            this.IsRunning = false;
            ProcessUtils.Logger("MONITOR", $"Process exited after {ConvertElapsedToString(_isw.ElapsedMilliseconds)} attempting to reaquire {this.Process.ProcessName}.exe");
            return false;
        }

        private async Task IncrementTimerAsync()
        {
            await Task.Delay(1000);
            this.GlobalElapsedTimer += 1000;
        }

        private static string ConvertElapsedToString(long stopwatchElapsed)
        {
            double tempSecs = Convert.ToDouble(stopwatchElapsed / 1000);
            double tempMins = Convert.ToDouble(tempSecs / 60);
            // return minutes or seconds (if applicable)
            return tempSecs > 60 ? $"{tempMins:0.##}m" : $"{tempSecs:0.##}s";
        }
    }
}
