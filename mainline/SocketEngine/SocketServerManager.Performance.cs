﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using SuperSocket.Common;

namespace SuperSocket.SocketEngine
{
    public static partial class SocketServerManager
    {
        private static Timer m_PerformanceTimer = new Timer(OnPerformanceTimerCallback);
        private static readonly int m_TimerInterval = 1000 * 60;//1 minute

        private static Timer m_CpuUsageTimer = new Timer(OnCpuUsageTimerCallback);
        private static readonly int m_CpuTimerInterval = 1000;//1 second
        private static double m_PrevTotalProcessorTime = 0;
        private static DateTime m_PrevCheckingTime = DateTime.MinValue;
        private static double m_CpuUsgae = 0;
        private static readonly long m_MbUnit = 1024 * 1024;


        private static void OnPerformanceTimerCallback(object state)
        {
            var process = Process.GetCurrentProcess();
            LogUtil.LogPerf(string.Format("CPU Usage: {0}%, Physical Memory Usage: {1}M, Virtual Memory Usage: {2}M, Total Thread Count: {3}", m_CpuUsgae.ToString("0.00"), process.WorkingSet64 / m_MbUnit, process.VirtualMemorySize64 / m_MbUnit, process.Threads.Count));
            int availableWorkingThreads, availableCompletionPortThreads;
            ThreadPool.GetAvailableThreads(out availableWorkingThreads, out availableCompletionPortThreads);
            LogUtil.LogPerf(string.Format("AvailableWorkingThreads: {0}, AvailableCompletionPortThreads: {1}", availableWorkingThreads, availableCompletionPortThreads));
            m_ServerList.ForEach(s =>
                {
                    var perfData = s.CollectPerformanceData();
                    s.Logger.LogPerf(string.Format("Total connections: {0}, total handled commands: {1}, command handling speed: {2}/s",
                        perfData.CurrentRecord.TotalConnections,
                        perfData.CurrentRecord.TotalHandledCommands,
                        (perfData.CurrentRecord.TotalHandledCommands - perfData.PreviousRecord.TotalHandledCommands) / perfData.CurrentRecord.RecordSpan));
                });
        }

        private static void OnCpuUsageTimerCallback(object state)
        {
            var process = Process.GetCurrentProcess();

            if (m_PrevTotalProcessorTime == 0)
                m_PrevCheckingTime = process.StartTime;

            double currentProcessorTime = process.TotalProcessorTime.TotalMilliseconds;
            DateTime currentCheckingTime = DateTime.Now;

            m_CpuUsgae = (currentProcessorTime - m_PrevTotalProcessorTime) * 100 / (currentCheckingTime.Subtract(m_PrevCheckingTime).TotalMilliseconds * Environment.ProcessorCount);
            m_PrevCheckingTime = currentCheckingTime;
            m_PrevTotalProcessorTime = currentProcessorTime;
        }

        private static void StartPerformanceLog()
        {
            m_PerformanceTimer.Change(m_TimerInterval, m_TimerInterval);
            m_CpuUsageTimer.Change(m_CpuTimerInterval, m_CpuTimerInterval);
        }

        private static void StopPerformanceLog()
        {
            m_PerformanceTimer.Change(Timeout.Infinite, Timeout.Infinite);
            m_CpuUsageTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }
}
