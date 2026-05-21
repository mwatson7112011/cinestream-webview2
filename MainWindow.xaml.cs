using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Web.WebView2.Core;

namespace cinestream_webview2;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private Dictionary<string, WebView2> _serviceWebViews = new();
    private string? _activeServiceName = null;
    private CoreWebView2Environment? _commonEnvironment = null;

    private WindowStyle _previousWindowStyle = WindowStyle.SingleBorderWindow;
    private WindowState _previousWindowState = WindowState.Normal;
    private ResizeMode _previousResizeMode = ResizeMode.CanResize;

    private System.Windows.Threading.DispatcherTimer? _hoverTimer = null;

    public MainWindow()
    {
        InitializeComponent();
        LoadWindowIcon();
        InitializeMainWebView();
        InitializeHoverTimer();
    }

    private void InitializeHoverTimer()
    {
        _hoverTimer = new System.Windows.Threading.DispatcherTimer();
        _hoverTimer.Interval = TimeSpan.FromMilliseconds(50);
        _hoverTimer.Tick += HoverTimer_Tick;
    }

    private void LoadWindowIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (File.Exists(iconPath))
            {
                this.Icon = BitmapFrame.Create(new Uri(Path.GetFullPath(iconPath), UriKind.Absolute));
            }
            else
            {
                iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "assets", "logo.png");
                if (File.Exists(iconPath))
                {
                    this.Icon = BitmapFrame.Create(new Uri(Path.GetFullPath(iconPath), UriKind.Absolute));
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load window icon: {ex.Message}");
        }
    }

    private async void InitializeMainWebView()
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var commonDataPath = Path.Combine(appData, "CineStream", "webview_data");

            // Create a single shared environment for the entire application
            _commonEnvironment = await CoreWebView2Environment.CreateAsync(null, commonDataPath);
            
            // Initialize main webview with the shared environment
            await MainWebView.EnsureCoreWebView2Async(_commonEnvironment);

            // Map http://cinestream.local to wwwroot folder
            string wwwrootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");
            MainWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "cinestream.local",
                wwwrootPath,
                CoreWebView2HostResourceAccessKind.Allow
            );

            // Add message received handler
            MainWebView.CoreWebView2.WebMessageReceived += MainWebView_WebMessageReceived;

            // Add navigation completed handler for dynamic profile personalization
            MainWebView.CoreWebView2.NavigationCompleted += MainWebView_NavigationCompleted;

            // Add process failed handler to monitor renderer/GPU crashes
            MainWebView.CoreWebView2.ProcessFailed += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"[CineStream Host] MainWebView Process Failed: {e.ProcessFailedKind}, Reason: {e.Reason}, ExitCode: {e.ExitCode}");
            };

            // Configure basic settings
            MainWebView.CoreWebView2.Settings.IsWebMessageEnabled = true;

            // Navigate to local main page
            MainWebView.CoreWebView2.Navigate("http://cinestream.local/index.html");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize Main WebView: {ex.Message}\nStack Trace: {ex.StackTrace}", "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void MainWebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            try
            {
                string currentUri = MainWebView.Source?.ToString() ?? "";
                if (currentUri.Contains("index.html") || currentUri.Equals("http://cinestream.local/", StringComparison.OrdinalIgnoreCase))
                {
                    string username = Environment.UserName;
                    string displayName = "Watson's Tech Services";
                    string avatarLetter = "W";

                    if (!string.Equals(username, "wats", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(username, "mwats", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(username))
                        {
                            var parts = username.Split(new[] { ' ', '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
                            for (int i = 0; i < parts.Length; i++)
                            {
                                if (parts[i].Length > 0)
                                {
                                    parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1).ToLower();
                                }
                            }
                            displayName = string.Join(" ", parts);
                            if (displayName.Length > 0)
                            {
                                avatarLetter = displayName[0].ToString().ToUpper();
                            }
                        }
                    }

                    // Also change window title to: CineStream - [displayName]
                    this.Title = $"CineStream - {displayName}";

                    // Inject JavaScript to update UI elements
                    string script = $@"
                        (function() {{
                            const usernameEl = document.querySelector('.sidebar-footer .username');
                            if (usernameEl) {{
                                usernameEl.textContent = {JsonSerializer.Serialize(displayName)};
                            }}
                            const avatarEl = document.querySelector('.sidebar-footer .avatar');
                            if (avatarEl) {{
                                avatarEl.textContent = {JsonSerializer.Serialize(avatarLetter)};
                            }}
                        }})();
                    ";
                    await MainWebView.CoreWebView2.ExecuteScriptAsync(script);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CineStream Host] Failed to personalize UI: {ex.Message}");
            }
        }
    }

    private void MainWebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var messageJson = e.TryGetWebMessageAsString();
            System.Diagnostics.Debug.WriteLine($"[CineStream Host] WebMessageReceived: {messageJson}");
            
            if (string.IsNullOrEmpty(messageJson)) 
            {
                System.Diagnostics.Debug.WriteLine("[CineStream Host] Received empty message.");
                return;
            }

            // Defer message processing to the Dispatcher thread to prevent reentrancy and blocking in the WebView2 message pump.
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    // Parse JSON message
                    using var doc = JsonDocument.Parse(messageJson);
                    var message = doc.RootElement;
                    if (!message.TryGetProperty("type", out var typeProp)) 
                    {
                        System.Diagnostics.Debug.WriteLine("[CineStream Host] Message does not contain 'type' property.");
                        return;
                    }

                    var type = typeProp.GetString();
                    System.Diagnostics.Debug.WriteLine($"[CineStream Host] Processing message type: {type}");

                    if (type == "showService")
                    {
                        var name = message.GetProperty("name").GetString() ?? "";
                        var url = message.GetProperty("url").GetString() ?? "";
                        System.Diagnostics.Debug.WriteLine($"[CineStream Host] showService requested: Name={name}, URL={url}");
                        await ShowServiceAsync(name, url);
                    }
                    else if (type == "hideAllServices")
                    {
                        System.Diagnostics.Debug.WriteLine("[CineStream Host] hideAllServices requested.");
                        HideAllServices();
                    }
                    else if (type == "hoverSidebar")
                    {
                        var state = message.GetProperty("state").GetString();
                        System.Diagnostics.Debug.WriteLine($"[CineStream Host] hoverSidebar from MainWebView: {state}");
                        if (_activeServiceName != null)
                        {
                            if (state == "show")
                            {
                                MainWebView.Visibility = Visibility.Visible;
                            }
                            else if (state == "hide")
                            {
                                MainWebView.Visibility = Visibility.Collapsed;
                            }
                        }
                    }
                    else if (type == "openDevTools")
                    {
                        System.Diagnostics.Debug.WriteLine("[CineStream Host] openDevTools requested.");
                        MainWebView.CoreWebView2.OpenDevToolsWindow();
                    }
                    else if (type == "log")
                    {
                        var logMsg = message.GetProperty("message").GetString();
                        System.Diagnostics.Debug.WriteLine($"[JS Log] {logMsg}");
                    }
                    else if (type == "openPopup")
                    {
                        var url = message.GetProperty("url").GetString() ?? "";
                        var partition = message.GetProperty("partition").GetString() ?? "main";
                        partition = partition.Replace("persist:", "");
                        System.Diagnostics.Debug.WriteLine($"[CineStream Host] openPopup requested: URL={url}, Partition={partition}");
                        OpenPopupWindow(url, partition);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error processing web message async: {ex.Message}");
                }
            }));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error reading web message: {ex.Message}");
        }
    }

    private async Task ShowServiceAsync(string name, string url)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[CineStream Host] ShowServiceAsync: Name={name}, URL={url}");
            
            // Hide active service webview if any
            if (_activeServiceName != null && _activeServiceName != name)
            {
                if (_serviceWebViews.TryGetValue(_activeServiceName, out var activeWebView))
                {
                    System.Diagnostics.Debug.WriteLine($"[CineStream Host] Hiding active service: {_activeServiceName}");
                    activeWebView.Visibility = Visibility.Collapsed;
                    activeWebView.CoreWebView2.IsMuted = true;
                    // Pause media in the hidden webview
                    await activeWebView.CoreWebView2.ExecuteScriptAsync(@"
                        (function() {
                            const media = document.querySelectorAll('video, audio');
                            media.forEach(m => {
                                try { m.pause(); } catch(e) {}
                            });
                        })()
                    ");
                }
            }

            _activeServiceName = name;
            ServiceGrid.Visibility = Visibility.Visible;
            Grid.SetColumnSpan(MainWebView, 1);
            MainWebView.Visibility = Visibility.Collapsed; // Hide sidebar initially
            LeftEdgeTrigger.Visibility = Visibility.Visible;
            _hoverTimer?.Start();

            if (!_serviceWebViews.TryGetValue(name, out var webView))
            {
                System.Diagnostics.Debug.WriteLine($"[CineStream Host] Creating new WebView2 for service: {name}");
                
                if (_commonEnvironment == null)
                {
                    throw new InvalidOperationException("WebView2 Environment is not initialized.");
                }

                // Create a new native WebView2 control
                webView = new WebView2
                {
                    Visibility = Visibility.Visible
                };

                // Add it to the ServiceGrid
                ServiceGrid.Children.Add(webView);
                _serviceWebViews[name] = webView;

                // Create controller options with custom ProfileName
                var options = _commonEnvironment.CreateCoreWebView2ControllerOptions();
                options.ProfileName = name; // Netflix, Paramount+, etc. will have isolated storage
                System.Diagnostics.Debug.WriteLine($"[CineStream Host] Created controller options with profile: {options.ProfileName}");

                System.Diagnostics.Debug.WriteLine($"[CineStream Host] Initializing WebView2 controller with shared environment...");
                await webView.EnsureCoreWebView2Async(_commonEnvironment, options);
                System.Diagnostics.Debug.WriteLine($"[CineStream Host] EnsureCoreWebView2Async completed successfully.");

                // Subscribe to messages from guest page
                webView.CoreWebView2.WebMessageReceived += (s, e) =>
                {
                    var msg = e.TryGetWebMessageAsString();
                    if (!string.IsNullOrEmpty(msg))
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            HandleGuestMessage(name, msg);
                        }));
                    }
                };

                // Handle HTML5 Fullscreen requests
                webView.CoreWebView2.ContainsFullScreenElementChanged += (s, e) =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        bool isFullScreen = webView.CoreWebView2.ContainsFullScreenElement;
                        System.Diagnostics.Debug.WriteLine($"[CineStream Host] ContainsFullScreenElementChanged ({name}): {isFullScreen}");
                        ToggleHostFullScreen(isFullScreen);
                    }));
                };

                // Inject webview-preload.js if it exists
                string preloadPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "webview-preload.js");
                if (File.Exists(preloadPath))
                {
                    string preloadScript = await File.ReadAllTextAsync(preloadPath);
                    await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(preloadScript);
                    System.Diagnostics.Debug.WriteLine($"[CineStream Host] Injected guest preload script.");
                }

                // Configure settings
                webView.CoreWebView2.Settings.IsWebMessageEnabled = true;

                // Adjust User Agent
                string defaultUA = webView.CoreWebView2.Settings.UserAgent;
                string cleanUA = defaultUA.Replace("WebView2", "").Replace("wv2", "").Replace("  ", " ");
                webView.CoreWebView2.Settings.UserAgent = cleanUA;
                System.Diagnostics.Debug.WriteLine($"[CineStream Host] Set custom User-Agent: {cleanUA}");

                // Add process failed handler to monitor renderer/GPU crashes
                webView.CoreWebView2.ProcessFailed += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[CineStream Host] Service WebView ({name}) Process Failed: {e.ProcessFailedKind}, Reason: {e.Reason}, ExitCode: {e.ExitCode}");
                };

                // Handle permission requests
                webView.CoreWebView2.PermissionRequested += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[CineStream Host] Permission requested: {e.PermissionKind}");
                    if (e.PermissionKind == CoreWebView2PermissionKind.Microphone ||
                        e.PermissionKind == CoreWebView2PermissionKind.Camera ||
                        e.PermissionKind == CoreWebView2PermissionKind.ClipboardRead)
                    {
                        e.State = CoreWebView2PermissionState.Allow;
                    }
                };

                // Handle New Window requested (popups)
                webView.CoreWebView2.NewWindowRequested += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[CineStream Host] New window requested to: {e.Uri}");
                    e.Handled = true;
                    var uri = e.Uri;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        OpenPopupWindow(uri, name);
                    }));
                };

                System.Diagnostics.Debug.WriteLine($"[CineStream Host] Navigating WebView2 to {url}...");
                webView.CoreWebView2.Navigate(url);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[CineStream Host] Re-showing existing WebView2 for service: {name}");
                webView.Visibility = Visibility.Visible;
                webView.CoreWebView2.IsMuted = false;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error showing service {name}: {ex.Message}\n{ex.StackTrace}", "Service Loading Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void HideAllServices()
    {
        try
        {
            _hoverTimer?.Stop();
            ServiceGrid.Visibility = Visibility.Collapsed;
            _activeServiceName = null;
            Grid.SetColumnSpan(MainWebView, 2);
            MainWebView.Visibility = Visibility.Visible; // Restore sidebar visibility
            LeftEdgeTrigger.Visibility = Visibility.Collapsed;

            // Exit fullscreen state if active
            ToggleHostFullScreen(false);

            foreach (var serviceWebView in _serviceWebViews.Values)
            {
                serviceWebView.Visibility = Visibility.Collapsed;
                serviceWebView.CoreWebView2.IsMuted = true;
                try
                {
                    await serviceWebView.CoreWebView2.ExecuteScriptAsync(@"
                        (function() {
                            const media = document.querySelectorAll('video, audio');
                            media.forEach(m => {
                                try { m.pause(); } catch(e) {}
                            });
                        })()
                    ");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to pause hidden webview: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error hiding services: {ex.Message}");
        }
    }

    private async void OpenPopupWindow(string url, string partitionName)
    {
        try
        {
            if (_commonEnvironment == null)
            {
                throw new InvalidOperationException("WebView2 Environment is not initialized.");
            }

            var popupWindow = new Window
            {
                Title = "Sign In / Authentication",
                Width = 1000,
                Height = 700,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#07050f"))
            };

            var popupWebView = new WebView2();
            popupWindow.Content = popupWebView;
            popupWindow.Show();

            var options = _commonEnvironment.CreateCoreWebView2ControllerOptions();
            options.ProfileName = partitionName; // Keep same profile for the popup!

            await popupWebView.EnsureCoreWebView2Async(_commonEnvironment, options);

            // Inject webview-preload.js
            string preloadPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "webview-preload.js");
            if (File.Exists(preloadPath))
            {
                string preloadScript = await File.ReadAllTextAsync(preloadPath);
                await popupWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(preloadScript);
            }

            // Set User Agent
            string defaultUA = popupWebView.CoreWebView2.Settings.UserAgent;
            string cleanUA = defaultUA.Replace("WebView2", "").Replace("wv2", "").Replace("  ", " ");
            popupWebView.CoreWebView2.Settings.UserAgent = cleanUA;

            popupWebView.CoreWebView2.Navigate(url);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open popup window: {ex.Message}\n{ex.StackTrace}", "Popup Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void HandleGuestMessage(string name, string messageJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(messageJson);
            var message = doc.RootElement;
            if (message.TryGetProperty("type", out var typeProp))
            {
                var type = typeProp.GetString();
                if (type == "hoverSidebar")
                {
                    var state = message.GetProperty("state").GetString();
                    System.Diagnostics.Debug.WriteLine($"[CineStream Host] hoverSidebar from Guest ({name}): {state}");
                    if (_activeServiceName != null)
                    {
                        if (state == "show")
                        {
                            MainWebView.Visibility = Visibility.Visible;
                        }
                        else if (state == "hide")
                        {
                            MainWebView.Visibility = Visibility.Collapsed;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CineStream Host] Error parsing guest web message: {ex.Message}");
        }
    }

    private void ToggleHostFullScreen(bool isFullScreen)
    {
        if (isFullScreen)
        {
            // Save state if we are not already fullscreen
            if (this.WindowStyle != WindowStyle.None)
            {
                _previousWindowStyle = this.WindowStyle;
                _previousWindowState = this.WindowState;
                _previousResizeMode = this.ResizeMode;
            }

            // To force Windows to hide the taskbar and overlay the window,
            // we must temporarily set WindowState to Normal if it is Maximized,
            // change style and resize mode, and then set to Maximized.
            // Setting Topmost = true forces the window above the taskbar.
            this.WindowState = WindowState.Normal;
            this.WindowStyle = WindowStyle.None;
            this.ResizeMode = ResizeMode.NoResize;
            this.Topmost = true;
            this.WindowState = WindowState.Maximized;

            // Hide sidebar completely in fullscreen
            MainWebView.Visibility = Visibility.Collapsed;
            LeftEdgeTrigger.Visibility = Visibility.Collapsed;
        }
        else
        {
            // Restore window style, size, and topmost
            this.Topmost = false;
            this.WindowStyle = _previousWindowStyle;
            this.ResizeMode = _previousResizeMode;
            this.WindowState = _previousWindowState;

            // Keep sidebar collapsed if a service is still active (will show on hover)
            if (_activeServiceName != null)
            {
                MainWebView.Visibility = Visibility.Collapsed;
                LeftEdgeTrigger.Visibility = Visibility.Visible;
            }
            else
            {
                MainWebView.Visibility = Visibility.Visible;
                LeftEdgeTrigger.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void LeftEdgeTrigger_MouseEnter(object sender, MouseEventArgs e)
    {
        if (_activeServiceName != null)
        {
            MainWebView.Visibility = Visibility.Visible;
            LeftEdgeTrigger.Visibility = Visibility.Collapsed;
        }
    }

    private void MainWebView_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_activeServiceName != null)
        {
            MainWebView.Visibility = Visibility.Collapsed;
            LeftEdgeTrigger.Visibility = Visibility.Visible;
        }
    }

    private void HoverTimer_Tick(object? sender, EventArgs e)
    {
        if (_activeServiceName == null) return;

        // Check if our window or one of its child HWNDs (like WebView2) is the foreground window
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        IntPtr myHwnd = helper.Handle;
        IntPtr fgHwnd = GetForegroundWindow();
        bool isWindowActive = (fgHwnd == myHwnd || IsChild(myHwnd, fgHwnd));

        // Skip check if the app window is not active or is minimized
        if (!isWindowActive || this.WindowState == WindowState.Minimized)
        {
            if (MainWebView.Visibility == Visibility.Visible)
            {
                MainWebView.Visibility = Visibility.Collapsed;
                LeftEdgeTrigger.Visibility = Visibility.Visible;
            }
            return;
        }

        // Skip check if the window is in fullscreen mode (to not interrupt video playback layout)
        if (this.WindowStyle == WindowStyle.None)
        {
            return;
        }

        if (GetCursorPos(out var w32Point))
        {
            try
            {
                // Convert screen coordinates to window client coordinates
                Point relativePoint = this.PointFromScreen(new Point(w32Point.X, w32Point.Y));

                // Check vertical bounds first
                if (relativePoint.Y >= 0 && relativePoint.Y <= this.ActualHeight)
                {
                    if (MainWebView.Visibility == Visibility.Visible)
                    {
                        // Sidebar is open (width 250px).
                        // Hide it if the mouse moves out of the sidebar (to the right, i.e., > 250px)
                        // or if the mouse moves out of the window horizontal bounds.
                        if (relativePoint.X > 250 || relativePoint.X < 0)
                        {
                            MainWebView.Visibility = Visibility.Collapsed;
                            LeftEdgeTrigger.Visibility = Visibility.Visible;
                        }
                    }
                    else
                    {
                        // Sidebar is collapsed.
                        // Show it if the mouse is near the left edge (<= 25px) and inside the window horizontal bounds.
                        if (relativePoint.X >= 0 && relativePoint.X <= 25)
                        {
                            MainWebView.Visibility = Visibility.Visible;
                            LeftEdgeTrigger.Visibility = Visibility.Collapsed;
                        }
                    }
                }
                else
                {
                    // Mouse is vertically outside the window. If the sidebar is open, hide it.
                    if (MainWebView.Visibility == Visibility.Visible)
                    {
                        MainWebView.Visibility = Visibility.Collapsed;
                        LeftEdgeTrigger.Visibility = Visibility.Visible;
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // PointFromScreen can throw if the window is not yet fully initialized or is closing
            }
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Win32Point lppoint);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool IsChild(IntPtr hWndParent, IntPtr hWnd);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct Win32Point
    {
        public int X;
        public int Y;
    }
}