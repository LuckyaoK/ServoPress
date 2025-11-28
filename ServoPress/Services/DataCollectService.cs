using NLog.Fluent;
using OxyPlot;
using ServoPress.Database;
using ServoPress.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ServoPress.Services
{
    /// <summary>
    /// 数据结果类
    /// </summary>
    public class DataResult
    {
        public int StationId { get; set; }  // 触发的工位 ID (1-4)
        public string ProductType { get; set; } = "Y29";
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
                    string prefix = $"{ProductType}{StationId:D2}{dateStr}";

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
    /// 数据采集服务 (单例)
    /// 模拟通过网口与控制卡通信
    /// </summary>
    public class DataCollectService
    {
        /// <summary>
        /// 当数据采集并分析完成后，触发此事件
        /// </summary>
        public event Action<DataResult> OnDataCollect;

        private readonly ConcurrentDictionary<int, byte> _busyStations = new ConcurrentDictionary<int, byte>();
        /// <summary>
        /// 外部 (PlcService) 调用此方法来启动一次采集
        /// </summary>
        public async Task TriggerCollectAsync(int stationId)
        {
            // 尝试将该工位标记为忙碌
            // TryAdd 是原子操作：如果添加成功返回 true（抢到锁了）；如果已存在返回 false（正在忙）
            if (!_busyStations.TryAdd(stationId, 0))
            {
                LogService.Info($"[DataAcquisition] 工位 {stationId} 正在采集中，忽略本次触发。");
                return;
            }
            LogService.Info($"[DataAcquisitionService] 工位 {stationId} 开始采集...");

            try
            {
                var result = await Task.Run(() => CollectFromControlCard(stationId));
                OnDataCollect?.Invoke(result);
            }
            catch (Exception ex)
            {
                LogService.Error($"[DataAcquisitionService] 采集失败: {ex.Message}");
            }
            finally
            {
                // 采集完成，移除该工位的忙碌状态，释放锁
                _busyStations.TryRemove(stationId, out _);
                // 延时等待PLC信号复位，防止重复触发
                Thread.Sleep(1000);
            }
        }

        /// <summary>
        /// 控制卡通信采集逻辑
        /// </summary>
        private DataResult CollectFromControlCard(int stationId)
        {
            // TODO:真实网口通信代码
            // 1. (连接到控制卡 Socket)
            // 2. (发送开始采集命令)
            // 3. (循环接收数据... 假设收到了两组数据：位移和压力)
            // 4. (断开连接)

            // 模拟生成曲线数据
            var curve = new List<DataPoint>();
            var r = new Random();
            double force = 0;
            for (double pos = 0; pos <= 10; pos += 0.1)
            {
                force += (r.NextDouble() - 0.4);
                if (pos > 5) force += (r.NextDouble() - 0.2); // 模拟压力上升
                if (force < 0) force = 0;
                curve.Add(new DataPoint(pos, force));
            }

            double maxForce = curve.Max(p => p.Y);
            double EndForce = curve.LastOrDefault().Y;

            double EndPos = curve.LastOrDefault().X;

            //初始化结果对象
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
    }
}