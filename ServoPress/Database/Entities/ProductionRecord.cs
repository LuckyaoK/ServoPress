using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServoPress.Database.Entities
{
    /// <summary>
    /// 生产记录实体（对应数据库表）
    /// </summary>
    public class ProductionRecord
    {
        /// <summary>
        /// 序号ID
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        /// <summary>
        /// 工站ID
        /// </summary>
        public int StationId { get; set; }
        /// <summary>
        /// 工站名称
        /// </summary>
        public string StationName { get; set; }
        /// <summary>
        /// 产品序列号
        /// </summary>
        public string SerialNumber { get; set; }
        /// <summary>
        /// 产品型号
        /// </summary>
        public string ProductModel { get; set; } 
        /// <summary>
        /// 时间
        /// </summary>
        public DateTime Timestamp { get; set; }
        /// <summary>
        /// 判定结果
        /// </summary>
        public bool ResulEnd { get; set; }
        /// <summary>
        /// 结果文本
        /// </summary>
        public string ResultText { get; set; }
        /// <summary>
        ///  最小位移
        /// </summary>
        public double MinPosition { get; set; }
        /// <summary>
        /// 最大位移
        /// </summary>
        public double MaxPosition { get; set; }
        /// <summary>
        /// 最小压力
        /// </summary>
        public double MinForce { get; set; }
        /// <summary>
        /// 最大压力
        /// </summary>
        public double MaxForce { get; set; }
        /// <summary>
        /// 最终位移
        /// </summary>
        public double EndPosition { get; set; }
        /// <summary>
        /// 最终压力
        /// </summary>
        public double EndForce { get; set; }

        /// <summary>
        /// 曲线数据;数据库里存字符串，代码里用 JSON 转换
        /// </summary>
        public string CurveDataJson { get; set; }
    }
}
