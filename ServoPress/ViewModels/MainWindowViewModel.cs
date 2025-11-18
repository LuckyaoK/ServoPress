using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging; // 1. 引入消息
using HslCommunication.Profinet.Siemens;
using ServoPress.Models; // 2. 引入 ProgramConfig
using ServoPress.Services;
using System.Collections.Generic; // 3. 引入 List
using System.Diagnostics;
using System.IO; // 4. 引入 IO
using System.Linq; // 5. 引入 Linq
using System.Text.Json; // 6. 引入 JsonSerializer
using System.Text.Json.Serialization; // 7. 引入 Json 源生成
using System.Windows;

namespace ServoPress.ViewModels
{
    /// <summary>
    /// MainWindow 的 ViewModel，负责处理窗口状态和命令
    /// </summary>
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

      
        /// <summary>
        /// 当前加载的程序号 (用于命名配置文件)
        /// </summary>
        [ObservableProperty]
        private string _currentProgram = "MP101_PartA";

        // 触发地址
        private readonly string[] _triggerAddresses = { "DB10.15.0" };
        /// <summary>
        /// 生产页面的 ViewModel 实例
        /// </summary>
        public ProductionViewModel ProductionVM { get; }


        private CancellationTokenSource _cts;
        private DataCollectService _dataCollectService;
        private PlcCommunicationService _plcService;



        public MainWindowViewModel()
        {
            // 1. 创建并持有 ProductionVM 实例
            ProductionVM = new ProductionViewModel();

            // 2. 注册消息：当任何一个 StationVM 发送 "SaveAllUniboxesMessage" 时，
            //    调用 OnSaveAllUniboxes 方法
            WeakReferenceMessenger.Default.Register<SaveAllUniboxesMessage>(this, (r, m) =>
            {
                OnSaveAllUniboxes();
            });


            // 3. 初始化服务
            _dataCollectService = new DataCollectService();
            // (修改) 实例化新的服务
            _plcService = new PlcCommunicationService("192.168.1.10");

            // 4. 订阅数据采集完成事件
            _dataCollectService.OnDataAcquired += OnDataCollectHandler;

            Start();
        }

        private void OnDataCollectHandler(DataResult result)
        {
            try
            {
                // 1. 根据 ID 找到对应的工位 ViewModel
                var stationVM = ProductionVM.Stations.FirstOrDefault(s => s.Id == result.StationId);
                if (stationVM == null) return;

                // 2. [关键] 将数据封送到 UI 线程
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 3. 在 UI 线程上更新 ViewModel
                    stationVM.UpdateWithNewData(result);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindowViewModel] 更新 UI 失败: {ex.Message}");
            }
        }


        /// <summary>
        /// 启动服务
        /// </summary>
        public void Start()
        {
            Task.Run(() => PollingLoop(_cts.Token));
        }


        /// <summary>
        /// 后台轮询循环
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
                            break; // 退出 for 循环，重新开始外层 while 循环 (会触发重连)
                        }

                        if (readResult.Content == true)
                        {
                            int stationId = i + 1;
                            Debug.WriteLine($"[PLC Polling] 检测到工位 {stationId} 触发");
                            _ = _dataCollectService.TriggerCollectAsync(stationId);
                        }
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
        /// 执行保存的业务逻辑
        /// </summary>
        private void OnSaveAllUniboxes()
        {
            // 1. 从 ProductionVM 中收集所有4个工位的 Unibox 设置
            var allSettings = ProductionVM.Stations.Select(s => s.UniboxSettings).ToList();

            // 2. 创建要序列化的配置对象
            var config = new ProgramConfig
            {
                UniboxSettings = allSettings
            };

            string configDir = Path.Combine(AppContext.BaseDirectory, "Product");
            string configPath = Path.Combine(configDir, $"{CurrentProgram}.json");

            try
            {
                Directory.CreateDirectory(configDir);
                string json = JsonSerializer.Serialize(config, ConfigJsonContext.Default.ProgramConfig);
                File.WriteAllText(configPath, json);
                MessageBox.Show($"程序 {CurrentProgram} 的 Unibox 设置已保存到:\n{configPath}", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
        public List<EvalWindow> UniboxSettings { get; set; }

        // 未来可以在此添加其他需要保存的程序特定设置
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