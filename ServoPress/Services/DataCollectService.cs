using OxyPlot;
using ServoPress.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ServoPress.Services
{
    /// <summary>
    /// 定义从控制卡采集到的完整数据包
    /// </summary>
    public class DataResult
    {
        /// <summary>
        /// 触发的工位 ID (1-4)
        /// </summary>
        public int StationId { get; set; }

        /// <summary>
        /// 最终判定结果 (OK, NG, WAIT)
        /// </summary>
        public string Result { get; set; }

        /// <summary>
        /// 采集到的曲线数据 (位移 vs 压力)
        /// </summary>
        public List<DataPoint> CurveData { get; set; }

        /// <summary>
        /// 过程值
        /// </summary>
        public double StartPosition { get; set; }
        public double EndPosition { get; set; }
        public double StartForce { get; set; }
        public double MaxForce { get; set; }

        /// <summary>
        /// 统计
        /// </summary>
        public bool IsOk { get; set; }
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

        private bool _isBusy = false; // 确保同一时间只采一个


        private readonly Dictionary<int, List<EvalWindow>> _stationSettings ;

       
        public DataCollectService(Dictionary<int, List<EvalWindow>> stationSettings)
        {
            _stationSettings = stationSettings;
        }


        /// <summary>
        /// 外部 (PlcService) 调用此方法来启动一次采集
        /// </summary>
        public async Task TriggerCollectAsync(int stationId)
        {
            if (_isBusy ) return; // 如果正在采集，则忽略新的触发信号
            _isBusy = true;
            Debug.WriteLine($"[DataAcquisitionService] 工位 {stationId} 开始采集...");

            try
            {
                // 1. 在后台线程上运行模拟采集
                var result = await Task.Run(() => CollectFromControlCard(stationId));

                // 2. 采集完成，触发事件
                //    (注意: 此时我们在后台线程)
                OnDataCollect?.Invoke(result);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DataAcquisitionService] 采集失败: {ex.Message}");
            }
            finally
            {
                _isBusy = false;
            }
        }

        /// <summary>
        /// [模拟] 真正与控制卡通信的逻辑
        /// </summary>
        private DataResult CollectFromControlCard(int stationId)
        {
            // TODO: 在这里替换为您的真实网口通信代码
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

            // 根据配置设置进行判定
            for(int i = 0; i < _stationSettings[stationId].Count;i++)
            {
                var value = _stationSettings[stationId][i].EndY;
                if (force> value)
                {

                }
            }
           
            bool isOk = r.NextDouble() > 0.2; // 80% 概率 OK
            double maxForce = curve.Max(p => p.Y);

            // 5. 将所有数据打包成结果对象
            return new DataResult
            {
                StationId = stationId,
                Result = isOk ? "OK" : "NG",
                IsOk = isOk,
                CurveData = curve,
                StartPosition = 0.0,
                EndPosition = 10.0,
                StartForce = 0.0,
                MaxForce = maxForce
            };
        }
    }
}