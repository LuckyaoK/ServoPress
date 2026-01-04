using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ServoPress.Models;
using ServoPress.Services;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;
using NLog.Targets;

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
        // 将ProductionVM 改为 ObservableProperty，以便初始化完成后通知 UI 更新
        [ObservableProperty]
        private ProductionViewModel _productionVM;

        //用于控制界面显示的 Loading 状态（可选，界面可以绑定此属性显示加载动画）
        [ObservableProperty]
        private bool _isLoading = true;

        // 系统状态文本
        [ObservableProperty]
        private string _systemStatus = "系统初始化";


        // 系统状态文本
        [ObservableProperty]
        private string _productType = "510";

        // 系统状态颜色
        [ObservableProperty]
        private SolidColorBrush _systemStatusColor = new SolidColorBrush(Colors.Gray);

        // 锁对象，用于保护日志队列的并发访问
        private readonly object _logLock = new object();

        // 锁对象，用于保护结果处理逻辑的并发访问 (PLC写入、数据判定等)
        private readonly object _processingLock = new object();

        // 日志内容
        [ObservableProperty]
        private string _logContent = "";

        // 日志队列，用于精确管理行数
        private readonly Queue<string> _logQueue = new Queue<string>();

        // 最大日志行数限制
        private const int MaxLogLines = 300;

        //心跳
        private bool _heartbeat;

        //心跳地址
        private string _heartbeatAdd => "DB10.2.0";

        //PLC地址
        private string _s7IPAddress => "192.168.0.10";

        // 压装开始采集触发地址
        private readonly string[] _triggerAddresses = { "DB10.4.2", "DB10.4.4", "DB10.4.6", "DB10.5.0", };

        // 压装结束完成信号地址
        private readonly string[] _finishedAddresses = { "DB10.12.3", "DB10.12.5", "DB10.12.7", "DB10.13.1", };

        // 压装结果信号地址
        private readonly string[] _resultAddresses = { "DB10.20.0", "DB10.22.0", "DB10.24.0", "DB10.26.0", };

        // 压装结果屏蔽地址
        private readonly string[] _pingBiAddresses = { "DB10.8.2", "DB10.8.3", "DB10.8.4", "DB10.8.5", };


        private CancellationTokenSource _cts;
        private CancellationTokenSource _systemStatusCts;

        //服务
        private DataCollectService _dataCollectService;
        private PlcCommunicationService _plcService;
        private CurveBoxService _curveBoxService;
        private DataStorageService _storageService;
        private ArtDAQController _daqController;

        public MainWindowViewModel()
        {

            // 设置初始状态
            SystemStatus = "系统启动中...";
            SystemStatusColor = new SolidColorBrush(Colors.Yellow);

            // 开启后台任务进行初始化，释放 UI 线程
            Task.Run(async () =>
            {
                await InitializeAppAsync();
            });
            LogService.Info("应用程序启动");
        }


        // 异步初始化逻辑
        private async Task InitializeAppAsync()
        {
            try
            {
                // 初始化服务对象
                _curveBoxService = new CurveBoxService();//判定框
                _plcService = new PlcCommunicationService(_s7IPAddress);//PLC
                _daqController = new ArtDAQController();//采集卡
                _dataCollectService = new DataCollectService(_plcService, _daqController);//数据分析
                _storageService = new DataStorageService();//数据存储

                //日志订阅
                LogService.OnNewLog += OnNewLogReceived;
              
                // 数据库初始化 (耗时)
                _storageService.InitializeDatabase();

                // 加载配置文件 (IO耗时)
                _curveBoxService.LoadConfig();

                // 创建子 ViewModel (如果它构造函数不耗时)
                var prodVM = new ProductionViewModel(_curveBoxService, _storageService);

                // 回到 UI 线程更新界面 ---
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 赋值 ViewModel，触发界面绑定更新
                    ProductionVM = prodVM;

                    // 注册消息监听 (必须在 ProductionVM 创建后)
                    RegisterMessages();

                    // 订阅采集完成处理事件
                    _dataCollectService.OnDataCollect += OnDataCollectHandler;


                    // 启动监控线程
                    StartSystemMonitor();
                    StartPLCLMonitor();

                    // 更新状态
                    IsLoading = false;
                    SystemStatus = "系统就绪";
                    LogService.Info("应用程序启动完成");
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    SystemStatus = "初始化失败";
                    SystemStatusColor = new SolidColorBrush(Colors.Red);
                    MessageBox.Show($"启动失败: {ex.Message}");
                });
            }
        }

        private void RegisterMessages()
        {
            WeakReferenceMessenger.Default.Register<SaveAllUniboxesMessage>(this, (r, m) =>
            {
                // 注意判空，防止初始化未完成时触发
                if (ProductionVM != null)
                {
                    foreach (var station in ProductionVM.Stations)
                    {
                        station.SyncDataToService();
                    }
                    _curveBoxService.SaveConfig();
                }
            });
        }

        /// <summary>
        /// 监听系统运行状态
        /// </summary>
        private void StartSystemMonitor()
        {
            _systemStatusCts = new CancellationTokenSource();
           
            // 在后台线程运行，以免阻塞 UI

            Task.Run(async () =>
            {
                using (var ping = new Ping())
                {
                    while (!_systemStatusCts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            // 发送 Ping 请求，超时设置 1000ms
                            PingReply reply = await ping.SendPingAsync(_s7IPAddress, 1000);

                            bool IsPlcConnected = (reply.Status == IPStatus.Success);

                            App.Current.Dispatcher.Invoke(() =>
                            {
                                if (IsPlcConnected)
                                {
                                    SystemStatus = "运行中"; 
                                    SystemStatusColor = new SolidColorBrush(Colors.LimeGreen);
                                }
                                else
                                {
                                    SystemStatus = "PLC网络中断";
                                    SystemStatusColor = new SolidColorBrush(Colors.Red);
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                          
                            LogService.Error($"监听错误: {ex.Message}");
                        }

                        await Task.Delay(2000, _systemStatusCts.Token);
                    }
                }
            }, _systemStatusCts.Token);

        }

        /// <summary>
        /// 监听PLC触发信号
        /// </summary>
        public void StartPLCLMonitor()
        {
            _cts = new CancellationTokenSource();
            Task.Run(() => PollingLoop(_cts.Token));
        }

        public void StopPLCMonitor()
        {
            _cts?.Cancel();
        }


        private void OnNewLogReceived(string message)
        {
            lock (_logLock)
            {
                _logQueue.Enqueue(message);

                while (_logQueue.Count > MaxLogLines)
                {
                    _logQueue.Dequeue();
                }

                // 为了避免频繁刷新 UI，这里只拼接字符串
                // string.Join 本身会遍历队列，所以必须在 lock 内部
                string newLogContent = string.Join(Environment.NewLine, _logQueue);

           
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    LogContent = newLogContent;
                });
            }
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
                        var readResult = _plcService.ReadBool(address);
                        if (!readResult.IsSuccess)
                        {
                            LogService.Error($"[PLC Polling] 读取 {address} 失败: {readResult.Message}");
                            await Task.Delay(5000, token); // 发生错误时，等待5秒
                            break; // 退出 for 循环(会触发重连)
                        }

                        if (readResult.Content == true)
                        {
                           
                            LogService.Info($"[PLC Polling] 检测到工位 {i+1} 触发");
                            _ = _dataCollectService.TriggerCollectAsync(i);
                        }

                    }

                    // 轮询间隔
                    await Task.Delay(100, token); // 100ms 扫描周期
                }
                catch (Exception ex)
                {
                    // 捕获任务取消等异常
                    LogService.Info($"[PlcService] 轮询循环出错: {ex.Message}");
                    if (ex is TaskCanceledException) break;
                    await Task.Delay(1000, token);
                }
            }
            LogService.Info("[PlcService] 轮询已停止。");
        }

        /// <summary>
        /// 结果处理和判定
        /// </summary>
        /// <param name="result"></param>
        private void OnDataCollectHandler(DataResult result)
        {
            // 加锁：防止多个工位同时完成时发生资源竞争（如同时写PLC、写数据库、更新UI等）
            lock (_processingLock)
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

                    result.SerialNumber = result.GenerateSerialNumber();//生成产品序列号
                    result.EvalWindow = windows;

                    bool pingbi = _plcService.ReadBool(_pingBiAddresses[result.StationId]).Content;

                    if (!pingbi)
                    {
                        result.ResultText = sb.ToString().TrimEnd();
                        result.Result = isAllPassed;
                    }
                    else
                    {
                        result.ResultText = "压装工站屏蔽中";
                        result.Result = true;
                    }

                    //先写入PLC结果
                    short ResEnd = (short)(result.Result ? 99 : 1);
                    LogService.Info($"工位{result.StationId + 1} 写入PLC{_resultAddresses[result.StationId]}结果 ->{ResEnd}");
                    _plcService.WriteInt16(_resultAddresses[result.StationId], ResEnd);


                    //后写入PLC完成信号
                    _plcService.WriteBool(_finishedAddresses[result.StationId], true);

                    if (result.StopSignalTimestamp > 0)
                    {
                        long currentTimestamp = Stopwatch.GetTimestamp();
                        // 计算时间差 (Ticks -> 毫秒)
                        double elapsedMs = (currentTimestamp - result.StopSignalTimestamp) * 1000.0 / Stopwatch.Frequency;

                        LogService.Info($"【性能监控】工位{result.StationId + 1} 收到停止信号 -> 写完PLC结果 流程耗时: {elapsedMs:F2} ms");
                    }


                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        //更新图表
                        stationVM.UpdateWithNewData(result);

                    });

                    //数据库存储
                    _storageService.SaveResultAsync(result);

                }
                catch (Exception ex)
                {
                    LogService.Error($"压装结果采集判定失败: {ex.Message}");

                    //结果
                    short ResEnd = 1;
                    _plcService.WriteInt16(_resultAddresses[result.StationId], ResEnd);


                    //采集完成信号
                    _plcService.WriteBool(_finishedAddresses[result.StationId], true);

                }
            }

        }


        #region 窗口化处理
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
        #endregion
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