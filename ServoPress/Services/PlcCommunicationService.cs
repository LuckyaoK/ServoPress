using HslCommunication;
using HslCommunication.Profinet.Siemens;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ServoPress.Services
{
    /// <summary>
    ///  PLC通信服务类
    /// </summary>
    public class PlcCommunicationService : IDisposable
    {
        private readonly SiemensS7Net _siemensS7Net;
        private readonly string _ipAddress;
        private bool _isDisposed = false;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="ipAddress">PLC 的 IP 地址</param>
        public PlcCommunicationService(string ipAddress)
        {
            _ipAddress = ipAddress;
            _siemensS7Net = new SiemensS7Net(SiemensPLCS.S1200, _ipAddress);
            _siemensS7Net.ConnectTimeOut = 5000; // 5秒连接超时
            ConnectAsync();
        }


        /// <summary>
        /// [可选] 显式连接 PLC。
        /// 注意：在调用 Read/Write 时 HSL 会自动连接，
        /// 但在启动时调用一次此方法是很好的做法。
        /// </summary>
        public  bool ConnectAsync()
        {
            return _siemensS7Net.ConnectServer().IsSuccess;
        }

    
        /// <summary>
        /// 读取 Bool 值 (例如 "M100.0", "DB1.DBX0.0")
        /// </summary>
        //public bool ReadBool(string address)
        //{
        //    return  _siemensS7Net.ReadBool(address).Content;
        //}

        public OperateResult<bool> ReadBool(string address)
        {
            return _siemensS7Net.ReadBool(address);
        }



        /// <summary>
        /// 写入 Bool 值
        /// </summary>
        public bool WriteBool(string address, bool value)
        {
            return  _siemensS7Net.Write(address, value).IsSuccess;
        }


        /// <summary>
        /// 读取 short 
        /// </summary>
        public short ReadShort(string address)
        {
            return _siemensS7Net.ReadInt16(address).Content;
        }

        /// <summary>
        /// 写入 short 西门子 PLC（S7-1200/1500/300/400）中，Int 代表 16位有符号整数（-32768 到 32767），占用 2个字节。
        /// 如果是 32位整数，西门子通常标记为 DInt(Double Int)。
        /// 
        /// </summary>
        public OperateResult WriteInt16(string address, short value)
        {
            return _siemensS7Net.Write(address, value); ;
        }

        /// <summary>
        /// 写入 Int32 值
        /// </summary>
        public OperateResult WriteInt32(string address, int value)
        {
            return _siemensS7Net.Write(address, value);
        }

        /// <summary>
        /// 读取 Int32 值 (西门子 DInt, 例如 "MD20", "DB1.DBD4")
        /// </summary>
        public OperateResult<int> ReadInt(string address)
        {
            return  _siemensS7Net.ReadInt32(address);
        }



        /// <summary>
        /// 关闭连接并释放资源
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _siemensS7Net?.ConnectClose();
            LogService.Info("[PlcService] 已断开连接并释放。");
        }
    }
}