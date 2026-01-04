using OxyPlot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ServoPress.Services
{
    /// <summary>
    /// 数据优化与抽稀工具类
    /// 提供三种核心算法：
    /// 1. SmoothMovingAverage: 平滑滤波，消除高频噪声（毛刺）。
    /// 2. DownsampleMinMax: UI渲染专用抽稀，保证不卡顿且不丢失峰值。
    /// 3. CompressCurveData: 数据存储专用压缩，死区过滤，大幅减小体积。
    /// </summary>
    public static class DataOptimizer
    {
        #region 1. 预处理：平滑滤波 (解决曲线波动/毛刺问题)

        /// <summary>
        /// 移动平均滤波算法
        /// 作用：消除传感器的高频噪声，让曲线变得平滑连贯。
        /// 建议在所有抽稀/压缩操作之前，先调用此方法处理原始数据。
        /// </summary>
        /// <param name="data">原始数据</param>
        /// <param name="windowSize">窗口大小 (建议 5-10)。值越大越平滑，但对峰值的响应会变慢。</param>
        /// <returns>平滑后的数据</returns>
        public static List<DataPoint> SmoothMovingAverage(this List<DataPoint> data, int windowSize = 5)
        {
            if (data == null || data.Count < windowSize) return data ?? new List<DataPoint>();

            var result = new List<DataPoint>(data.Count);

            for (int i = 0; i < data.Count; i++)
            {
                // 动态计算窗口范围，处理边界情况
                int start = Math.Max(0, i - windowSize / 2);
                int end = Math.Min(data.Count - 1, i + windowSize / 2);
                int count = end - start + 1;

                double sumX = 0;
                double sumY = 0;

                for (int j = start; j <= end; j++)
                {
                    sumX += data[j].X;
                    sumY += data[j].Y;
                }

                result.Add(new DataPoint(sumX / count, sumY / count));
            }

            return result;
        }

        #endregion

        #region 2. UI显示优化：Min-Max 抽稀 (解决 OxyPlot 卡顿问题)

        /// <summary>
        /// 使用 Min-Max 算法对数据进行定额抽稀。
        /// 作用：将任意长度的数据缩减到固定的 targetCount (如3000点)，确保界面 FPS 稳定。
        /// 原理：将数据分桶，每桶只取 Min 和 Max，保证波峰波谷不丢失。
        /// </summary>
        /// <param name="data">数据源 (建议传入平滑后的数据)</param>
        /// <param name="targetCount">目标点数 (建议 2000-4000)</param>
        /// <returns>抽稀后的数据</returns>
        public static List<DataPoint> DownsampleMinMax(this List<DataPoint> data, int targetCount = 2000)
        {
            if (data == null || data.Count <= targetCount) return data ?? new List<DataPoint>();

            // 预估容量：targetCount * 2 (每个桶取 Min 和 Max 两个点)
            var result = new List<DataPoint>(targetCount * 2);

            int step = data.Count / targetCount;
            if (step < 1) step = 1;

            for (int i = 0; i < data.Count; i += step)
            {
                int rangeEnd = Math.Min(i + step, data.Count);
                if (i >= rangeEnd) break;

                // 初始化极值
                DataPoint minP = data[i];
                DataPoint maxP = data[i];

                // 找极值
                for (int j = i + 1; j < rangeEnd; j++)
                {
                    if (data[j].Y < minP.Y) minP = data[j];
                    if (data[j].Y > maxP.Y) maxP = data[j];
                }

                // 按时间顺序添加，防止回绕
                if (minP.X < maxP.X)
                {
                    result.Add(minP);
                    result.Add(maxP);
                }
                else
                {
                    result.Add(maxP);
                    result.Add(minP);
                }
            }

            return result;
        }

        #endregion

        #region 3. 数据存储优化：死区过滤压缩 (解决数据库体积膨胀问题)

        /// <summary>
        /// 数据压缩与死区过滤算法
        /// 作用：智能过滤掉“无变化”的数据，保留特征点。
        /// 适用：存入数据库或 CSV 文件。
        /// </summary>
        /// <param name="rawData">原始数据</param>
        /// <param name="forceThreshold">力值变化死区 (例如 0.5N)</param>
        /// <param name="posThreshold">位移变化死区 (例如 0.01mm)</param>
        /// <returns>压缩后的数据</returns>
        public static List<DataPoint> CompressCurveData(this List<DataPoint> rawData, double forceThreshold = 0.1, double posThreshold = 0.01)
        {
            if (rawData == null || rawData.Count == 0) return new List<DataPoint>();

            // 预估容量：假设压缩率为 10%
            var result = new List<DataPoint>(rawData.Count / 10 + 100);

            // 总是记录第一个点
            DataPoint lastSavedPoint = rawData[0];
            result.Add(lastSavedPoint);

            // 强制记录间隔 (防止长时间无数据)
            const int MAX_INTERVAL_COUNT = 1000;
            // 最小记录间隔 (防止噪声导致数据过密)
            const int MIN_INTERVAL_COUNT = 5;

            int pointsSkipped = 0;

            for (int i = 1; i < rawData.Count; i++)
            {
                var current = rawData[i];
                bool isLastPoint = (i == rawData.Count - 1);

                // 计算变化量
                double deltaForce = Math.Abs(current.Y - lastSavedPoint.Y);
                double deltaPos = Math.Abs(current.X - lastSavedPoint.X);

                // 判定逻辑
                // 1. 间隔保护：距离上一个点至少隔了 MIN_INTERVAL_COUNT 个点 (除非是必须要记的大变化)
                bool isIntervalOk = pointsSkipped >= MIN_INTERVAL_COUNT;

                // 2. 变化判定：力或位移变化超过阈值
                bool isChangeBig = (deltaForce > forceThreshold) || (deltaPos > posThreshold);

                // 3. 强制记录：时间太久了，必须记一个心跳点
                bool isForceRecord = pointsSkipped >= MAX_INTERVAL_COUNT;

                // 综合判断：
                // A. 是最后一个点 -> 必须记
                // B. 强制记录触发 -> 必须记
                // C. 变化大 且 间隔满足最小限制 -> 记 (兼顾细节与去噪)
                if (isLastPoint || isForceRecord || (isChangeBig && isIntervalOk))
                {
                    result.Add(current);
                    lastSavedPoint = current;
                    pointsSkipped = 0;
                }
                else
                {
                    pointsSkipped++;
                }
            }

            // 输出日志 (可选)
            // double compressionRate = 1.0 - (double)result.Count / rawData.Count;
            // LogService.Info($"[数据压缩] 原始: {rawData.Count} -> 压缩后: {result.Count} (丢弃率: {compressionRate:P1})");

            return result;
        }

        #endregion
    }
}