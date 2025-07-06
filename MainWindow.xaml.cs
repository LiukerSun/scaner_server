using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ScanerServer.Data;
using ScanerServer.Middleware;
using ScanerServer.Models;

namespace ScanerServer
{
    // 复制按钮内容转换器
    public class CopyButtonContentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? "✓" : "📋";
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        )
        {
            throw new NotImplementedException();
        }
    }

    // 复制按钮背景转换器
    public class CopyButtonBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? Brushes.Gray : Brushes.Green;
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        )
        {
            throw new NotImplementedException();
        }
    }

    // 复制按钮提示转换器
    public class CopyButtonToolTipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? "已复制" : "复制Code";
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        )
        {
            throw new NotImplementedException();
        }
    }

    public partial class MainWindow : Window
    {
        // Windows API P/Invoke declarations
        [DllImport("user32.dll")]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll")]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll")]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll")]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr data);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalFree(IntPtr hMem);

        [DllImport("kernel32.dll")]
        private static extern IntPtr lstrcpy(IntPtr lpString1, string lpString2);

        private const uint CF_UNICODETEXT = 13;
        private const uint GMEM_MOVEABLE = 0x0002;

        private IHost? _webHost;
        private readonly ObservableCollection<ScanerServer.Models.HttpRequest> _requests;
        private bool _isServerRunning = false;
        private readonly Dictionary<int, bool> _copiedCodes = new Dictionary<int, bool>();

        public MainWindow()
        {
            InitializeComponent();
            _requests = new ObservableCollection<ScanerServer.Models.HttpRequest>();
            RequestList.ItemsSource = _requests;

            // 初始化数据库
            InitializeDatabase();

            // 加载历史请求
            LoadHistoricalRequests();
        }

        private void InitializeDatabase()
        {
            try
            {
                using var context = new ApplicationDbContext();
                context.Database.EnsureCreated();
                DatabaseStatusText.Text = "已连接";
                DatabaseStatusText.Foreground = System.Windows.Media.Brushes.Green;
            }
            catch (Exception ex)
            {
                DatabaseStatusText.Text = "连接失败";
                DatabaseStatusText.Foreground = System.Windows.Media.Brushes.Red;
                MessageBox.Show(
                    $"数据库初始化失败: {ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void LoadHistoricalRequests()
        {
            try
            {
                using var context = new ApplicationDbContext();
                var recentRequests = context
                    .HttpRequests.OrderByDescending(r => r.Timestamp)
                    .Take(50)
                    .ToList();

                foreach (var request in recentRequests.AsEnumerable().Reverse())
                {
                    _requests.Add(request);
                    // 根据数据库中的复制状态初始化内存中的状态
                    if (request.IsCopied)
                    {
                        _copiedCodes[request.Id] = true;
                    }
                }

                UpdateRequestCount();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"加载历史请求失败: {ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private async void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isServerRunning)
            {
                await StartServer();
            }
            else
            {
                await StopServer();
            }
        }

        private string GetLocalIPAddress()
        {
            try
            {
                // 获取本机所有网络接口
                var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

                foreach (var network in networkInterfaces)
                {
                    // 只查找以太网和WiFi接口，且状态为Up
                    if (
                        (
                            network.NetworkInterfaceType == NetworkInterfaceType.Ethernet
                            || network.NetworkInterfaceType == NetworkInterfaceType.Wireless80211
                        )
                        && network.OperationalStatus == OperationalStatus.Up
                    )
                    {
                        var properties = network.GetIPProperties();

                        foreach (var address in properties.UnicastAddresses)
                        {
                            // 只返回IPv4地址，且不是回环地址
                            if (
                                address.Address.AddressFamily
                                    == System.Net.Sockets.AddressFamily.InterNetwork
                                && !IPAddress.IsLoopback(address.Address)
                            )
                            {
                                return address.Address.ToString();
                            }
                        }
                    }
                }

                return "127.0.0.1"; // 如果没找到，返回本地回环地址
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        private async Task StartServer()
        {
            try
            {
                if (!int.TryParse(PortTextBox.Text, out int port))
                {
                    MessageBox.Show(
                        "请输入有效的端口号",
                        "错误",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                    return;
                }

                var localIP = GetLocalIPAddress();

                _webHost = Host.CreateDefaultBuilder()
                    .ConfigureWebHostDefaults(webBuilder =>
                    {
                        webBuilder.UseUrls($"http://0.0.0.0:{port}");
                        webBuilder.Configure(app =>
                        {
                            app.UseMiddleware<RequestLoggingMiddleware>(OnRequestReceived);

                            app.Run(async context =>
                            {
                                context.Response.ContentType = "application/json";
                                await context.Response.WriteAsync("{\"message\": \"请求已记录\"}");
                            });
                        });
                    })
                    .Build();

                await _webHost.StartAsync();

                _isServerRunning = true;
                StartStopButton.Content = "停止服务器";
                StatusText.Text = "运行中";
                StatusText.Foreground = System.Windows.Media.Brushes.Green;
                ServerUrlText.Text = $"http://{localIP}:{port}";
                PortTextBox.IsEnabled = false;

                MessageBox.Show(
                    $"服务器已启动在端口 {port}\n局域网访问地址: http://{localIP}:{port}",
                    "成功",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"启动服务器失败: {ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private async Task StopServer()
        {
            try
            {
                if (_webHost != null)
                {
                    await _webHost.StopAsync();
                    _webHost.Dispose();
                    _webHost = null;
                }

                _isServerRunning = false;
                StartStopButton.Content = "启动服务器";
                StatusText.Text = "已停止";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
                PortTextBox.IsEnabled = true;

                MessageBox.Show(
                    "服务器已停止",
                    "信息",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"停止服务器失败: {ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void OnRequestReceived(ScanerServer.Models.HttpRequest request)
        {
            // 在UI线程中更新界面
            Dispatcher.Invoke(() =>
            {
                _requests.Insert(0, request);

                // 保持最多显示100条记录
                while (_requests.Count > 100)
                {
                    _requests.RemoveAt(_requests.Count - 1);
                }

                UpdateRequestCount();
            });
        }

        private void UpdateRequestCount()
        {
            RequestCountText.Text = $" ({_requests.Count})";
        }

        private void CopyCode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string code)
            {
                // 禁用按钮防止重复点击
                button.IsEnabled = false;
                
                try
                {
                    // 使用优化的WPF剪切板复制方法
                    bool copySuccess = TrySetClipboardTextWPF(code);
                    
                    if (copySuccess)
                    {
                        // 获取请求对象
                        var dataContext = button.DataContext as ScanerServer.Models.HttpRequest;
                        if (dataContext != null)
                        {
                            // 标记为已复制
                            _copiedCodes[dataContext.Id] = true;

                            // 更新数据模型中的IsCopied状态，触发UI更新
                            dataContext.IsCopied = true;

                            // 更新数据库中的复制状态
                            UpdateCopiedStatusInDatabase(dataContext.Id);
                        }

                        // 显示简短的成功提示
                        var statusText = StatusText.Text;
                        StatusText.Text = $"已复制: {code}";
                        StatusText.Foreground = System.Windows.Media.Brushes.Green;

                        // 3秒后恢复状态文本
                        var statusTimer = new System.Windows.Threading.DispatcherTimer
                        {
                            Interval = TimeSpan.FromSeconds(3),
                        };
                        statusTimer.Tick += (s, args) =>
                        {
                            StatusText.Text = _isServerRunning ? "运行中" : "未启动";
                            StatusText.Foreground = _isServerRunning
                                ? System.Windows.Media.Brushes.Green
                                : System.Windows.Media.Brushes.Red;
                            statusTimer.Stop();
                        };
                        statusTimer.Start();
                    }
                    else
                    {
                        // 提供手动复制选项
                        var result = MessageBox.Show(
                            $"复制失败：剪切板被其他应用程序占用\n\n文本内容：{code}\n\n是否打开文本以便手动复制？",
                            "剪切板访问失败",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question
                        );
                        
                        if (result == MessageBoxResult.Yes)
                        {
                            // 打开记事本显示文本
                            ShowTextInNotepad(code);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"复制失败: {ex.Message}",
                        "错误",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
                finally
                {
                    // 重新启用按钮
                    button.IsEnabled = true;
                }
            }
        }

        /// <summary>
        /// 尝试设置剪切板文本，带重试机制
        /// </summary>
        /// <param name="text">要复制的文本</param>
        /// <param name="maxRetries">最大重试次数</param>
        /// <returns>是否成功</returns>
        private bool TrySetClipboardText(string text, int maxRetries = 5)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    // 使用STA线程来设置剪切板，这是WPF推荐的方式
                    if (System.Threading.Thread.CurrentThread.GetApartmentState() == System.Threading.ApartmentState.STA)
                    {
                        Clipboard.SetText(text);
                    }
                    else
                    {
                        // 如果不在STA线程，使用Dispatcher
                        bool success = false;
                        Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                Clipboard.SetText(text);
                                success = true;
                            }
                            catch
                            {
                                success = false;
                            }
                        });
                        if (success) return true;
                    }
                    return true;
                }
                catch (ExternalException ex)
                {
                    // 检查是否是剪切板被占用错误
                    if (ex.HResult == -2147221040) // CLIPBRD_E_CANT_OPEN
                    {
                        if (i < maxRetries - 1)
                        {
                            // 使用指数退避策略，等待时间逐渐增加
                            int waitTime = Math.Min(100 * (int)Math.Pow(2, i), 1000);
                            System.Threading.Thread.Sleep(waitTime);
                            continue;
                        }
                    }
                    
                    // 其他错误或重试次数用完
                    System.Diagnostics.Debug.WriteLine($"剪切板访问失败 (尝试 {i + 1}/{maxRetries}): {ex.Message}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"剪切板操作异常: {ex.Message}");
                    break;
                }
            }
            
            return false;
        }

        /// <summary>
        /// 备用剪切板设置方法，使用多种策略
        /// </summary>
        /// <param name="text">要复制的文本</param>
        /// <returns>是否成功</returns>
        private bool TrySetClipboardTextAlternative(string text)
        {
            // 方法1：使用DataObject
            try
            {
                var dataObject = new System.Windows.DataObject();
                dataObject.SetData(DataFormats.Text, text);
                dataObject.SetData(DataFormats.UnicodeText, text);
                
                Clipboard.SetDataObject(dataObject, true);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"备用方法1失败: {ex.Message}");
            }

            // 方法2：使用延迟设置
            try
            {
                System.Threading.Thread.Sleep(500);
                Clipboard.SetText(text);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"备用方法2失败: {ex.Message}");
            }

            // 方法3：使用异步方式
            try
            {
                var task = Task.Run(() =>
                {
                    try
                    {
                        Clipboard.SetText(text);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                });
                
                if (task.Wait(TimeSpan.FromSeconds(2)))
                {
                    return task.Result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"备用方法3失败: {ex.Message}");
            }

            // 方法4：使用Windows API直接操作剪切板
            try
            {
                return SetClipboardTextWithAPI(text);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"备用方法4失败: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// 优化的WPF剪切板复制方法，专门解决WPF剪切板冲突问题
        /// </summary>
        /// <param name="text">要复制的文本</param>
        /// <returns>是否成功</returns>
        private bool TrySetClipboardTextWPF(string text)
        {
            // 方法1：使用Windows API（最可靠，绕过WPF剪切板问题）
            if (SetClipboardTextWithAPI(text))
            {
                return true;
            }

            // 方法2：使用DataObject（WPF推荐方式）
            try
            {
                var dataObject = new DataObject();
                dataObject.SetData(DataFormats.Text, text);
                dataObject.SetData(DataFormats.UnicodeText, text);
                
                // 在UI线程中执行
                bool success = false;
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        Clipboard.SetDataObject(dataObject, true);
                        success = true;
                    }
                    catch
                    {
                        success = false;
                    }
                });
                
                if (success) return true;
            }
            catch
            {
                // 忽略异常，继续尝试其他方法
            }

            // 方法3：延迟后重试
            System.Threading.Thread.Sleep(200);
            try
            {
                bool success = false;
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        Clipboard.SetText(text);
                        success = true;
                    }
                    catch
                    {
                        success = false;
                    }
                });
                
                if (success) return true;
            }
            catch
            {
                // 忽略异常
            }

            return false;
        }

        /// <summary>
        /// 智能剪切板复制方法，自动尝试多种策略
        /// </summary>
        /// <param name="text">要复制的文本</param>
        /// <returns>是否成功</returns>
        private bool TrySetClipboardTextSmart(string text)
        {
            // 策略1：使用Windows API直接操作（最可靠）
            if (SetClipboardTextWithAPI(text))
            {
                return true;
            }

            // 策略2：在UI线程中尝试WPF剪切板
            bool success = false;
            Dispatcher.Invoke(() =>
            {
                success = TrySetClipboardText(text, maxRetries: 2);
            });
            if (success) return true;

            // 策略3：等待后重试WPF剪切板
            System.Threading.Thread.Sleep(300);
            Dispatcher.Invoke(() =>
            {
                success = TrySetClipboardText(text, maxRetries: 2);
            });
            if (success) return true;

            // 策略4：使用备用方法
            if (TrySetClipboardTextAlternative(text))
            {
                return true;
            }

            // 策略5：使用异步方式
            try
            {
                var task = Task.Run(() =>
                {
                    bool result = false;
                    Dispatcher.Invoke(() =>
                    {
                        result = TrySetClipboardText(text, maxRetries: 1);
                    });
                    return result;
                });
                
                if (task.Wait(TimeSpan.FromSeconds(2)))
                {
                    return task.Result;
                }
            }
            catch
            {
                // 忽略异步操作异常
            }

            return false;
        }

        /// <summary>
        /// 在记事本中显示文本，方便用户手动复制
        /// </summary>
        /// <param name="text">要显示的文本</param>
        private void ShowTextInNotepad(string text)
        {
            try
            {
                // 创建临时文件
                string tempFile = Path.GetTempFileName();
                File.WriteAllText(tempFile, text, System.Text.Encoding.UTF8);

                // 使用记事本打开文件
                System.Diagnostics.Process.Start("notepad.exe", tempFile);

                MessageBox.Show(
                    "已打开记事本显示文本内容\n\n请手动复制文本后关闭记事本",
                    "手动复制",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"打开记事本失败: {ex.Message}\n\n请手动复制以下文本：\n\n{text}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        /// <summary>
        /// 使用Windows API直接设置剪切板文本（修正版，使用Marshal.Copy）
        /// </summary>
        /// <param name="text">要复制的文本</param>
        /// <returns>是否成功</returns>
        private bool SetClipboardTextWithAPI(string text)
        {
            IntPtr hGlobal = IntPtr.Zero;
            IntPtr source = IntPtr.Zero;

            try
            {
                if (!OpenClipboard(IntPtr.Zero))
                {
                    return false;
                }

                if (!EmptyClipboard())
                {
                    CloseClipboard();
                    return false;
                }

                // Unicode字符串，末尾要有\0\0
                byte[] bytes = System.Text.Encoding.Unicode.GetBytes(text + "\0");
                hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes.Length);
                if (hGlobal == IntPtr.Zero)
                {
                    CloseClipboard();
                    return false;
                }

                source = GlobalLock(hGlobal);
                if (source == IntPtr.Zero)
                {
                    GlobalFree(hGlobal);
                    CloseClipboard();
                    return false;
                }

                // 用Marshal.Copy写入内存
                Marshal.Copy(bytes, 0, source, bytes.Length);

                GlobalUnlock(hGlobal);

                if (SetClipboardData(CF_UNICODETEXT, hGlobal) == IntPtr.Zero)
                {
                    GlobalFree(hGlobal);
                    CloseClipboard();
                    return false;
                }

                CloseClipboard();
                return true;
            }
            catch
            {
                if (hGlobal != IntPtr.Zero)
                {
                    GlobalFree(hGlobal);
                }
                CloseClipboard();
                return false;
            }
        }

        private void UpdateCopiedStatusInDatabase(int requestId)
        {
            try
            {
                using var context = new ApplicationDbContext();
                var request = context.HttpRequests.FirstOrDefault(r => r.Id == requestId);
                if (request != null)
                {
                    request.IsCopied = true;
                    context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                // 静默处理数据库更新错误，不影响用户体验
                System.Diagnostics.Debug.WriteLine($"更新复制状态失败: {ex.Message}");
            }
        }

        protected override async void OnClosed(EventArgs e)
        {
            if (_isServerRunning)
            {
                await StopServer();
            }
            base.OnClosed(e);
        }
    }
}
