using Microsoft.EntityFrameworkCore;
using ServoPress.Database;
using ServoPress.Database.Entities;
using System.Diagnostics;
using System.Text.Json;
namespace ServoPress.Services
{
    /// <summary>
    /// 数据库存储服务类(EFCore)
    /// </summary>
    public class DataStorageService
    {
        /// <summary>
        /// 确保数据库已创建（在 App 启动时调用）
        /// </summary>
        public void InitializeDatabase()
        {
            using (var context = new AppDbContext())
            {
                // 如果数据库不存在则创建
                context.Database.EnsureCreated();
            }
        }

        /// <summary>
        /// 保存测试结果
        /// </summary>
        public  void SaveResultAsync(DataResult result)
        {
            try
            {
                // 1. 将业务对象 DataResult 转换为 数据库实体 ProductionRecord
                var record = new ProductionRecord
                {
                    StationId = result.StationId,
                    StationName = $"工位-{result.StationId}",
                    ProductType =result.ProductType,
                    SerialNumber = result.GenerateSerialNumber(), 
                    Timestamp = DateTime.Now,

                    ResulEnd = result.Result,
                    ResultText = result.ResultText,

                    MinPosition = result.MinPosition,
                    MaxPosition = result.MaxPosition,
                    EndPosition = result.EndPosition,
                    MinForce = result.MinForce,
                    MaxForce = result.MaxForce,
                    EndForce = result.EndForce,
                  
                    // 核心：将曲线点列表序列化为 JSON 字符串存储
                    CurveDataJson = JsonSerializer.Serialize(result.CurveData),
                    EvalWindowsJson = JsonSerializer.Serialize(result.EvalWindow)
                };

                // 2. 写入数据库
                using (var context = new AppDbContext())
                {
                    context.ProductionRecords.Add(record);
                    context.SaveChanges();
                }

                LogService.Info($"[DataStorage] 工位 {result.StationId} 数据保存成功。ID: {record.Id}");
            }
            catch (Exception ex)
            {
                LogService.Error($"[DataStorage] 保存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 简单获取历史记录
        /// </summary>
        public List<ProductionRecord> GetHistory(DateTime start, DateTime end, int? stationId = null)
        {
            using (var context = new AppDbContext())
            {
                var query = context.ProductionRecords
                    .Where(r => r.Timestamp >= start && r.Timestamp <= end);

                if (stationId.HasValue)
                {
                    query = query.Where(r => r.StationId == stationId.Value);
                }

                // 按时间倒序，取前1000条防止卡顿
                return query.OrderByDescending(r => r.Timestamp).Take(1000).ToList();
            }
        }

        /// <summary>
        /// 高级查询历史记录
        /// </summary>
        /// <param name="start">开始时间</param>
        /// <param name="end">结束时间</param>
        /// <param name="stationIndex">工站索引 (0=全部, 1=1号...)</param>
        /// <param name="resultIndex">结果索引 (0=全部, 1=OK, 2=NG)</param>
        /// <param name="keyword">产品型号关键字</param>
        public List<ProductionRecord> QueryHistory(DateTime start, DateTime end, int stationIndex, int resultIndex, string keyword)
        {
            using (var context = new AppDbContext())
            {
                // 1. 基础时间过滤
                var query = context.ProductionRecords
                    .Where(r => r.Timestamp >= start && r.Timestamp <= end);

                // 2. 工站过滤 (ComboBox索引0为全部，1对应StationId=1)
                if (stationIndex > 0)
                {
                    query = query.Where(r => r.StationId == stationIndex);
                }

                // 3. 结果过滤 (ComboBox索引0为全部，1=OK, 2=NG)
                if (resultIndex > 0)
                {
                    bool isOk = (resultIndex == 1);
                    query = query.Where(r => r.ResulEnd == isOk);
                }

                // 4. 产品型号模糊查询
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    query = query.Where(r => r.ProductType.Contains(keyword));
                }

                // 5. 按ID倒序返回，最多500条以保证性能
                return query.OrderByDescending(r => r.Id).Take(500).ToList();
            }
        }

        /// <summary>
        /// 获取生产统计个数
        /// </summary>
        /// <param name="stationId"></param>
        /// <returns></returns>
        public (int OkCount, int NgCount) GetStationCounts(int stationId)
        {
            try
            {
                using (var context = new AppDbContext())
                {
                  
                    var okCount = context.ProductionRecords
                        .Count(r => r.StationId == stationId && r.ResulEnd == true);

                    var ngCount = context.ProductionRecords
                        .Count(r => r.StationId == stationId && r.ResulEnd == false);

                    return (okCount, ngCount);
                }
            }
            catch (Exception ex)
            {
                LogService.Error($"[DataStorage] 获取工位 {stationId} 统计数据失败: {ex.Message}");
                return (0, 0);
            }
        }

    }
}