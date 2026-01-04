using System;
using System.Runtime.InteropServices;
using ServoPress.Models; // 确保引用了 MultiStationData 所在的命名空间

namespace ServoPress.Services
{
    /// <summary>
    /// 采集卡硬件控制器
    /// 负责：任务创建、生命周期管理、底层数据读取、错误恢复
    /// </summary>
    public class ArtDAQController:IDisposable
    {

        //// 参数说明
        //// 1. 进水速度 (不可变)
        //private const double _Rate = 10000.0;// 单通道采样率 10kHz (8通道并发时的安全值)

        //// 2. 
        //// 设置为 20秒 的容量，即使软件卡死十几秒也不会报错
        //private const int BufferSize = 200000; // 环形缓冲区大小,设置为采样率的 10-20 倍

        //// 3. 每次取水量(勺子大小)
        //// 每 0.1秒 (100ms) 触发一次回调,每次回调读取的数据块大小 (1000点 = 0.1秒刷新一次)0.1秒的数据量
        //// 这是为了让界面曲线看起来是“实时动画”，而不是“一顿一顿”的
        //private const int PerBlock = 1000;


        // === 配置参数 ===
        private const string DeviceName = "Dev1";
        private const string TaskName = "GlobalMultiStationTask";
        public const double SampleRate = 10000.0; // 10kHz
        public const int PerBlock = 1000;  // 每次回调读取点数
        public const int ChannelCount = 8;        // ai0-ai7
        private const int BufferSize = 200000;    // 缓冲区大小

        // === 内部状态 ===
        private IntPtr _taskHandle = IntPtr.Zero;
        private ArtDAQ.ArtDAQ_EveryNSamplesEventCallbackPtr _callbackDelegate;
        private double[] _rawDataBuffer; // 复用的接收缓冲区
        public bool _isRunning = false;
        private readonly object _lock = new object();

        // === 事件 ===
        // 当硬件产生数据时触发，传出封装好的数据包
        public event Action<MultiStationData> DataReceived;
        public event Action<string> OnError;

        public ArtDAQController()
        {
            // 预分配内存，避免GC压力
            _rawDataBuffer = new double[PerBlock * ChannelCount];

            InitializeInternal();
        }

        /// <summary>
        /// 启动采集任务 (如果已启动则忽略)
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                if (_isRunning && _taskHandle != IntPtr.Zero) return;

                try
                {
                    ArtDAQ.ArtDAQ_StartTask(_taskHandle);
                    Thread.Sleep(800);//延时等待开启
                }
                catch (Exception ex)
                {
                    // 1. 确保清理旧资源
                    ShutdownInternal();

                    // 2. 硬件复位 (解决 -50103 资源占用问题)
                    ArtDAQ.ArtDAQ_ResetDevice(DeviceName);
                    Thread.Sleep(500); // 等待复位完成

                    // 3. 重新初始化任务
                    InitializeInternal();

                    _isRunning = false;
                    LogService.Error($"[ArtDAQ] 致命错误，无法启动采集卡: {ex.Message}");
                }

                _isRunning = true;
            }
        }

        /// <summary>
        /// 停止采集任务
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                if (_taskHandle != IntPtr.Zero)
                {
                    try
                    {
                        ArtDAQ.ArtDAQ_StopTask(_taskHandle);
                        Thread.Sleep(500);
                    }
                    catch (Exception ex)
                    {
                        LogService.Error($"[ArtDAQ] 停止任务异常: {ex.Message}");
                    }
}
                }
                _isRunning = false;
            }
        

        private void InitializeInternal()
        {
            int error = 0;

            // 1. 创建任务 (防御性：处理任务名冲突)
            if (_taskHandle != IntPtr.Zero) ShutdownInternal();

            error = ArtDAQ.ArtDAQ_CreateTask(TaskName, out _taskHandle);
            if (error == -200089) 
            {
                LogService.Debug("[ArtDAQ] 检测到僵尸任务，执行清理...");
                IntPtr oldTask;
                ArtDAQ.ArtDAQ_LoadTask(TaskName, out oldTask);
                ArtDAQ.ArtDAQ_StopTask(oldTask);
                ArtDAQ.ArtDAQ_ClearTask(oldTask);
                Thread.Sleep(300);
                error = ArtDAQ.ArtDAQ_CreateTask(TaskName, out _taskHandle);
            }

            CheckError(error, "CreateTask");

            // 2. 创建通道 (8路)
            string channelString = $"{DeviceName}/ai0:7";
            error = ArtDAQ.ArtDAQ_CreateAIVoltageChan(_taskHandle, channelString, "", ArtDAQ.ArtDAQ_Val_Cfg_Default, -10.0, 10.0, ArtDAQ.ArtDAQ_Val_Volts, null);
            CheckError(error, "CreateAIChan");

            // 3. 配置时钟
            error = ArtDAQ.ArtDAQ_CfgSampClkTiming(_taskHandle, "", SampleRate, ArtDAQ.ArtDAQ_Val_Rising, ArtDAQ.ArtDAQ_Val_ContSamps, BufferSize);
            CheckError(error, "CfgSampClk");

            // 4. 注册回调
            _callbackDelegate = new ArtDAQ.ArtDAQ_EveryNSamplesEventCallbackPtr(EveryNSamplesCallback);
            error = ArtDAQ.ArtDAQ_RegisterEveryNSamplesEvent(_taskHandle, ArtDAQ.ArtDAQ_Val_Acquired_Into_Buffer, (uint)PerBlock, 0, _callbackDelegate, IntPtr.Zero);
            CheckError(error, "RegisterCallback");

            error = ArtDAQ.ArtDAQ_StartTask(_taskHandle);
            CheckError(error, "StartTask");

            _isRunning = true;
            LogService.Info("采集卡初始化成功！");
        }

        /// <summary>
        /// 停止清除任务
        /// </summary>

        private void ShutdownInternal()
        {
            if (_taskHandle != IntPtr.Zero)
            {
                try
                {
                    ArtDAQ.ArtDAQ_StopTask(_taskHandle);
                    ArtDAQ.ArtDAQ_ClearTask(_taskHandle);
                    Thread.Sleep(1000);
                }
                catch (Exception ex)
                {
                    LogService.Error($"[ArtDAQ] 停止任务异常: {ex.Message}");
                }
                finally
                {
                    _taskHandle = IntPtr.Zero;
                }
            }
        }



        // 驱动回调函数
        private int EveryNSamplesCallback(IntPtr taskHandle, int everyNsamplesEventType, uint nSamples, IntPtr callbackData)
        {
            try
            {
                int read = 0;
                // 读取数据
                int error = ArtDAQ.ArtDAQ_ReadAnalogF64(taskHandle, PerBlock, 10.0,
                    ArtDAQ.ArtDAQ_Val_GroupByChannel, _rawDataBuffer, (uint)_rawDataBuffer.Length, out read, IntPtr.Zero);

                if (read > 0 && error == 0)
                {
                    // 封装数据包
                    var dataPackage = new MultiStationData();

                    // 并行拷贝数据 (微小性能优化)
                    Parallel.For(0, ChannelCount, ch =>
                    {
                        double[] channelData = new double[read];
                        int startOffset = ch * read;
                        // 边界检查
                        if (startOffset + read <= _rawDataBuffer.Length)
                        {
                            Array.Copy(_rawDataBuffer, startOffset, channelData, 0, read);
                        }

                        if (ch < 4) dataPackage.Pressures[ch] = channelData;
                        else dataPackage.Displacements[ch - 4] = channelData;
                    });

                    // 触发事件通知上层
                    DataReceived?.Invoke(dataPackage);
                }
                else if (error != 0)
                {
                    // 记录但不抛出，避免回调崩溃
                    CheckError(error, "ReadCallback");
                }
            }
            catch (Exception ex)
            {
                LogService.Error($"[ArtDAQ] 回调处理异常: {ex.Message}");
            }
            return 0;
        }

        private void CheckError(int errorCode, string stepName)
        {
            if (errorCode < 0)
            {
                byte[] errorInfo = new byte[2048];
                ArtDAQ.ArtDAQ_GetExtendedErrorInfo(errorInfo, 2048);
                string msg = System.Text.Encoding.Default.GetString(errorInfo).TrimEnd('\0');
                throw new Exception($"DAQ Error {errorCode} at {stepName}: {msg}");
            }
        }

        public void Dispose()
        {
            ShutdownInternal();

        }
    }
}