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

namespace RobotPM
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
            POS = 8,
            VEL = 9,
            TRQ = 10,
            HOMING = 6,
            NONE = -1
        }

        enum STATUS
        {
            OFF = 1,
            DISABLED = 2,
            ENABLED = 3,
            RUNNING = 4,
            HOMING = 5,
            FAULT = 6
        }

        enum COMMON_CMD
        {
            ENABLE,
            DISABLE,
            GOHOME_1,
            GOHOME_2,
            HOME2START_1,
            HOME2START_2,
            PARAMETER
        }

        public MainPage()
        {
            this.InitializeComponent();
            clientSocket = new StreamSocket();
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
                await clientSocket.ConnectAsync(serverHost, ControlPort.Text);
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

        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {

            closing = true;
            clientSocket.Dispose();
            clientSocket = null;
            connected = false;
            StatusText.Text = "Socket is disconnected.";
        }

        private void COMMON_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            int msg = 0;
            if (btn == null)
            {
                return;
            }
            string content = btn.Content.ToString();
            switch (content)
            {
                case "Enable":
                    msg = (int)COMMON_CMD.ENABLE;
                    break;
                case "Disable":
                    msg = (int)COMMON_CMD.DISABLE;
                    break;
                case "GoHome_1":
                    msg = (int)COMMON_CMD.GOHOME_1;
                    GoHome_1.IsEnabled = false; //GoHome_1只能按一次
                    break;
                case "GoHome_2":
                    msg = (int)COMMON_CMD.GOHOME_2;
                    GoHome_2.IsEnabled = false; //GoHome_2只能按一次
                    break;
                case "HomeToStart_1":
                    msg = (int)COMMON_CMD.HOME2START_1;
                    HomeToStart_1.IsEnabled = false; //HomeToStart_1只能按一次
                    break;
                case "HomeToStart_2":
                    msg = (int)COMMON_CMD.HOME2START_2;
                    HomeToStart_2.IsEnabled = false; //HomeToStart_2只能按一次
                    break;
            }
            byte[] bMsg = System.BitConverter.GetBytes(msg);
            SendMsg(bMsg,0);

            //显示发送的MsgID
            StatusText.Text = "Msg " + msg.ToString() + " is sent";
        }

        /// <summary>
        /// 发送用户输入参数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SendPm_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            int i=Grid.GetRow(btn);
            TextBox total_count = (TextBox)SendPmGrid.FindName("total_count" + i.ToString());
            ComboBox walkDir = (ComboBox)SendPmGrid.FindName("walkDir" + i.ToString());
            ComboBox upDir = (ComboBox)SendPmGrid.FindName("upDir" + i.ToString());
            TextBox step_d = (TextBox)SendPmGrid.FindName("step_d" + i.ToString());
            TextBox step_h = (TextBox)SendPmGrid.FindName("step_h" + i.ToString());
            TextBox step_alpha = (TextBox)SendPmGrid.FindName("step_alpha" + i.ToString());
            TextBox step_beta = (TextBox)SendPmGrid.FindName("step_beta" + i.ToString());
            TextBox step_num = (TextBox)SendPmGrid.FindName("step_num" + i.ToString());

            int begin = (int)COMMON_CMD.PARAMETER; //定义数据头
            uint tc = uint.Parse(total_count.Text);
            string wdir = walkDir.SelectedItem.ToString();
            string udir = upDir.SelectedItem.ToString();
            double sd = double.Parse(step_d.Text);
            double sh = double.Parse(step_h.Text);
            double salpha = double.Parse(step_alpha.Text);
            double sbeta = double.Parse(step_beta.Text);
            uint snum = uint.Parse(step_num.Text);

            byte[] b0 = System.BitConverter.GetBytes(begin);
            byte[] b1 = System.BitConverter.GetBytes(tc);
            byte[] b2 = System.Text.UnicodeEncoding.UTF8.GetBytes(wdir);
            byte[] b3 = System.Text.UnicodeEncoding.UTF8.GetBytes(udir);
            byte[] b4 = System.BitConverter.GetBytes(sd);
            byte[] b5 = System.BitConverter.GetBytes(sh);
            byte[] b6 = System.BitConverter.GetBytes(salpha);
            byte[] b7 = System.BitConverter.GetBytes(sbeta);
            byte[] b8 = System.BitConverter.GetBytes(snum);

            byte[] Pm = new byte[60]; //C#会自动将byte数组中的每个元素初始化为0

            Array.Copy(b0, 0, Pm, 0, b0.Length);
            Array.Copy(b1, 0, Pm, 4, b1.Length);
            Array.Copy(b2, 0, Pm, 8, b2.Length);
            Array.Copy(b3, 0, Pm, 16, b3.Length);
            Array.Copy(b4, 0, Pm, 24, b4.Length);
            Array.Copy(b5, 0, Pm, 32, b5.Length);
            Array.Copy(b6, 0, Pm, 40, b6.Length);
            Array.Copy(b7, 0, Pm, 48, b7.Length);
            Array.Copy(b8, 0, Pm, 56, b8.Length);

            SendMsg(Pm, 0);

            //显示发送数据内容
            StatusText.Text = tc.ToString() + wdir + udir + sd.ToString() + sh.ToString() + salpha.ToString() + sbeta.ToString() + snum.ToString();
        }

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="sData">待发送的数据正文</param>
        /// <param name="msgID">msgID</param>
        private async void SendMsg(byte[] sData, int msgID)
        {
            if (!connected)
            {
                StatusText.Text = "Must be connected to send!";
                return;
            }

            try
            {
                //StatusText.Text = "Trying to send data ...";

                byte[] sendData = ConvSendMsg(sData, (uint)sData.Length, msgID);
                DataWriter writer = new DataWriter(clientSocket.OutputStream);
                //把数据写入到发送流
                writer.WriteBytes(sendData);
                //异步发送
                await writer.StoreAsync();

                //显示发送的MsgID
                //int sendID = BitConverter.ToInt32(sendData, 4);
                //StatusText.Text = "MsgID " + sendID.ToString() + " was sent";

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
                    for (int i = 0; i < dataLength / (5 * sizeof(int)); i++)
                    {
                        //每次读取同一个电机的5个参数
                        int[] motorPM = new int[5];
                        for (int j = 0; j < 5; j++)
                        {
                            tempByteArr = new byte[4];
                            await reader.LoadAsync(sizeof(uint));
                            reader.ReadBytes(tempByteArr);
                            motorPM[j] = System.BitConverter.ToInt32(tempByteArr, 0);
                        }
                        Motor motor = new Motor
                        {
                            Ordinal = i,
                            Status = Enum.GetName(typeof(STATUS), motorPM[0]),
                            Mode = Enum.GetName(typeof(MODE), motorPM[1]),
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

    }
}
