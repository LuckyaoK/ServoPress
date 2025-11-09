using ServoPress.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServoPress.Services
{
    /// <summary>
    /// 接口：负责与运动控制卡硬件通信
    /// </summary>
    public interface IMotionControlService
    {
        /// <summary>
        /// 控制卡运行
        /// </summary>
        Task RunProgramAsync(IEnumerable<SequenceStep> steps);

        /// <summary>
        /// 命令运动卡停止
        /// </summary>
        Task StopProgramAsync();

        /// <summary>
        /// 命令运动卡复位
        /// </summary>
        Task ResetAsync();
    }
}
