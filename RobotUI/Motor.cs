using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RobotUI
{
    /// <summary>
    /// 电机类
    /// </summary>
    public class Motor
    {
        /// <summary>
        /// 电机序号
        /// </summary>
        public int Ordinal { get; set; }
        /// <summary>
        /// 电机状态
        /// </summary>
        public int Status { get; set; }
        /// <summary>
        /// 工作模式
        /// </summary>
        public int Mode { get; set; }
        /// <summary>
        /// 电机转角
        /// </summary>
        public int Position { get; set; }
        /// <summary>
        /// 电机转速
        /// </summary>
        public int Velocity { get; set; }
        /// <summary>
        /// 电机电流
        /// </summary>
        public int Current { get; set; }
    }
}
