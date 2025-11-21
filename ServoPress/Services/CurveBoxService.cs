using OxyPlot;
using ServoPress.Models;
using ServoPress.ViewModels;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace ServoPress.Services
{
    /// <summary>
    /// 定义了Box的哪个边被击中
    /// </summary>
    public enum BoxSide
    {
        None,
        Left,
        Right,
        Top,
        Bottom
    }

    /// <summary>
    /// 存储一次相交事件的详细信息
    /// </summary>
    public class IntersectionEvent
    {
        /// <summary>
        /// 相交发生的精确坐标点
        /// </summary>
        public PointF Point { get; }

        /// <summary>
        /// 曲线击中了Box的哪条边
        /// </summary>
        public BoxSide Side { get; }

        /// <summary>
        /// True表示进入Box，False表示退出Box
        /// </summary>
        public bool IsEntering { get; }

        /// <summary>
        /// 触发此事件的是曲线的第几段（索引）
        /// </summary>
        public int SegmentIndex { get; }

        public IntersectionEvent(PointF point, BoxSide side, bool isEntering, int segmentIndex)
        {
            Point = point;
            Side = side;
            IsEntering = isEntering;
            SegmentIndex = segmentIndex;
        }

        public override string ToString()
        {
            string line = "";
            //string eventType = IsEntering ? "进入" : "退出";
            switch (Side)
            {

                case BoxSide.Top:
                    line = "上"; break;
                case BoxSide.Bottom:
                    line = "下"; break;
                case BoxSide.Left:
                    line = "左"; break;
                case BoxSide.Right:
                    line = "右"; break;
            }
            return $"[相交点{Point} 相交于(:{line})判定OK";
        }
    }

    /// <summary>
    /// 用于分析曲线与Box的相交情况
    /// </summary>
    public class CurveBoxService
    {
        //Key=工位ID, Value=该工位的Unibox列表
        public Dictionary<int, List<EvalWindow>> StationSettings { get; private set; } = new Dictionary<int, List<EvalWindow>>();

        private const string ConfigPath = "Product/UniBoxConfig.json";



        public void LoadConfig()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    StationSettings = new Dictionary<int, List<EvalWindow>>();
                    return;
                }

                string jsonString = File.ReadAllText(ConfigPath);
                ProgramConfig config = JsonSerializer.Deserialize(jsonString, ConfigJsonContext.Default.ProgramConfig);

                if (config != null && config.StationSettingsDict != null)
                {
                    // JSON Key 是 string ("1", "2"), 转换为 int Key
                    StationSettings = new Dictionary<int, List<EvalWindow>>();
                    foreach (var kvp in config.StationSettingsDict)
                    {
                        if (int.TryParse(kvp.Key, out int id))
                        {
                            StationSettings[id] = kvp.Value;
                        }
                    }
                }
                else
                {
                    StationSettings = new Dictionary<int, List<EvalWindow>>();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取配置文件失败: {ex.Message}", "系统错误", MessageBoxButton.OK, MessageBoxImage.Error);
                StationSettings = new Dictionary<int, List<EvalWindow>>();
            }
        }

        public void SaveConfig()
        {
            try
            {
                // 将 int Key 转换为 string Key 以便序列化
                var exportDict = new Dictionary<string, List<EvalWindow>>();
                foreach (var kvp in StationSettings)
                {
                    exportDict[kvp.Key.ToString()] = kvp.Value;
                }

                var configToSave = new ProgramConfig
                {
                    StationSettingsDict = exportDict
                };

                string dir = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string jsonString = JsonSerializer.Serialize(configToSave, ConfigJsonContext.Default.ProgramConfig);
                File.WriteAllText(ConfigPath, jsonString);
                MessageBox.Show("所有工位设置保存成功!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存配置文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        /// <summary>
        /// 根据工位ID获取其配置
        /// </summary>
        public List<EvalWindow> GetSettingsForStation(int stationId)
        {
            if (StationSettings.ContainsKey(stationId))
            {
                // 返回副本或引用皆可，这里直接返回引用以便读取
                return StationSettings[stationId];
            }
            return new List<EvalWindow>();
        }

        /// <summary>
        /// 更新指定工位的配置
        /// </summary>
        public void UpdateStationSettings(int stationId, List<EvalWindow> windows)
        {
            // 保存副本，防止引用问题
            StationSettings[stationId] = new List<EvalWindow>(windows);
        }

        /// <summary>
        /// 分析一条曲线（由点列表定义）与一个矩形框的完整相交历史
        /// </summary>
        /// <param name="curve">代表曲线的点列表。例如 P1, P2, P3...</param>
        /// <param name="box">目标矩形框 (UniBox)</param>
        /// <returns>一个包含所有相交事件的列表</returns>
        public List<IntersectionEvent> AnalyzeCurve(List<DataPoint> wpfBoxCorners, double x, double y, double width, double height)
        {
            // 1. 将 WPF Box 角点转换为 PointF 数组
            List<PointF> curve = wpfBoxCorners.Select(p => new PointF((float)p.X, (float)p.Y)).ToList();

            RectangleF box = new RectangleF((float)(x), (float)y, (float)width, (float)height);
            var events = new List<IntersectionEvent>();
            if (curve == null || curve.Count < 2)
            {
                return events; // 曲线至少需要两个点才能形成线段
            }

            // 确定曲线的初始状态（第一个点是在内部还是外部）
            bool wasInside = box.Contains(curve[0]);

            // 遍历曲线的每一条线段
            for (int i = 0; i < curve.Count - 1; i++)
            {
                PointF p1 = curve[i];   // 线段起点
                PointF p2 = curve[i + 1]; // 线段终点

                bool isInside = box.Contains(p2); // 线段终点的新状态

                // 1. 从内部移动到外部 (退出)
                if (wasInside && !isInside)
                {
                    // 我们需要找到线段 p1-p2 与 Box 边界的交点
                    if (FindIntersection(p1, p2, box, out PointF exitPoint, out BoxSide exitSide))
                    {
                        events.Add(new IntersectionEvent(exitPoint, exitSide, false, i));
                    }
                }
                // 2. 从外部移动到内部 (进入)
                else if (!wasInside && isInside)
                {
                    // 我们需要找到线段 p1-p2 与 Box 边界的交点
                    if (FindIntersection(p2, p1, box, out PointF entryPoint, out BoxSide entrySide))
                    {
                        events.Add(new IntersectionEvent(entryPoint, entrySide, true, i));
                    }
                }
                // 3. 一直在外部 (但可能穿过了Box)
                else if (!wasInside && !isInside)
                {
                    // 这是最复杂的情况：线段p1-p2可能完全穿过了Box
                    var hits = GetSegmentBoxIntersections(p1, p2, box);

                    // 如果有两个交点，说明它穿过了Box
                    if (hits.Count == 2)
                    {
                        // 必须对交点排序，以确定哪个是进入，哪个是退出
                        // 比较交点与p1的距离
                        var orderedHits = hits.OrderBy(h => GetDistanceSq(p1, h.point)).ToList();

                        var entry = orderedHits[0];
                        var exit = orderedHits[1];

                        events.Add(new IntersectionEvent(entry.point, entry.side, true, i));
                        events.Add(new IntersectionEvent(exit.point, exit.side, false, i));
                    }
                    //(hits.Count == 1 是线段刚好"擦"到边或角，可以根据需要处理)
                }
                // 4. 一直在内部 (wasInside && isInside)
                // 这种情况没有发生边界穿越，所以什么也不做

                // 更新状态，为下一个线段做准备
                wasInside = isInside;
            }

            return events;
        }


        public bool AnalyzeContainPoint(System.Windows.Point _point, double x, double y, double width, double height)
        {
            PointF point = new PointF((float)_point.X, (float)_point.Y);
            RectangleF box = new RectangleF((float)(x), (float)y, (float)width, (float)height);
            bool wasInside = box.Contains(point);
            return wasInside;
        }
        /// <summary>
        /// 辅助函数：查找一个线段(p_inside -> p_outside)与Box的交点
        /// </summary>
        private bool FindIntersection(PointF p_inside, PointF p_outside, RectangleF box, out PointF intersection, out BoxSide side)
        {
            var hits = GetSegmentBoxIntersections(p_inside, p_outside, box);
            if (hits.Count == 0)
            {
                intersection = PointF.Empty;
                side = BoxSide.None;
                return false;
            }

            // 穿越时，p_inside -> p_outside 线段与Box的第一个交点就是我们要的
            // 我们按交点与 p_inside 的距离排序，取最近的那个
            var closestHit = hits.OrderBy(h => GetDistanceSq(p_inside, h.point)).First();

            intersection = closestHit.point;
            side = closestHit.side;
            return true;
        }

        /// <summary>
        /// 辅助函数：获取线段 p1-p2 与 Box 四条边的所有交点
        /// </summary>
        private List<(PointF point, BoxSide side)> GetSegmentBoxIntersections(PointF p1, PointF p2, RectangleF box)
        {
            var hits = new List<(PointF point, BoxSide side)>();
            PointF intersection;

            // 底边 (Bottom)
            if (LineSegmentIntersection(p1, p2, new PointF(box.Left, box.Top), new PointF(box.Right, box.Top), out intersection))
                hits.Add((intersection, BoxSide.Bottom));

            // 顶边 (Top)
            if (LineSegmentIntersection(p1, p2, new PointF(box.Left, box.Bottom), new PointF(box.Right, box.Bottom), out intersection))
                hits.Add((intersection, BoxSide.Top));

            // 左边 (Left)
            if (LineSegmentIntersection(p1, p2, new PointF(box.Left, box.Top), new PointF(box.Left, box.Bottom), out intersection))
                hits.Add((intersection, BoxSide.Left));

            // 右边 (Right)
            if (LineSegmentIntersection(p1, p2, new PointF(box.Right, box.Top), new PointF(box.Right, box.Bottom), out intersection))
                hits.Add((intersection, BoxSide.Right));

            return hits;
        }

        /// <summary>
        /// 核心辅助函数：计算两条线段 (p1-p2) 和 (p3-p4) 是否相交
        /// </summary>
        private bool LineSegmentIntersection(PointF p1, PointF p2, PointF p3, PointF p4, out PointF intersection)
        {
            intersection = PointF.Empty;

            float dx1 = p2.X - p1.X;
            float dy1 = p2.Y - p1.Y;
            float dx2 = p4.X - p3.X;
            float dy2 = p4.Y - p3.Y;

            float denominator = (dy2 * dx1) - (dx2 * dy1);

            // 如果分母为0，两条线（或线段）平行
            if (denominator == 0)
            {
                return false;
            }

            float t_num = (dx2 * (p1.Y - p3.Y)) - (dy2 * (p1.X - p3.X));
            float u_num = (dx1 * (p1.Y - p3.Y)) - (dy1 * (p1.X - p3.X));

            float t = t_num / denominator;
            float u = u_num / denominator;

            // 只有当 t 和 u 都在 [0, 1] 范围内，两条 *线段* 才相交
            if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
            {
                intersection = new PointF(p1.X + t * dx1, p1.Y + t * dy1);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 辅助函数：计算两点之间距离的平方（避免开方，用于比较大小）
        /// </summary>
        private float GetDistanceSq(PointF p1, PointF p2)
        {
            return (p2.X - p1.X) * (p2.X - p1.X) + (p2.Y - p1.Y) * (p2.Y - p1.Y);
        }


    }
}
