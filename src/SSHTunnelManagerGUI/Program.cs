﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SSHTunnelManager.Business;
using SSHTunnelManager.Domain;
using SSHTunnelManager.Util;
using SSHTunnelManagerGUI.Forms;
using SSHTunnelManagerGUI.Properties;
using log4net.Core;

namespace SSHTunnelManagerGUI
{
    public static class Program
    {
        private class StartUpArgs
        {
            public bool Minimized { get; set; }
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Main(string[] args)
        {
            /*var enc = CryptoHelper.EncryptStringAes("Hello, World!...", "qwerty7");
            var ret = CryptoHelper.DecryptStringAes(enc, "qwerty7");*/

            // We can run single instance only here based on Visual Studio
            // Project Settings. So initialize instance to null here.
            Process instance = null;

            // If we are supposed to, check to see if we are the only instance of this
            // program running.
            if (Settings.Default.StartSingleInstanceOnly)
                instance = RunningInstance();

            // If we are the only running instance of this program then continue.
            if (instance == null)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                var startUpArgs = parseArgs(args);

                var startUpDlg = new StartUpDialog();
                if (startUpDlg.DialogRequired && startUpDlg.ShowDialog() == DialogResult.Cancel)
                    return;

                // Apply config
                var cfg = new Config
                              {
                                  RestartEnabled = Settings.Default.Config_RestartEnabled,
                                  MaxAttemptsCount = Settings.Default.Config_MaxAttemptsCount,
                                  RestartDelay = Settings.Default.Config_RestartDelay,
                                  DelayInsteadStop = Settings.Default.Config_AfterMaxAttemptsMakeDelay,
                                  RestartHostsWithWarnings = Settings.Default.Config_RestartHostsWithWarnings,
                                  RestartHostsWithWarningsInterval = Settings.Default.Config_RestartHostsWithWarningsInterval
                              };
                Logger.SetThresholdForAppender(HostLogDelegateAppender, Settings.Default.Config_TraceDebug ? Level.Debug : Level.Info);

                var hm = new HostsManager<HostViewModel>(cfg, startUpDlg.Storage, startUpDlg.Filename, startUpDlg.Password);

                bool firstStart = true;

                // changeSource request handling
                while (true)
                {
                    var mainForm = new MainForm(hm, firstStart && startUpArgs.Minimized);
                    firstStart = false;

                    Application.Run(mainForm);

                    if (mainForm.ChangeSourceRequested)
                    {
                        startUpDlg = new StartUpDialog();
                        if (startUpDlg.ShowDialog() == DialogResult.Cancel)
                        {
                            return;
                        }

                        hm = new HostsManager<HostViewModel>(
                            cfg,
                            startUpDlg.Storage,
                            startUpDlg.Filename,
                            startUpDlg.Password);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            else
            {
                // We are not the only running instance of this program. So do this.
                HandleRunningInstance(instance);
            }
        }

        private static StartUpArgs parseArgs(string[] args)
        {
            var ret = new StartUpArgs();
            for (int i = 0; i < args.Length; i++)
            {
                var s = args[i];
                if (s.Equals("/minimized", StringComparison.InvariantCultureIgnoreCase))
                {
                    ret.Minimized = true;
                }

                // TODO: Extend it or simply switch to external parseArgs solution.
            }

            return ret;
        }

        // Look at all currently runninng processes and see if there is already one of
        // us running with the name of TheNotifyIconExamplewc1.exe
        public static Process RunningInstance()
        {
            Process current = Process.GetCurrentProcess();
            Process[] processes = Process.GetProcessesByName(current.ProcessName);

            // Вернуть процесс с таким же названием и исполняемым файлом ИЛИ null
            return processes.Where(process => process.Id != current.Id).FirstOrDefault(process =>
                string.Equals(Helper.GetExecutablePath(), process.MainModule.FileName, StringComparison.CurrentCultureIgnoreCase));
        }

        // Bring to focus the current running process with our name
        // and we will go away without doing anything more.
        public static void HandleRunningInstance(Process instance)
        {
            ShowWindowAsync(instance.MainWindowHandle, WS_SHOWNORMAL);
            SetForegroundWindow(instance.MainWindowHandle);
        }

        [DllImport(@"User32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int cmdShow);

        [DllImport(@"User32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const int WS_SHOWNORMAL = 1;
        public const string HostLogDelegateAppender = "HostLogDelegateAppender";
        public const string CommonErrorsDelegateAppender = "CommonErrorsDelegateAppender";
    }
}
