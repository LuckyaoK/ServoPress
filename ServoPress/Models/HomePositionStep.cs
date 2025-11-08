using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServoPress.Models
{
    public partial class HomePositionStep : SequenceStep
    {
        public override string StepType => "回原";
        public override string IconSource => "/Resources/Icons/HomePosition.svg";
        public override string Summary => $"参考: {Reference} 位置: {Position} mm,速度: {Speed} mm/s,最大力: {MaxForce} N,最小力: {MinForce} N";

        /// <summary>
        /// 描述
        /// </summary>
        [ObservableProperty]
        private string _description = "HOME_POSITION";

        /// <summary>
        /// 参考
        /// </summary>
        [ObservableProperty]
       
        private string _reference = "绝对位移";

        /// <summary>
        /// 位置
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Summary))]
        private double _position = 0.00;

        /// <summary>
        /// 速度
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Summary))]
        private double _speed = 50.00;

        /// <summary>
        /// 最大力
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Summary))]
        private double _maxForce = 5000;

        /// <summary>
        /// 最小力
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Summary))]
        private double _minForce = -5000;

    }
}
