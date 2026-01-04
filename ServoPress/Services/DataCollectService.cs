using OxyPlot;
using ServoPress.Database;
using ServoPress.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace ServoPress.Services
{
    /// <summary>
    /// 数据结果类
    /// </summary>
    public class DataResult
    {
        public int StationId { get; set; }  // 触发的工位 ID (0-3)
        public string SerialNumber { get; set; } = "";//产品序列号
        public string ProductType { get; set; } = "510";
        public List<DataPoint> CurveData { get; set; } // 采集到的曲线数据 (位移 vs 压力)
        public double MinPosition { get; set; }
        public double MaxPosition { get; set; }
        public double MinForce { get; set; }
        public double MaxForce { get; set; }
        public double EndPosition { get; set; }
        public double EndForce { get; set; }
        public bool Result { get; set; } // 最终判定结果
        public string ResultText { get; set; } = "";//结果判定文本
        public List<EvalWindow> EvalWindow { get; set; }

        public long StopSignalTimestamp { get; set; }



        /// <summary>
        /// 生成序列号
        /// 格式：产品类型(N位) + 工位ID(2位) + 日期(8位) + 流水号(5位)
        /// 示例：Y29012025112700001
        /// </summary>
        /// <returns>完整的序列号</returns>
        public string GenerateSerialNumber()
        {
            try
            {
                using (var context = new AppDbContext())
                {
                    // 1. 获取当前日期格式
                    string dateStr = DateTime.Now.ToString("yyyyMMdd");

                    // 2. 拼接前缀 (用于在数据库中模糊查询) 
                    string prefix = $"{ProductType}{(StationId+1):D2}{dateStr}";

                    // 3. 查询数据库中，今天生成的最后一个包含该前缀的序列号
                    var lastRecord = context.ProductionRecords
                        .Where(r => r.SerialNumber != null && r.SerialNumber.StartsWith(prefix))
                        .OrderByDescending(r => r.SerialNumber)
                        .FirstOrDefault();

                    int currentSequence = 0;

                    if (lastRecord != null)
                    {
                        // 4. 解析已有序号的最后5位
                        string snStr = lastRecord.SerialNumber;
                        if (snStr.Length >= 5)
                        {
                            string seqPart = snStr.Substring(snStr.Length - 5);
                            int.TryParse(seqPart, out currentSequence);
                        }
                    }

                    // 5. 序号 + 1
                    int nextSequence = currentSequence + 1;

                    // 6. 格式化为5位字符串 (00001)
                    string sequenceStr = nextSequence.ToString("D5");

                    // 7. 组合最终结果
                    string fullSerialNumber = $"{prefix}{sequenceStr}";

                    LogService.Info($"生成新序列号: {fullSerialNumber}");
                    return fullSerialNumber;
                }
            }
            catch (Exception ex)
            {
                LogService.Error(ex, "生成序列号失败");
                return "";

            }
        }

    }


    /// <summary>
    /// 多工位实时数据包类
    /// </summary>
    public class MultiStationData
    {
        public double[][] Pressures { get; set; } = new double[4][];     // ai0-ai3
        public double[][] Displacements { get; set; } = new double[4][]; // ai4-ai7
    }



    /// <summary>
    /// 数据采集服务类(单例)
    /// </summary>
    public class DataCollectService
    {
        // 并发管理
        private int _activeStationCount = 0;
        private readonly object _hardwareLock = new object();
        private readonly ConcurrentDictionary<int, byte> _busyStations = new ConcurrentDictionary<int, byte>();

        //服务
        private readonly PlcCommunicationService _plcService;
        private readonly ArtDAQController _daqController;

        // 停止采集触发地址
        private readonly string[] _stopTriggers = { "DB10.4.3", "DB10.4.5", "DB10.4.7", "DB10.5.1", };
        // 压装开始采集完成地址
        private readonly string[] _startONAddresses = { "DB10.12.2", "DB10.12.4", "DB10.12.6", "DB10.13.0", };

        //采集完毕事件
        public event Action<DataResult> OnDataCollect;

        // 当硬件回调产生数据时触发事件
        private event Action<MultiStationData> OnInternalDataReceived;


        public DataCollectService(PlcCommunicationService plcService, ArtDAQController dAQController)
        {
            _plcService = plcService;


            // 初始化硬件控制器 
            _daqController = dAQController;

            // 订阅硬件数据
            _daqController.DataReceived += (data) =>
            {
                // 转发给内部监听者
                OnInternalDataReceived?.Invoke(data);
            };
        }

        public async Task TriggerCollectAsync(int stationId)
        {
            // 尝试将该工位标记为忙碌
            // TryAdd 是原子操作：如果添加成功返回 true（抢到锁了）；如果已存在返回 false（正在忙）
            // await Task.Delay(1000) 的作用是“霸占着锁不放”。它强行延长了任务的生命周期，让锁多存在一会儿。
            //_busyStations 的作用是“检查锁是否存在”。
            if (!_busyStations.TryAdd(stationId, 0))
            {
                //LogService.Info($"[DataAcquisition] 工位 {stationId + 1} 正在采集中，忽略本次触发。");
                return;
            }
            LogService.Info($"[DataAcquisitionService] 工位 {stationId + 1}采集流程开始");


            // 1. 发送PLC开始采集完成信号
            _plcService.WriteBool(_startONAddresses[stationId], true);

         
            // 3. 获取停止地址
            string stopAddress = _stopTriggers[stationId];

            // 4. 调用异步采集方法
            var result = await CollectStationDataAsync(stationId,
                () => _plcService.ReadBool(stopAddress).Content // 传入检查逻辑
            );

            // 5. 触发完成事件
            OnDataCollect?.Invoke(result);

            // 6. 清理状态，延时等待PLC信号复位，防止重复触发
            await Task.Delay(1000);

            // 7. 移除锁
            _busyStations.TryRemove(stationId, out _);

        }

        /// <summary>
        /// 【核心方法】异步采集单个工位的数据
        /// </summary>
        /// <param name="stationId">工位号 (0-3)</param>
        /// <param name="shouldStopFunc">停止条件检查函数 (传入 () => plc.Read(StopAddr))</param>
        /// <param name="timeoutSeconds">最大超时时间</param>
        /// <returns>该工位的采集结果</returns>
        public async Task<DataResult> CollectStationDataAsync(int stationId, Func<bool> shouldStopFunc, int timeoutSeconds = 15)
        {

            //// 增加引用计数并启动硬件
            //ManageHardwareStart();

            if (!_daqController._isRunning)
            {
                LogService.Error($"工位 {stationId + 1} 无法启动硬件，采集终止。");
                return new DataResult { StationId = stationId, Result = false, ResultText = "硬件启动失败" };
            }

            var result = new DataResult { StationId = stationId };
            var localCurve = new List<DataPoint>();

            // 零点电压基准值
            // 初始化为 NaN，表示尚未进行归零
            double _zeroDispVol = double.NaN;
            double _zeroPressVol = double.NaN;


            // 定义数据接收处理回调 (只提取当前 stationId 的数据)
            Action<MultiStationData> dataHandler = (allData) =>
            {
                // 获取当前工位的压力和位移数据数组
                double[] pressures = allData.Pressures[stationId];
                double[] displacements = allData.Displacements[stationId];

                // 确保两个数组都不为空且长度一致（通常是一致的，取决于 PerBlockSize）
                if (pressures != null && displacements != null)
                {
                    int count = Math.Min(pressures.Length, displacements.Length);
                    
                    // 只要 _zeroDispVol 是 NaN，就说明是刚开始采集的前几毫秒数据。
                    // 我们计算这批数据的平均值，作为本次压装的“绝对零点”。
                    if (double.IsNaN(_zeroDispVol) && count > 0)
                    {
                        double sumDisp = 0;
                        double sumPress = 0;
                        // 取前50个点（约5ms）来计算平均值，消除底噪跳动
                        int avgCount = Math.Min(count, 50);
                        for (int k = 0; k < avgCount; k++)
                        {
                            sumDisp += displacements[k];
                            sumPress += pressures[k];
                        }
                        _zeroDispVol = sumDisp / avgCount;
                        _zeroPressVol = sumPress / avgCount;

                    }


                    lock (localCurve) // 保护 List 线程安全
                    {
                        // 遍历这一批次的所有点，全部添加到曲线中
                        for (int i = 0; i < count; i++)
                        {
                            // -----------------------------------------------------
                            // 物理量换算公式： (当前电压 - 零点基准电压) / 量程 * 物理满量程
                            // -----------------------------------------------------

                            //换算 (模拟量电压/量程10V) * 50mm
                            double disPlace = (displacements[i] - _zeroDispVol) / 10 * 50;

                            //换算 (模拟量电压/量程10V) * 500kg(500 * 9.8N)
                            double pre = (pressures[i]-_zeroPressVol) / 10 * (9.8 * 500);

                            if (disPlace >= 0 && pre >= 0)
                            {
                                localCurve.Add(new DataPoint(disPlace, pre));
                            }
                        }
                    }
                }
            };

            // 3. 订阅数据流
            OnInternalDataReceived += dataHandler;

            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                TimeSpan maxDuration = TimeSpan.FromSeconds(timeoutSeconds);

                while (sw.Elapsed < maxDuration)
                {
                    // 检查 PLC 停止信号
                    if (shouldStopFunc())
                    {
                        result.StopSignalTimestamp = Stopwatch.GetTimestamp();//获取停止信号时间戳
                        LogService.Info($"PLC触发工位{stationId+1}停止采集");
                        break;
                    }

                    // 避免死循环占用 CPU
                    await Task.Delay(20);
                }
            }
            catch (Exception ex)
            {
                LogService.Error($"工位 {stationId} 采集流程异常: {ex.Message}");
            }
            finally
            {
                // 5. 停止接收数据
                OnInternalDataReceived -= dataHandler;

                //// 6. 减少引用计数并停止硬件 (如果是最后一个任务)
                //ManageHardwareStop();
            }

            // 7. 整理返回结果
            lock (localCurve)
            {
                //  -- 处理数据点--  //
                result.CurveData = localCurve
                    .SmoothMovingAverage(200) //平滑：窗口300 (去噪)
                    .DownsampleMinMax(800);     // 抽稀：目标1600点 (防卡顿)

            }

            if (result.CurveData.Any())
            {
                result.MinPosition = result.CurveData.Min(p => p.X);
                result.MaxPosition = result.CurveData.Max(p => p.X);
                result.EndPosition = result.CurveData.Last().X;

                result.MinForce = result.CurveData.Min(p => p.Y);
                result.MaxForce = result.CurveData.Max(p => p.Y);
                result.EndForce = result.CurveData.Last().Y;
            }

            return result;
        }


        //private void ManageHardwareStart()
        //{
        //    lock (_hardwareLock)
        //    {
        //        _activeStationCount++;
        //        if (_activeStationCount == 1)
        //        {
        //            _daqController.Start(); 
        //        }
        //    }
        //}

        //private void ManageHardwareStop()
        //{
        //    lock (_hardwareLock)
        //    {
        //        _activeStationCount--;
        //        if (_activeStationCount <= 0)
        //        {
        //            _activeStationCount = 0;
        //            _daqController.Stop(); 
        //        }
        //    }
        //}



        #region 旧模拟测试逻辑
        private DataResult CollectFromControlCard(int stationId)
        {
            // TODO:真实网口通信代码
            // 1. (连接到控制卡 Socket)
            // 2. (发送开始采集命令)
            // 3. (循环接收数据... 假设收到了两组数据：位移和压力)
            // 4. (断开连接)

            string stopAddress = _stopTriggers[stationId];
            LogService.Info($"工位 {stationId + 1} 开始采集数据...");

            List<double> times = new List<double>();
            List<double> positions = new List<double>();
            List<double> forces = new List<double>();

            Stopwatch sw = Stopwatch.StartNew();

            // 安全超时设置 (例如最长采集60秒)，防止死循环
            TimeSpan maxDuration = TimeSpan.FromSeconds(60);

            // 曲线数据
            var curve = new List<DataPoint>();
            float pos = 0, force = 0;

            while (sw.Elapsed < maxDuration)
            {
                bool shouldStop = _plcService.ReadBool(stopAddress).Content;
                if (shouldStop)
                {
                    LogService.Info($"工位 {stationId + 1} 收到PLC停止信号 ({stopAddress})，停止采集。");
                    break;
                }



                //var r = new Random();
                //for (double pos = 0; pos <= 10; pos += 0.1)
                //{
                //    force += (r.NextDouble() - 0.4);
                //    if (pos > 5) force += (r.NextDouble() - 0.2); // 模拟压力上升
                //    if (force < 0) force = 0;
                //    curve.Add(new DataPoint(pos, force));
                //}
                //curve.Add(new DataPoint(pos, force));
                // 5. 采样频率控制 (例如 10ms 一次)
                //Thread.Sleep(10);
            }

            double maxForce = curve.Max(p => p.Y);
            double EndForce = curve.LastOrDefault().Y;
            double EndPos = curve.LastOrDefault().X;

            return new DataResult
            {
                StationId = stationId,
                CurveData = curve,
                MinPosition = 0.0,
                MaxPosition = 10.0,
                EndPosition = EndPos,
                MinForce = 0.0,
                MaxForce = maxForce,
                EndForce = EndForce
            };

        }

        #endregion



    }
}