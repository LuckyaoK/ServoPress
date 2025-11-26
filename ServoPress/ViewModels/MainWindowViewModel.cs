using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging; 
using HslCommunication.Profinet.Siemens;
using ServoPress.Models; 
using ServoPress.Services;
using System.Collections.Generic; 
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json; 
using System.Text.Json.Serialization; 
using System.Windows;
using System.Windows.Media;

namespace ServoPress.ViewModels
{

    public partial class MainWindowViewModel : ObservableObject
    {
        // 1. 绑定到 Window.WindowState
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsMaximized))]
        private WindowState _windowState = WindowState.Normal;


        public bool IsMaximized => WindowState == WindowState.Maximized;

        // 2. 绑定到一个附加行为，用于触发窗口关闭
        [ObservableProperty]
        private bool _isCloseRequested;

        // ViewModel
        [ObservableProperty]
        private StationViewModel _stationVM;
        public ProductionViewModel ProductionVM { get; }

        // 系统状态文本
        [ObservableProperty]
        private string _systemStatus = "系统初始化";

        // 系统状态颜色
        [ObservableProperty]
        private SolidColorBrush _systemStatusColor = new SolidColorBrush(Colors.Gray);

        //心跳
        private bool _heartbeat;

        //心跳地址
        private string _heartbeatAdd => "DB10.2.0";

        //PLC地址
        private string _s7IPAddress => "127.0.0.1";

        // 触发地址
        private readonly string[] _triggerAddresses = { "DB10.15.0", "DB10.16.0", "DB10.17.0", "DB10.18.0", };

        private  CancellationTokenSource _cts;
        private  CancellationTokenSource _systemStatusCts;

        //服务
        private DataCollectService _dataCollectService;
        private PlcCommunicationService _plcService;
        private readonly CurveBoxService _curveBoxService;
        private readonly DataStorageService _storageService;

        public MainWindowViewModel()
        {
            // 1. 初始化基础服务
            _curveBoxService = new CurveBoxService();
            _plcService = new PlcCommunicationService(_s7IPAddress);
            _dataCollectService = new DataCollectService();
            _storageService = new DataStorageService();
            _storageService.InitializeDatabase();

            // 2. 加载评估窗口配置文件
            _curveBoxService.LoadConfig();

            // 3. 初始化 ProductionVM，并将 service 传递给它
            ProductionVM = new ProductionViewModel(_curveBoxService);

            // 4. 注册保存消息监听
            WeakReferenceMessenger.Default.Register<SaveAllUniboxesMessage>(this, (r, m) =>
            {
                foreach (var station in ProductionVM.Stations)
                {
                    station.SyncDataToService();
                }

                _curveBoxService.SaveConfig();
            });

            //5. 订阅数据采集完成事件
            _dataCollectService.OnDataCollect += OnDataCollectHandler;

            //6. 开启系统状态监听线程
            StartSystemMonitor();

            //7. 开启后台监听线程
            StartPLCLMonitor();

            LogService.Info("应用程序启动");
        }

        private void StartSystemMonitor()
        {
            _systemStatusCts = new CancellationTokenSource();

            // 在后台线程运行，以免阻塞 UI
            Task.Run(() =>
            {

                while (!_systemStatusCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        // 初始连接
                        bool IsPlcConnected = _plcService.ConnectAsync();
                        //监听心跳
                        _heartbeat = _plcService.ReadBool(_heartbeatAdd).Content;
                        if (IsPlcConnected && _heartbeat)
                        {
                             _plcService.WriteBool(_heartbeatAdd, false);
                        }

                        App.Current.Dispatcher.Invoke(() =>
                        {
                            if (IsPlcConnected)
                            {
                                SystemStatus = "运行中";
                                SystemStatusColor = new SolidColorBrush(Colors.LimeGreen);
                            }
                            else
                            {
                            
                                SystemStatus = "PLC连接失败";
                                SystemStatusColor = new SolidColorBrush(Colors.Red);
                            }
                        });

                    }
                    catch (Exception ex)
                    {
                       Debug.WriteLine($"监听错误: {ex.Message}");
                    }
                    
                   Thread.Sleep(500);
                }
            }, _systemStatusCts.Token);
        }


        public void StartPLCLMonitor()
        {
            _cts = new CancellationTokenSource();
            Task.Run(() => PollingLoop(_cts.Token));
        }
        public void StopPLCMonitor()
        {
            _cts?.Cancel();
        }


        /// <summary>
        /// PLC后台监听轮询循环
        /// </summary>
        private async Task PollingLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // 轮询4个工位的触发信号
                    for (int i = 0; i < _triggerAddresses.Length; i++)
                    {
                        string address = _triggerAddresses[i];

                        // 1. 使用新的服务读取 bool 值
                        var readResult =  _plcService.ReadBool(address);
                        if (!readResult.IsSuccess)
                        {
                            Debug.WriteLine($"[PLC Polling] 读取 {address} 失败: {readResult.Message}");
                            await Task.Delay(5000, token); // 发生错误时，等待5秒
                            break; // 退出 for 循环(会触发重连)
                        }

                        if (readResult.Content == true)
                        {
                            int stationId = i + 1;
                            Debug.WriteLine($"[PLC Polling] 检测到工位 {stationId} 触发");
                            _ =_dataCollectService.TriggerCollectAsync(stationId);
                        }

                        //重置
                        //var writeResult = _plcService.WriteBool(address, false);

                    }

                    // 轮询间隔
                    await Task.Delay(100, token); // 100ms 扫描一次
                }
                catch (Exception ex)
                {
                    // 捕获任务取消等异常
                    Debug.WriteLine($"[PlcService] 轮询循环出错: {ex.Message}");
                    if (ex is TaskCanceledException) break;
                    await Task.Delay(1000, token);
                }
            }
            Debug.WriteLine("[PlcService] 轮询已停止。");
        }

        /// <summary>
        /// 结果处理和判定
        /// </summary>
        /// <param name="result"></param>
        private void OnDataCollectHandler(DataResult result)
        {
            try
            {
                var stationVM = ProductionVM.Stations.FirstOrDefault(s => s.Id == result.StationId);
                if (stationVM == null) return;

                // 获取配置
                var windows = _curveBoxService.GetSettingsForStation(result.StationId);
                StringBuilder sb = new StringBuilder();
                bool isAllPassed = true;

                foreach (var box in windows)
                {
                    // 1. 分析几何关系
                    var events = _curveBoxService.AnalyzeCurve(result.CurveData, box);

                    // 2. 判定验证
                    var (boxPassed, boxMessage) = _curveBoxService.VerifyBoxResult(box, events);

                    if (!boxPassed) isAllPassed = false;

                    sb.AppendLine($"[{box.Name}]: {boxMessage}");
                }

                result.EvalWindow = windows;
                result.ResultText = sb.ToString().TrimEnd();
                result.Result = isAllPassed;

                //更新图表
                stationVM.UpdateWithNewData(result);
                Task.Run(() =>
                {
                    try
                    {
                        //数据库存储
                         _storageService.SaveResultAsync(result);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[DB Error] {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindowViewModel] 更新 UI 失败: {ex.Message}");
            }
        }

       
        /// <summary>
        /// 最小化窗口
        /// </summary>
        [RelayCommand]
        private void MinimizeWindow()
        {
            // 触发双向绑定，View 将会收到此变更
            WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// 切换最大化/还原
        /// </summary>
        [RelayCommand]
        private void ToggleMaximizeWindow()
        {
            // 触发双向绑定，View 将会收到此变更
            WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
        }

        /// <summary>
        /// 请求关闭窗口
        /// </summary>
        [RelayCommand]
        private void CloseWindow()
        {
            // 触发附加行为
            IsCloseRequested = true;
            LogService.Info("应用程序关闭");
        }
    }

   
    /// <summary>
    /// 一个空消息，用于从 StationVM 触发 MainWindowVM 的保存操作
    /// </summary>
    public class SaveAllUniboxesMessage { }


    /// <summary>
    /// 定义将要序列化为 JSON 的程序配置的根结构。
    /// </summary>
    public class ProgramConfig
    {
        /// <summary>
        /// 包含所有工位的 Unibox 设置
        /// </summary>
        public Dictionary<string, List<EvalWindow>> StationSettingsDict { get; set; }

    }


    /// <summary>
    /// 为 System.Text.Json 源生成器提供配置。
    /// 这会预编译 ProgramConfig 类的序列化/反序列化逻辑，速度极快。
    /// </summary>
    [JsonSourceGenerationOptions(WriteIndented = true)] // 配置文件写入时带缩进，易于阅读
    [JsonSerializable(typeof(ProgramConfig))] // 告诉生成器我们关心这个类型
    public partial class ConfigJsonContext : JsonSerializerContext
    {

    }
}