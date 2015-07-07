using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.Networking.Connectivity;

// “空白页”项模板在 http://go.microsoft.com/fwlink/?LinkId=234238 上有介绍

namespace RobotUI
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private StreamSocket clientSocket;
        private HostName serverHost;
        private bool connected = false;
        private bool closing = false;

        enum MODE
        {
            POS     =8,
            VEL     =9,
            TRQ     =10,
            HOMING  =6,
            NONE    =-1
        }

        enum STATUS
        {
            OFF     =1,
            DISABLED=2,
            ENABLED =3,
            RUNNING =4,
            HOMING  =5,
            FAULT   =6
        }

        enum COMMON_CMD
        {
            NOCMD               = 0,
            ENABLE              = 1,
            RUNNING             = 2,
            DISABLE             = 3,
            GOHOME_1            = 5,
            GOHOME_2            = 6,
            HOME2START_1        = 7,
            HOME2START_2        = 8,
            BACK2STANDSTILL     = 9
        }

        enum HEX2_CMD
        {
            LEFT_FORWARD        = 10,
            FORWARD             = 11,
            LEFT_BACKWARD       = 12,
            RIGHT_FORWARD       = 13,
            BACKWARD            = 14,
            RIGHT_BACKWARD      = 15,
            TURNLEFT            = 16,
            TURNRIGHT           = 17,
            WAVE_BODY           = 18,
            WAVE_LEG            = 19,
            SIT                 = 20,
            STANDUP             = 21,
            OPERATION           = 22,
            ROLL_BODY           = 23,
            UPDOWN_BODY         = 24,
            START_HIGAIT        = 25,
            STOP_HIGAIT         = 26,
            HI_LEFT_FORWARD     = 27,
            HI_FORWARD          = 28,
            HI_LEFT_BACKWARD    = 29,
            HI_RIGHT_FORWARD    = 30,
            HI_BACKWARD         = 31,
            HI_RIGHT_BACKWARD   = 32
        };

        enum HEX3_CMD
        {
            FORWARD         = 10,
            BACKWARD        = 11,
            FAST_FORWARD    = 12,
            FAST_BACKWARD   = 13,
            TURNRIGHT       = 14,
            TURNLEFT        = 15,
            LEGUP           = 16
        };

        enum HEX4_CMD
        {
            SINGLE_FORWARD  = 10,
            SINGLE_BACKWARD = 11,
            SINGLE_RIGHT    = 12,
            SINGLE_LEFT     = 13,
            TURNRIGHT       = 14,
            TURNLEFT        = 15,
            FORWARD         = 16,
            BACKWARD        = 17,
            CLIMB_UP        = 18,
            GO_DOWN         = 19,
            SLOW_AND_STOP   = 20,
            SIT             = 21,
            POSITION_1      = 22,
            POSITION_2      = 23
        };

        public MainPage()
        {
            this.InitializeComponent();
            clientSocket = new StreamSocket();

            //列表参数初始化
            //List<Motor> m_Motors = new List<Motor>();
            //for (int i = 0; i < 18; i++)
            //{
            //    m_Motors.Add(new Motor { Ordinal = i, Status = 0, Mode = 0, Position = 3*i, Velocity = 4*i, Current = 5*i });
            //}
            //this.MotorGridView.ItemsSource = m_Motors;
        }

        /// <summary>
        /// 建立连接
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            if (connected)
            {
                StatusText.Text = "Already connected";
                return;
            }

            try
            {
                StatusText.Text = "Trying to connect ...";

                serverHost = new HostName(ServerHostname.Text);
                // 发起连接
                await clientSocket.ConnectAsync(serverHost,ControlPort.Text);
                connected = true;
                StatusText.Text = "Connection established";
                //开始接受数据
                if (clientSocket.Information != null)
                {
                    await BeginReceived();
                }

            }
            catch (Exception exception)
            {
                // If this is an unknown status, 
                // it means that the error is fatal and retry will likely fail.
                if (SocketError.GetStatus(exception.HResult) == SocketErrorStatus.Unknown)
                {
                    throw;
                }

                StatusText.Text = "Connect failed with error: " + exception.Message;
                // Could retry the connection, but for this simple example
                // just close the socket.

                closing = true;
                // the Close method is mapped to the C# Dispose
                clientSocket.Dispose();
                clientSocket = null;
            }
        }

        /// <summary>
        /// 断开网络连接
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            closing = true;
            clientSocket.Dispose();
            clientSocket = null;
            connected = false;
            StatusText.Text = "Socket is disconnected.";
        }

        /// <summary>
        /// 公用按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void COMMON_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            int msgID = 0;
            if (btn == null)
            {
                return;
            }
            string content = btn.Content.ToString();
            switch (content)
            {
                case "Enable":
                    msgID = (int)COMMON_CMD.ENABLE;
                    break;
                case "Running":
                    msgID = (int)COMMON_CMD.RUNNING;
                    break;
                case "PreDisable":
                    Disable.IsEnabled =!Disable.IsEnabled;
                    break;
                case "Disable":
                    msgID = (int)COMMON_CMD.DISABLE;
                    break;
                case "GoHome_1":
                    msgID = (int)COMMON_CMD.GOHOME_1;
                    GoHome_1.IsEnabled = false; //GoHome_1只能按一次
                    break;
                case "GoHome_2":
                    msgID = (int)COMMON_CMD.GOHOME_2;
                    GoHome_2.IsEnabled = false; //GoHome_2只能按一次
                    break;
                case "HomeToStart_1":
                    msgID = (int)COMMON_CMD.HOME2START_1;
                    HomeToStart_1.IsEnabled = false; //HomeToStart_1只能按一次
                    break;
                case "HomeToStart_2":
                    msgID = (int)COMMON_CMD.HOME2START_2;
                    HomeToStart_2.IsEnabled = false; //HomeToStart_2只能按一次
                    break;
                case "BackToStandstill":
                    msgID = (int)COMMON_CMD.BACK2STANDSTILL;
                    break;
            }
            SendMsg(msgID);
        }

        /// <summary>
        /// Hex II 专用按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HEX2_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            int msgID = 0;
            if (btn == null)
            {
                return;
            }
            string content = btn.Content.ToString();
            switch (content)
            {
                case "LF":
                    msgID = (int)HEX2_CMD.LEFT_FORWARD;
                    break;
                case "FW":
                    msgID = (int)HEX2_CMD.FORWARD;
                    break;
                case "LH":
                    msgID = (int)HEX2_CMD.LEFT_BACKWARD;
                    break;
                case "RF":
                    msgID = (int)HEX2_CMD.RIGHT_FORWARD;
                    break;
                case "BW":
                    msgID = (int)HEX2_CMD.BACKWARD;
                    break;
                case "RH":
                    msgID = (int)HEX2_CMD.RIGHT_BACKWARD;
                    break;
                case "TL":
                    msgID = (int)HEX2_CMD.TURNLEFT;
                    break;
                case "TR":
                    msgID = (int)HEX2_CMD.TURNRIGHT;
                    break;

                case "Sit":
                    msgID = (int)HEX2_CMD.SIT;
                    break;
                case "StandUp":
                    msgID = (int)HEX2_CMD.STANDUP;
                    break;
                case "Operation":
                    msgID = (int)HEX2_CMD.OPERATION;
                    break;
                case "RollBody":
                    msgID = (int)HEX2_CMD.ROLL_BODY;
                    break;
                case "UpDownBody":
                    msgID = (int)HEX2_CMD.UPDOWN_BODY;
                    break;

                case "StartHiGait":
                    msgID = (int)HEX2_CMD.START_HIGAIT;
                    break;
                case "StopHiGait":
                    msgID = (int)HEX2_CMD.STOP_HIGAIT;
                    break;
                case "WaveBody":
                    msgID = (int)HEX2_CMD.WAVE_BODY;
                    break;

                case "HiLF":
                    msgID = (int)HEX2_CMD.HI_LEFT_FORWARD;
                    break;
                case "HiFW":
                    msgID = (int)HEX2_CMD.HI_FORWARD;
                    break;
                case "HiLH":
                    msgID = (int)HEX2_CMD.HI_LEFT_BACKWARD;
                    break;
                case "HiRF":
                    msgID = (int)HEX2_CMD.HI_RIGHT_FORWARD;
                    break;
                case "HiBW":
                    msgID = (int)HEX2_CMD.HI_BACKWARD;
                    break;
                case "HiRH":
                    msgID = (int)HEX2_CMD.HI_RIGHT_BACKWARD;
                    break;
             }
            SendMsg(msgID);
        }

        /// <summary>
        /// Hex III 专用按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HEX3_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            int msgID = 0;
            if (btn == null)
            {
                return;
            }
            string content = btn.Content.ToString();
            switch (content)
            {
                case "Forward":
                    msgID = (int)HEX3_CMD.FORWARD;
                    break;
                case "Backward":
                    msgID = (int)HEX3_CMD.BACKWARD;
                    break;
                case "FastFW":
                    msgID = (int)HEX3_CMD.FAST_FORWARD;
                    break;
                case "FastBW":
                    msgID = (int)HEX3_CMD.FAST_BACKWARD;
                    break;
                case "LegUp":
                    msgID = (int)HEX3_CMD.LEGUP;
                    break;
                case "TurnLeft":
                    msgID = (int)HEX3_CMD.TURNLEFT;
                    break;
                case "TurnRight":
                    msgID = (int)HEX3_CMD.TURNRIGHT;
                    break;
            }
            SendMsg(msgID);
        }

        /// <summary>
        /// Hex IV专用按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HEX4_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            int msgID = 0;
            if (btn == null)
            {
                return;
            }
            string content = btn.Content.ToString();
            switch (content)
            {
                case "SingleF":
                    msgID = (int)HEX4_CMD.SINGLE_FORWARD;
                    break;
                case "SingleB":
                    msgID = (int)HEX4_CMD.SINGLE_BACKWARD;
                    break;
                case "SingleR":
                    msgID = (int)HEX4_CMD.SINGLE_RIGHT;
                    break;
                case "SingleL":
                    msgID = (int)HEX4_CMD.SINGLE_LEFT;
                    break;

                case "TurnRight":
                    msgID = (int)HEX4_CMD.TURNRIGHT;
                    break;
                case "TurnLeft":
                    msgID = (int)HEX4_CMD.TURNLEFT;
                    break;
                case "Forward":
                    msgID = (int)HEX4_CMD.FORWARD;
                    break;
                case "Backward":
                    msgID = (int)HEX4_CMD.BACKWARD;
                    break;

                case "ClimbUp":
                    msgID = (int)HEX4_CMD.CLIMB_UP;
                    break;
                case "GoDown":
                    msgID = (int)HEX4_CMD.GO_DOWN;
                    break;
                case "SlowAndStop":
                    msgID = (int)HEX4_CMD.SLOW_AND_STOP;
                    break;
                case "Sit":
                    msgID = (int)HEX4_CMD.SIT;
                    break;
                case "WaveBody_1":
                    msgID = (int)HEX4_CMD.POSITION_1;
                    break;
                case "WaveBody_2":
                    msgID = (int)HEX4_CMD.POSITION_2;
                    break;
            }
            SendMsg(msgID);
        }

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="msgID"></param>
        private async void SendMsg(int msgID)
        {
            if (!connected)
            {
                StatusText.Text = "Must be connected to send!";
                return;
            }

            try
            {
                //StatusText.Text = "Trying to send data ...";

                byte[] sData = new byte[4];
                byte[] sendData = ConvSendMsg(sData, (uint)sData.Length, msgID);
                DataWriter writer = new DataWriter(clientSocket.OutputStream);
                //把数据写入到发送流
                writer.WriteBytes(sendData);
                //异步发送
                await writer.StoreAsync();

                //显示发送的MsgID
                int sendID = BitConverter.ToInt32(sendData, 4);
                StatusText.Text = "MsgID " + sendID.ToString() + " was sent";

                // detach the stream and close it
                writer.DetachStream();
                writer.Dispose();

            }
            catch (Exception exception)
            {
                // If this is an unknown status, 
                // it means that the error is fatal and retry will likely fail.
                if (SocketError.GetStatus(exception.HResult) == SocketErrorStatus.Unknown)
                {
                    throw;
                }

                StatusText.Text = "Send data or receive failed with error: " + exception.Message;
                // Could retry the connection, but for this simple example
                // just close the socket.

                closing = true;
                clientSocket.Dispose();
                clientSocket = null;
                connected = false;

            }
        }

        /// <summary>
        /// 封装发送的数据包
        /// </summary>
        /// <param name="sendData">数据内容</param>
        /// <param name="dataLength">数据长度</param>
        /// <param name="msgID">msgID</param>
        /// <returns>返回封装好的数据包</returns>
        private byte[] ConvSendMsg(byte[] sendData, UInt32 dataLength, int msgID)
        {
            //数据包格式为 数据大小 + msgID + type + 保留字段 + 保留字段 + 用户自定义数据 + 数据内容

            // 0-3  字节，  unsigned int 代表数据大小
            byte[] bLength = System.BitConverter.GetBytes(dataLength);

            // 4-7  字节，  int          代表msgID
            byte[] bID = System.BitConverter.GetBytes(msgID);

            // 8-15 字节，  long long    代表type
            // 16-23字节，  long long    目前保留，准备用于时间戳
            // 24-31字节，  long long    目前保留，准备用于时间戳
            // 32-39字节，  long long    用户可以自定义的8字节数据
            byte[] bReserved = new byte[32];

            //数据包总长度
            int size = sendData == null ? 40 : sendData.Length + 40;

            //组装数据包
            byte[] b = new byte[size];
            Array.Copy(bLength, 0, b, 0, 4);
            Array.Copy(bID, 0, b, 4, 4);
            if (sendData != null)
            {
                Array.Copy(sendData, 0, b, 40, sendData.Length);
            }
            return b;
        }

        /// <summary>
        /// 接收数据
        /// </summary>
        /// <returns></returns>
        private async Task BeginReceived()
        {

            //绑定已连接的StreamSocket到DataReader
            DataReader reader = new DataReader(clientSocket.InputStream);
            while (true)
            {
                try
                {
                    byte[] tempByteArr;

                    //获取数据长度
                    tempByteArr = new byte[4];
                    await reader.LoadAsync(sizeof(uint));
                    reader.ReadBytes(tempByteArr);
                    uint dataLength = System.BitConverter.ToUInt32(tempByteArr, 0);
                    //StatusText.Text = dataLength.ToString();

                    //获取msgID
                    tempByteArr = new byte[4];
                    await reader.LoadAsync(sizeof(int));
                    reader.ReadBytes(tempByteArr);
                    int msgID = System.BitConverter.ToInt32(tempByteArr, 0);

                    //读完数据头
                    tempByteArr = new byte[32];
                    await reader.LoadAsync(32);
                    reader.ReadBytes(tempByteArr);

                    //定义电机参数列表
                    List<Motor> m_Motors = new List<Motor>();

                    //读取数据内容
                    for (int i = 0; i < dataLength/(5*sizeof(int)); i++ )
                    {
                        //每次读取同一个电机的5个参数
                        int[] motorPM = new int[5];
                        for(int j=0;j<5;j++)
                        {
                            tempByteArr = new byte[4];
                            await reader.LoadAsync(sizeof(uint));
                            reader.ReadBytes(tempByteArr);
                            motorPM[j] = System.BitConverter.ToInt32(tempByteArr, 0);
                        }
                        Motor motor = new Motor 
                        { 
                            Ordinal = i, 
                            Status = Enum.GetName(typeof(STATUS),motorPM[0]), 
                            Mode = Enum.GetName(typeof(MODE),motorPM[1]),
                            Position = motorPM[2], 
                            Velocity = motorPM[3], 
                            Current = motorPM[4] 
                        };
                        m_Motors.Add(motor);
                    }
                    this.MotorGridView.ItemsSource = m_Motors;
                }
                catch (Exception exception)
                {
                    // If this is an unknown status, 
                    // it means that the error is fatal and retry will likely fail.
                    if (SocketError.GetStatus(exception.HResult) == SocketErrorStatus.Unknown)
                    {
                        throw;
                    }

                    StatusText.Text = "Send data or receive failed with error: " + exception.Message;
                    // Could retry the connection, but for this simple example
                    // just close the socket.

                    closing = true;
                    clientSocket.Dispose();
                    clientSocket = null;
                    connected = false;
                }
            }
        }

        /// <summary>
        /// 切换控制界面
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UIMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string UIMode = e.AddedItems[0].ToString();
            switch (UIMode)
            {
                case "Hex II":
                    HexIIIGrid.Visibility = Visibility.Collapsed;
                    HexIVGrid.Visibility = Visibility.Collapsed;
                    HexIIGrid.Visibility = Visibility.Visible;
                    break;
                case "Hex III":
                    HexIIGrid.Visibility = Visibility.Collapsed;
                    HexIVGrid.Visibility = Visibility.Collapsed;
                    HexIIIGrid.Visibility = Visibility.Visible;
                    break;
                case "Hex IV":
                    HexIIGrid.Visibility = Visibility.Collapsed;
                    HexIIIGrid.Visibility = Visibility.Collapsed;
                    HexIVGrid.Visibility = Visibility.Visible;
                    break;
            }            
        }

    }
}
