using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging; // 1. 引入消息
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

      
        [ObservableProperty]
        private string _currentProgram = "MP101_PartA";

        // 公开 StationViewModel 供 View 绑定
        [ObservableProperty]
        private StationViewModel _stationVM;

        // 触发地址
        private readonly string[] _triggerAddresses = { "DB10.15.0", "DB10.16.0", "DB10.17.0", "DB10.18.0", };

        public ProductionViewModel ProductionVM { get; }


        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private DataCollectService _dataCollectService;
        private PlcCommunicationService _plcService;


        private readonly CurveBoxService _curveBoxService;


        public MainWindowViewModel()
        {
            // 1. 初始化基础服务
            _curveBoxService = new CurveBoxService();

            // 2. 加载数据 (此时 _curveBoxService.EvalWindows 被填充)
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

            // (修改) 实例化新的服务
            _plcService = new PlcCommunicationService("127.0.0.1");

            // 5. 初始化其他服务
            _dataCollectService = new DataCollectService();
            // 6. 订阅数据采集完成事件
            _dataCollectService.OnDataCollect += OnDataCollectHandler;
            StartPLCListen();
        }


        /// <summary>
        /// 启动服务
        /// </summary>
        public void StartPLCListen()
        {
            Task.Run(() => PollingLoop(_cts.Token));
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
                        var writeResult = _plcService.WriteBool(address, false);

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

                // 判定结果逻辑根据某个工站的所有评估窗口进行判定
                var windows = _curveBoxService.StationSettings[result.StationId];
                StringBuilder sb = new StringBuilder();
                foreach (var box in windows)
                {
                    double width = Math.Abs(box.EndX - box.StartX);
                    double height = Math.Abs(box.EndY - box.StartY);

                    var events = _curveBoxService.AnalyzeCurve(result.CurveData, box.StartX, box.StartY, width, height);
                    if (events.Count > 0)
                    {
                        Debug.WriteLine($"=>\n曲线进入方向{events[0].Side}{events[0].Point}\n");
                        Debug.WriteLine($"=>\n曲线退出方向{events[events.Count - 1].Side}{events[events.Count - 1].Point}\n");

                        BoxSide inBoxSide = new BoxSide();
                        switch (box.EntryDirection)
                        {
                            case "上进":
                                inBoxSide = BoxSide.Top; break;
                            case "下进":
                                inBoxSide = BoxSide.Bottom; break;
                            case "左进":
                                inBoxSide = BoxSide.Left; break;
                            case "右进":
                                inBoxSide = BoxSide.Right; break;
                        }

                        BoxSide outBoxSide = new BoxSide();
                        switch (box.ExitDirection)
                        {
                            case "上出":
                                outBoxSide = BoxSide.Top; break;
                            case "下出":
                                outBoxSide = BoxSide.Bottom; break;
                            case "左出":
                                outBoxSide = BoxSide.Left; break;
                            case "右出":
                                outBoxSide = BoxSide.Right; break;
                            case "不出":
                                outBoxSide = BoxSide.None; break;
                        }

                        bool InResult = true, OutResult = true;
                        // 1、曲线进出入方向和Unibox设置方向要一致
                        if (inBoxSide != events[0].Side)
                        {
                            InResult = false;
                        }
                        if (outBoxSide != events[events.Count - 1].Side)
                        {
                            OutResult = false;
                        }


                        bool InRepeat = false,OutRepeat = false,Touch=false;
                        for (int j = 1; j < events.Count - 1; j++)
                        {
                            // 2、不能触碰没有设置的边界
                            if (events[j].Side != inBoxSide && events[j].Side != outBoxSide)
                            {
                                Touch = true;
                            }

                            // 3、进入方向重复
                            if (events[j].Side == inBoxSide)
                            {
                                InRepeat = true;
                            }

                            // 4、退出方向重复
                            if (events[j].Side == outBoxSide)
                            {
                                OutRepeat = true;
                            }

                        }

                        //允许重进入
                        if (box.AllowReentry)
                        {
                            if (InResult)
                            {
                                sb.Append("进入方向判定OK\n");
                            }
                            else
                            {
                                sb.Append("进入方向判定NG\n");
                            }
                        }

                        else
                        {
                            if (InResult && !InRepeat)
                            {
                                sb.Append("进入方向判定OK\n");
                            }
                            else if (InResult && InRepeat)
                            {
                                sb.Append("重复进入,进入方向判定NG\n");
                            }
                            else
                            {
                                sb.Append("进入方向判定NG\n");
                            }
                        }


                        if (OutResult && !OutRepeat)
                        {
                            sb.Append("退出方向判定OK");
                        }
                        else if (OutResult && OutRepeat)
                        {
                            sb.Append("重复退出,退出方向判定NG");
                        }
                        else
                        {
                            sb.Append("退出方向判定NG");
                        }
                    }
                }

                result.ResultText = sb.ToString();
                if (!result.ResultText.Contains("NG"))
                    result.Result = true;

                stationVM.UpdateWithNewData(result);
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