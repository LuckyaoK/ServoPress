using OxyPlot;
using ServoPress.Database;
using ServoPress.Database.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ServoPress.Services
{
    /// <summary>
    /// 数据库存储服务类
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
        /// 异步保存测试结果
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
                    ProductModel = "test",
                    SerialNumber = DateTime.Now.ToString("yyyyMMddHHmmss"), // 模拟序列号
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
                    CurveDataJson = JsonSerializer.Serialize(result.CurveData)
                };

                // 2. 写入数据库
                using (var context = new AppDbContext())
                {
                    context.ProductionRecords.Add(record);
                    context.SaveChanges();
                }

                Debug.WriteLine($"[DataStorage] 工位 {result.StationId} 数据保存成功。ID: {record.Id}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DataStorage] 保存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取历史记录（用于历史查询界面）
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
    }
}