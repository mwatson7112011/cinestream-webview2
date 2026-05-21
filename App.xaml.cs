using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using Microsoft.Win32;

namespace cinestream_webview2;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern bool MoveFileEx(string lpExistingFileName, string? lpNewFileName, uint dwFlags);
    private const uint MOVEFILE_DELAY_UNTIL_REBOOT = 0x00000004;

    protected override void OnStartup(StartupEventArgs e)
    {
        string logFile = GetLogPath();
        try
        {
            File.WriteAllText(logFile, $"OnStartup called. Args: {string.Join(" ", e.Args)}\n");

            base.OnStartup(e);

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string targetDir = Path.Combine(localAppData, "WatsonTechServices", "CineStream");
            string targetExe = Path.Combine(targetDir, "cinestream-webview2.exe");

            string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string desktopShortcut = Path.Combine(desktopDir, "CineStream.lnk");

            string startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs");
            string startMenuShortcut = Path.Combine(startMenuDir, "CineStream.lnk");

            // 1. Check for Uninstall flag
            bool isUninstall = false;
            bool devBypass = false;
            bool isSilent = false;
            bool forceInstall = false;

            foreach (var arg in e.Args)
            {
                if (arg.Equals("--uninstall", StringComparison.OrdinalIgnoreCase))
                {
                    isUninstall = true;
                }
                else if (arg.Equals("--install", StringComparison.OrdinalIgnoreCase))
                {
                    forceInstall = true;
                }
                else if (arg.Equals("--silent", StringComparison.OrdinalIgnoreCase))
                {
                    isSilent = true;
                }
                else if (arg.Equals("--dev", StringComparison.OrdinalIgnoreCase) || arg.Equals("--no-install", StringComparison.OrdinalIgnoreCase))
                {
                    devBypass = true;
                }
            }

            File.AppendAllText(logFile, $"Parsed args: isUninstall={isUninstall}, devBypass={devBypass}, isSilent={isSilent}, forceInstall={forceInstall}\n");

            if (isUninstall)
            {
                // Switch to persistent uninstall log file outside targetDir
                logFile = GetUninstallLogPath();
                File.WriteAllText(logFile, $"Uninstall process started. Target directory: {targetDir}\n");

                MessageBoxResult result = MessageBoxResult.Yes;
                if (!isSilent)
                {
                    result = MessageBox.Show(
                        "Are you sure you want to uninstall CineStream?",
                        "Uninstall CineStream",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question
                    );
                }

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // 1. Kill other running instances of CineStream to release shortcut/file locks
                        try
                        {
                            int currentPid = Process.GetCurrentProcess().Id;
                            Process[] processes = Process.GetProcessesByName("cinestream-webview2");
                            foreach (var proc in processes)
                            {
                                if (proc.Id != currentPid)
                                {
                                    try
                                    {
                                        proc.Kill();
                                        proc.WaitForExit(3000);
                                        File.AppendAllText(logFile, $"Successfully terminated running process {proc.Id}\n");
                                    }
                                    catch (Exception ex)
                                    {
                                        File.AppendAllText(logFile, $"Failed to kill running instance process {proc.Id}: {ex.Message}\n");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            File.AppendAllText(logFile, $"Error terminating running processes: {ex.Message}\n");
                        }

                        // Give Windows a brief moment to release locks
                        System.Threading.Thread.Sleep(500);

                        // 2. Remove desktop shortcut with retries and alternate fallbacks
                        try
                        {
                            File.AppendAllText(logFile, $"Starting desktop shortcut deletion. Path: {desktopShortcut}\n");
                            DeleteFileWithRetries(desktopShortcut, logFile);

                            // Try alternate desktop shortcut paths
                            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                            string localDesktop = Path.Combine(userProfile, "Desktop", "CineStream.lnk");
                            string oneDriveDesktop = Path.Combine(userProfile, "OneDrive", "Desktop", "CineStream.lnk");

                            if (!string.Equals(localDesktop, desktopShortcut, StringComparison.OrdinalIgnoreCase))
                            {
                                DeleteFileWithRetries(localDesktop, logFile);
                            }
                            if (!string.Equals(oneDriveDesktop, desktopShortcut, StringComparison.OrdinalIgnoreCase) && 
                                !string.Equals(oneDriveDesktop, localDesktop, StringComparison.OrdinalIgnoreCase))
                            {
                                DeleteFileWithRetries(oneDriveDesktop, logFile);
                            }
                        }
                        catch (Exception ex)
                        {
                            File.AppendAllText(logFile, $"Failed to delete desktop shortcut: {ex.Message}\n");
                        }

                        // 3. Remove start menu shortcut with retries and alternate fallbacks
                        try
                        {
                            File.AppendAllText(logFile, $"Starting start menu shortcut deletion. Path: {startMenuShortcut}\n");
                            DeleteFileWithRetries(startMenuShortcut, logFile);

                            // Try alternate start menu shortcuts
                            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                            string alternateStartMenu = Path.Combine(userProfile, @"AppData\Roaming\Microsoft\Windows\Start Menu\Programs", "CineStream.lnk");
                            if (!string.Equals(alternateStartMenu, startMenuShortcut, StringComparison.OrdinalIgnoreCase))
                            {
                                DeleteFileWithRetries(alternateStartMenu, logFile);
                            }
                        }
                        catch (Exception ex)
                        {
                            File.AppendAllText(logFile, $"Failed to delete start menu shortcut: {ex.Message}\n");
                        }

                        // Refresh icon cache to notify explorer of shortcut deletion
                        RefreshIconCache();
                        File.AppendAllText(logFile, "Shell notified of shortcut deletion to refresh icon cache.\n");

                        // 4. Remove registry entry
                        try
                        {
                            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\CineStream", false);
                            File.AppendAllText(logFile, "Registry entries removed successfully.\n");
                        }
                        catch (Exception ex)
                        {
                            File.AppendAllText(logFile, $"Failed to delete registry entry: {ex.Message}\n");
                        }

                        if (!isSilent)
                        {
                            MessageBox.Show(
                                "CineStream has been uninstalled successfully!",
                                "Uninstall Complete",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information
                            );
                        }

                        // 5. Spawn detached powershell.exe to wait for us to exit and delete the target directory
                        try
                        {
                            int currentPid = Process.GetCurrentProcess().Id;
                            string escapedTargetDir = targetDir.Replace("'", "''");
                            string deleteCmd = $"-NoProfile -WindowStyle Hidden -Command \"Start-Sleep -Seconds 1; $p = Get-Process -Id {currentPid} -ErrorAction SilentlyContinue; if ($p) {{ $p.WaitForExit(10000) }}; Remove-Item -Path '{escapedTargetDir}' -Recurse -Force\"";
                            
                            File.AppendAllText(logFile, $"Spawning detached process to delete target directory: {targetDir}\n");
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "powershell.exe",
                                Arguments = deleteCmd,
                                CreateNoWindow = true,
                                UseShellExecute = false
                            });
                        }
                        catch (Exception ex)
                        {
                            File.AppendAllText(logFile, $"Failed to spawn directory deletion process: {ex.Message}\n");
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!isSilent)
                        {
                            MessageBox.Show(
                                $"An error occurred during uninstallation: {ex.Message}",
                                "Uninstall Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error
                            );
                        }
                        else
                        {
                            Console.Error.WriteLine($"Uninstall Error: {ex.Message}");
                        }
                        File.AppendAllText(logFile, $"Fatal uninstallation error: {ex.Message}\n{ex.StackTrace}\n");
                    }
                }

                Shutdown();
                return;
            }

            // 2. Check installation location
            string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? Assembly.GetExecutingAssembly().Location;
            string currentDir = AppDomain.CurrentDomain.BaseDirectory;

            File.AppendAllText(logFile, $"currentExe='{currentExe}'\ncurrentDir='{currentDir}'\ntargetExe='{targetExe}'\n");

            bool isInstalledLocation = string.Equals(
                Path.GetFullPath(currentExe),
                Path.GetFullPath(targetExe),
                StringComparison.OrdinalIgnoreCase
            );

            File.AppendAllText(logFile, $"isInstalledLocation={isInstalledLocation}\n");

            if (isInstalledLocation || (devBypass && !forceInstall))
            {
                File.AppendAllText(logFile, "Starting MainWindow normally...\n");
                // Start the application normally
                var mainWindow = new MainWindow();
                mainWindow.Show();
            }
            else
            {
                MessageBoxResult result = MessageBoxResult.No;
                if (forceInstall)
                {
                    result = MessageBoxResult.Yes;
                }
                else
                {
                    // Prompt to install
                    result = MessageBox.Show(
                        "CineStream is not installed on this computer.\n\nWould you like to install it now?",
                        "Install CineStream",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question
                    );
                }

                File.AppendAllText(logFile, $"Install dialog result: {result}\n");

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        File.AppendAllText(logFile, "Starting directory copying...\n");
                        // Copy directories and files
                        CopyDirectory(currentDir, targetDir);
                        File.AppendAllText(logFile, "Directory copying complete.\n");

                        // Register in Windows Control Panel (Add/Remove Programs) for Current User
                        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\CineStream"))
                        {
                            key.SetValue("DisplayName", "CineStream");
                            key.SetValue("DisplayVersion", "1.0.0");
                            key.SetValue("Publisher", "Watson's Tech Services");
                            key.SetValue("UninstallString", $"\"{targetExe}\" --uninstall");
                            key.SetValue("InstallLocation", targetDir);
                            key.SetValue("DisplayIcon", targetExe);
                            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                        }
                        File.AppendAllText(logFile, "Registry values written.\n");

                        // Create shortcuts
                        string targetIcon = Path.Combine(targetDir, "app.ico");
                        CreateShortcut(desktopShortcut, targetExe, targetDir, targetIcon);
                        CreateShortcut(startMenuShortcut, targetExe, targetDir, targetIcon);
                        RefreshIconCache();
                        File.AppendAllText(logFile, "Shortcuts created and icon cache refreshed.\n");

                        if (!isSilent)
                        {
                            MessageBox.Show(
                                "CineStream has been installed successfully!\n\nYou can launch it from your Desktop or Start Menu.",
                                "Installation Successful",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information
                            );
                        }

                        // Launch the installed copy unless we are in silent/force mode from command-line testing
                        if (!isSilent)
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = targetExe,
                                WorkingDirectory = targetDir,
                                UseShellExecute = true
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        File.AppendAllText(logFile, $"EXCEPTION during install: {ex.Message}\n{ex.StackTrace}\n");
                        if (!isSilent)
                        {
                            MessageBox.Show(
                                $"Failed to install CineStream: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}",
                                "Installation Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error
                            );
                        }
                        else
                        {
                            Console.Error.WriteLine($"Installation Error: {ex.Message}");
                        }
                    }
                }

                Shutdown();
            }
        }
        catch (Exception ex)
        {
            var innerMsg = "";
            var inner = ex.InnerException;
            while (inner != null)
            {
                innerMsg += $"\nInner Exception: {inner.Message}\n{inner.StackTrace}";
                inner = inner.InnerException;
            }
            File.AppendAllText(logFile, $"FATAL EXCEPTION in OnStartup: {ex.Message}\n{ex.StackTrace}{innerMsg}\n");
            Shutdown();
        }
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        string logFile = GetLogPath();
        try
        {
            File.AppendAllText(logFile, $"CopyDirectory called: sourceDir='{sourceDir}', destDir='{destDir}'\n");
            Directory.CreateDirectory(destDir);

            // Copy files
            var files = Directory.GetFiles(sourceDir);
            File.AppendAllText(logFile, $"Found {files.Length} files in '{sourceDir}'\n");
            foreach (string file in files)
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.AppendAllText(logFile, $"Copying file: '{file}' -> '{destFile}'\n");
                File.Copy(file, destFile, true);
            }

            // Copy subdirectories
            var subDirs = Directory.GetDirectories(sourceDir);
            File.AppendAllText(logFile, $"Found {subDirs.Length} subdirectories in '{sourceDir}'\n");
            foreach (string subDir in subDirs)
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir);
            }
        }
        catch (Exception ex)
        {
            File.AppendAllText(logFile, $"EXCEPTION in CopyDirectory: {ex.Message}\n{ex.StackTrace}\n");
            throw;
        }
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string workingDir, string iconPath)
    {
        try
        {
            string escapedShortcutPath = shortcutPath.Replace("'", "''");
            string escapedTargetPath = targetPath.Replace("'", "''");
            string escapedWorkingDir = workingDir.Replace("'", "''");
            string escapedIconPath = iconPath.Replace("'", "''");

            string arguments = $"-NoProfile -WindowStyle Hidden -Command \"$s = (New-Object -ComObject WScript.Shell).CreateShortcut('{escapedShortcutPath}'); $s.TargetPath = '{escapedTargetPath}'; $s.WorkingDirectory = '{escapedWorkingDir}'; $s.IconLocation = '{escapedIconPath}'; $s.Save()\"";
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using (Process? p = Process.Start(psi))
            {
                p?.WaitForExit(3000);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to create shortcut at {shortcutPath}: {ex.Message}");
        }
    }

    private static void DeleteFileWithRetries(string filePath, string logFile)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.AppendAllText(logFile, $"Attempting to delete shortcut: {filePath}\n");
                bool deleted = false;
                for (int i = 0; i < 15; i++)
                {
                    try
                    {
                        File.Delete(filePath);
                        deleted = true;
                        File.AppendAllText(logFile, $"Deleted shortcut successfully: {filePath}\n");
                        break;
                    }
                    catch (Exception ex)
                    {
                        File.AppendAllText(logFile, $"Retry {i+1} failed to delete {filePath}: {ex.Message}\n");
                        System.Threading.Thread.Sleep(1000);
                    }
                }

                if (!deleted)
                {
                    File.AppendAllText(logFile, $"Shortcut remains locked after retries. Scheduling reboot deletion for: {filePath}\n");
                    MoveFileEx(filePath, null, MOVEFILE_DELAY_UNTIL_REBOOT);
                }
            }
            else
            {
                File.AppendAllText(logFile, $"Shortcut not found to delete: {filePath}\n");
            }
        }
        catch (Exception ex)
        {
            File.AppendAllText(logFile, $"Exception in DeleteFileWithRetries for {filePath}: {ex.Message}\n");
        }
    }

    private static string GetUninstallLogPath()
    {
        try
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string parentDir = Path.Combine(localAppData, "WatsonTechServices");
            Directory.CreateDirectory(parentDir);
            return Path.Combine(parentDir, "cinestream_uninstall.log");
        }
        catch
        {
            return Path.Combine(Path.GetTempPath(), "WatsonTechServices_CineStream_uninstall.log");
        }
    }

    private static string GetLogPath()
    {
        try
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string targetDir = Path.Combine(localAppData, "WatsonTechServices", "CineStream");
            Directory.CreateDirectory(targetDir);
            return Path.Combine(targetDir, "install.log");
        }
        catch
        {
            return Path.Combine(Path.GetTempPath(), "WatsonTechServices_CineStream_install.log");
        }
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private static void RefreshIconCache()
    {
        try
        {
            // SHCNE_ASSOCCHANGED = 0x08000000
            SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to notify shell of changes: {ex.Message}");
        }
    }
}
