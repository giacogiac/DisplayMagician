using DisplayMagician.Processes;
using DisplayMagician.Resources;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace DisplayMagician.GameLibraries
{
    public class SteamBigPicture : Game
    {
        private string _gameRegistryKey;
        private string _steamBigPictureId;
        private string _steamBigPictureName;
        private string _steamExePath;
        private string _steamDir;
        private string _steamExe;
        private string _steamProcessName;
        private List<Process> _steamBigPictureProcesses = new List<Process>();
        private string _steamBigPictureIconPath;
        private static readonly SteamLibrary _steamBigPictureLibrary = SteamLibrary.GetLibrary();
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        static SteamBigPicture()
        {
            ServicePointManager.ServerCertificateValidationCallback +=
                (send, certificate, chain, sslPolicyErrors) => true;
        }

        public SteamBigPicture(string steamExePath)
        {
            _steamBigPictureId = "756";
            _gameRegistryKey = $@"{_steamBigPictureLibrary.SteamAppsRegistryKey}\\{_steamBigPictureId}";
            _steamBigPictureName = "Steam Big Picture";
            _steamExePath = steamExePath;
            _steamDir = Path.GetDirectoryName(steamExePath);
            _steamExe = Path.GetFileName(steamExePath);
            _steamProcessName = Path.GetFileNameWithoutExtension(steamExePath);
            _steamBigPictureIconPath = $"{_steamDir}\\resource\\icon_steamvr_desktop.png";
        }

        public override string Id { 
            get => _steamBigPictureId;
            set => _steamBigPictureId = value;
        }

        public override string Name
        {
            get => _steamBigPictureName;
            set => _steamBigPictureName = value;
        }

        public override SupportedGameLibraryType GameLibraryType { 
            get => SupportedGameLibraryType.Steam; 
        }

        [JsonIgnore]
        public override GameLibrary GameLibrary
        {
            get => _steamBigPictureLibrary;
        }

        public override string IconPath { 
            get => _steamBigPictureIconPath; 
            set => _steamBigPictureIconPath = value;
        }

        public override string ExePath
        {
            get => _steamExePath;
            set => _steamExePath = value;
        }

        public override string Directory
        {
            get => _steamDir;
            set => _steamDir = value;
        }

        public override string Executable 
        {
            get => _steamExe;
            set => _steamExe = value;
        }

        public override string ProcessName 
        {
            get => _steamProcessName;
            set => _steamProcessName = value;
        }

        public override List<Process> Processes
        {
            get => _steamBigPictureProcesses;
            set => _steamBigPictureProcesses = value;
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        public override bool IsRunning
        {
            get
            {
                // Pre-collect PIDs → huge speed boost
                var steamPids = Process.GetProcessesByName("steam").Select(p => p.Id).ToHashSet();
                var helperPids = Process.GetProcessesByName("steamwebhelper").Select(p => p.Id).ToHashSet();

                if (steamPids.Count == 0 && helperPids.Count == 0)
                    return false;

                bool found = false;

                EnumWindows((hWnd, lParam) =>
                {
                    // First check process (cheap)
                    GetWindowThreadProcessId(hWnd, out uint pid);
                    if (pid == 0)
                        return true;

                    int ipid = (int)pid;

                    if (!steamPids.Contains(ipid) && !helperPids.Contains(ipid))
                        return true; // skip fast

                    // Récupérer le titre
                    int len = GetWindowTextLength(hWnd);
                    if (len <= 0)
                        return true;

                    var sb = new StringBuilder(len + 1);
                    GetWindowText(hWnd, sb, sb.Capacity);

                    string title = sb.ToString()
                                    .Replace('\u00A0', ' '); // normalize NBSP

                    if (title.Contains("Big Picture", StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        return false; // stop enumeration early
                    }

                    return true;
                }, IntPtr.Zero);

                return found;
            }
        }

        public override bool IsUpdating
        {
            get
            {
                return false;
            }
        }

        public bool CopyInto(SteamBigPicture steamGame)
        {
            return false;
        }

        public override string ToString()
        {
            var name = _steamBigPictureName;

            if (string.IsNullOrWhiteSpace(name))
            {
                name = Language.Unknown;
            }

            if (IsRunning)
            {
                return name + " " + Language.Running;
            }

            if (IsUpdating)
            {
                return name + " " + Language.Updating;
            }

            return name;
        }

        public override bool Start(out List<Process> processesStarted, string gameArguments = "", ProcessPriority priority = ProcessPriority.Normal, int timeout = 20, bool runExeAsAdmin = false)
        {
            string address = $@"steam://open/bigpicture";
            if (!String.IsNullOrWhiteSpace(gameArguments))
            {
                address += @"//" + gameArguments;
            }
            processesStarted = ProcessUtils.StartProcess(address, null, priority);
            return true;
        }

        public override bool Stop()
        {
            return true;
        }

    }
}