using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
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
                try
                {
                    Clipboard.SetText(code);

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
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"复制失败: {ex.Message}",
                        "错误",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
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
