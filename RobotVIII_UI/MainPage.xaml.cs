using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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
using System.Threading.Tasks;

// “空白页”项模板在 http://go.microsoft.com/fwlink/?LinkId=234238 上有介绍

namespace RobotVIII_UI
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
        string wkDistance = "0.1";

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

                serverHost = new HostName(ServerIP.Text);
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

                    if (dataLength > 0)
                    {
                        //读取数据正文
                        tempByteArr = new byte[dataLength];
                        await reader.LoadAsync(dataLength);
                        reader.ReadBytes(tempByteArr);
                        StatusText.Text = System.Text.UnicodeEncoding.UTF8.GetString(tempByteArr, 0, int.Parse(dataLength.ToString()));
                    }
                    else
                    {
                        StatusText.Text = "";
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
        /// 发送数据
        /// </summary>
        /// <param name="sData"></param>
        private async void SendMsg(byte[] sData)
        {
            if (!connected)
            {
                StatusText.Text = "Must be connected to send!";
                return;
            }

            try
            {
                //StatusText.Text = "Trying to send data ...";

                byte[] sendData = ConvSendMsg(sData, 0);
                DataWriter writer = new DataWriter(clientSocket.OutputStream);
                //把数据写入到发送流
                writer.WriteBytes(sendData);
                //异步发送
                await writer.StoreAsync();

                //显示发送的消息内容
                //StatusText.Text = System.Text.UnicodeEncoding.UTF8.GetString(sendData, 40, sData.Length) + " was sent";

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
        private byte[] ConvSendMsg(byte[] sendData, int msgID, Int64 msgType = 1)
        {
            //数据包格式为 数据大小 + msgID + type + 保留字段 + 保留字段 + 用户自定义数据 + 数据内容

            // 0-3  字节，  unsigned int 代表数据大小
            int dataSize = sendData == null ? 0 : sendData.Length + 1;
            byte[] bLength = System.BitConverter.GetBytes(dataSize);

            // 4-7  字节，  int          代表msgID
            byte[] bID = System.BitConverter.GetBytes(msgID);

            // 8-15 字节，  long long    代表type
            byte[] bType = System.BitConverter.GetBytes(msgType);

            // 16-23字节，  long long    目前保留，准备用于时间戳
            // 24-31字节，  long long    目前保留，准备用于时间戳
            // 32-39字节，  long long    用户可以自定义的8字节数据
            byte[] bReserved = new byte[24];

            //组装数据包`
            byte[] b = new byte[dataSize + 40];
            Array.Copy(bLength, 0, b, 0, 4);
            Array.Copy(bID, 0, b, 4, 4);
            Array.Copy(bType, 0, b, 8, 8);
            if (sendData != null)
            {
                Array.Copy(sendData, 0, b, 40, sendData.Length);
            }
            return b;
        }

        /// <summary>
        /// 发送用户输入的命令及参数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SendPm_Click(object sender, RoutedEventArgs e)
        {
            byte[] Pm = System.Text.UnicodeEncoding.UTF8.GetBytes(command.Text);
            SendMsg(Pm);
            wkNum.Value = 1;

        }

        /// <summary>
        /// 发送Robots自带的基本命令
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Common_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null)
            {
                return;
            }
            string basicCmd;
            string btnContent = btn.Content.ToString();
            switch (btnContent)
            {
                case "Enable":
                    basicCmd = "en -a";
                    break;
                case "Disable":
                    basicCmd = "ds -a";
                    break;
                case "GoHome1":
                    basicCmd = "hm -f";
                    break;
                case "GoHome2":
                    basicCmd = "hm -s";
                    break;
                case "Recover1":
                    basicCmd = "rc -f";
                    break;
                case "Recover2":
                    basicCmd = "rc -s";
                    break;
                case "FastWalk":
                    basicCmd = "fw -n=" + wkNum.Value.ToString();
                    break;
                case "ResetOrigin":
                    basicCmd = "ro";
                    break;
                default:
                    basicCmd = "";
                    break;
            }
            byte[] sendBytes = System.Text.UnicodeEncoding.UTF8.GetBytes(basicCmd);
            SendMsg(sendBytes);
            wkNum.Value = 1;
        }

        /// <summary>
        /// 读取被选中的RadioButton的值
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void wkDistanceCheck(object sender, RoutedEventArgs e)
        {
            RadioButton rb = sender as RadioButton;
            wkDistance = rb.Content.ToString();
        }

        /*移动身体的cmfj命令*/
        private void Start_Click(object sender, RoutedEventArgs e)
        {
            Start.IsEnabled = false;
            Pull.IsEnabled = true;
            Push.IsEnabled = true;
            Stop.IsEnabled = true;

            bodyForwardBtn.IsEnabled = true;
            bodyBackwardBtn.IsEnabled = true;
            bodyLeftBtn.IsEnabled = true;
            bodyRightBtn.IsEnabled = true;
            bodyDownBtn.IsEnabled = true;
            bodyUpBtn.IsEnabled = true;

            string cmd = "cmfb";
            byte[] sendBytes = System.Text.UnicodeEncoding.UTF8.GetBytes(cmd);
            SendMsg(sendBytes);

            PauseContinue.Click += Pause_Click;
        }

        private void Pull_Click(object sender, RoutedEventArgs e)
        {
            PauseContinue.IsEnabled = true;
            Pull.IsEnabled = false;
            Push.IsEnabled = false;

            bodyForwardBtn.IsEnabled = false;
            bodyBackwardBtn.IsEnabled = false;
            bodyLeftBtn.IsEnabled = false;
            bodyRightBtn.IsEnabled = false;
            bodyDownBtn.IsEnabled = false;
            bodyUpBtn.IsEnabled = false;

            string cmd = "cmfj -p=1 -w=-1";
            byte[] sendBytes = System.Text.UnicodeEncoding.UTF8.GetBytes(cmd);
            SendMsg(sendBytes);
        }

        private void Push_Click(object sender, RoutedEventArgs e)
        {
            PauseContinue.IsEnabled = true;
            Pull.IsEnabled = false;
            Push.IsEnabled = false;

            bodyForwardBtn.IsEnabled = false;
            bodyBackwardBtn.IsEnabled = false;
            bodyLeftBtn.IsEnabled = false;
            bodyRightBtn.IsEnabled = false;
            bodyDownBtn.IsEnabled = false;
            bodyUpBtn.IsEnabled = false;

            string cmd = "cmfj -p=0 -w=-1";
            byte[] sendBytes = System.Text.UnicodeEncoding.UTF8.GetBytes(cmd);
            SendMsg(sendBytes);
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            bodyForwardBtn.IsEnabled = true;
            bodyBackwardBtn.IsEnabled = true;
            bodyLeftBtn.IsEnabled = true;
            bodyRightBtn.IsEnabled = true;
            bodyDownBtn.IsEnabled = true;
            bodyUpBtn.IsEnabled = true;

            PauseContinue.Content = "Continue";
            string cmd = "cmfj -c=0";
            byte[] sendBytes = System.Text.UnicodeEncoding.UTF8.GetBytes(cmd);
            SendMsg(sendBytes);

            PauseContinue.Click -= Pause_Click;
            PauseContinue.Click += Continue_Click;
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            bodyForwardBtn.IsEnabled = false;
            bodyBackwardBtn.IsEnabled = false;
            bodyLeftBtn.IsEnabled = false;
            bodyRightBtn.IsEnabled = false;
            bodyDownBtn.IsEnabled = false;
            bodyUpBtn.IsEnabled = false;

            PauseContinue.Content = "Pause";
            string cmd = "cmfj -c=1";
            byte[] sendBytes = System.Text.UnicodeEncoding.UTF8.GetBytes(cmd);
            SendMsg(sendBytes);

            PauseContinue.Click -= Continue_Click;
            PauseContinue.Click += Pause_Click;
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            Start.IsEnabled = true;
            Pull.IsEnabled = false;
            Push.IsEnabled = false;
            PauseContinue.IsEnabled = false;
            Stop.IsEnabled = false;

            bodyForwardBtn.IsEnabled = false;
            bodyBackwardBtn.IsEnabled = false;
            bodyLeftBtn.IsEnabled = false;
            bodyRightBtn.IsEnabled = false;
            bodyDownBtn.IsEnabled = false;
            bodyUpBtn.IsEnabled = false;

            string cmd = "cmfj -s=1";
            byte[] sendBytes = System.Text.UnicodeEncoding.UTF8.GetBytes(cmd);
            SendMsg(sendBytes);

            PauseContinue.Content = "Pause";
            PauseContinue.Click -= Pause_Click;
            PauseContinue.Click -= Continue_Click;
        }

        private void mvBody_Holding(object sender, HoldingRoutedEventArgs e)
        {
            string direction = "";
            Button btn = sender as Button;
            if(e.HoldingState==Windows.UI.Input.HoldingState.Started)
            {
                switch (btn.Name.ToString())
                {
                    case "bodyForwardBtn":
                        direction = " -w=-1";
                        break;
                    case "bodyBackwardBtn":
                        direction = " -w=1";
                        break;
                    case "bodyLeftBtn":
                        direction = " -u=-1";
                        break;
                    case "bodyRightBtn":
                        direction = " -u=1";
                        break;
                    case "bodyUpBtn":
                        direction = " -v=1";
                        break;
                    case "bodyDownBtn":
                        direction = " -v=-1";
                        break;
                }
            }
            string cmd = "cmfj" + direction;
            byte[] sendBytes = System.Text.UnicodeEncoding.UTF8.GetBytes(cmd);
            SendMsg(sendBytes);
        }

        private void mvBody_Tapped(object sender, TappedRoutedEventArgs e)
        {
            mvBodyUpdateCmd(sender as Button, "Tapped");
        }

        private void mvBody_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            mvBodyUpdateCmd(sender as Button, "RightTapped");
        }

        private void mvBodyUpdateCmd(Button btn, String gesture)
        {
            string distance;
            string angle;
            string direction;
            string rollDir = "";
            string mvType;

            string btnName = btn.Name.ToString();

            switch (gesture)
            {
                case "Tapped":
                    distance = moveTarget.SelectedIndex == 0 ? "0.01" : "0.1";
                    angle = "1";
                    break;
                case "RightTapped":
                    distance = moveTarget.SelectedIndex == 0 ? "0.05" : "0.2";
                    angle = "5";
                    break;
                default:
                    distance = "";
                    angle = "";
                    break;
            }

            switch (btnName)
            {
                case "bodyForwardBtn":
                    direction = " -w=-";
                    break;
                case "bodyBackwardBtn":
                    direction = " -w=";
                    break;
                case "bodyLeftBtn":
                    direction = " -u=-";
                    break;
                case "bodyRightBtn":
                    direction = " -u=";
                    break;
                case "bodyUpBtn":
                    direction = " -v=";
                    break;
                case "bodyDownBtn":
                    direction = " -v=-";
                    break;
                case "bodyMR1Btn":
                    direction = " -v=-";
                    rollDir = " -r=";
                    break;
                case "bodyMR2Btn":
                    direction = " -v=-";
                    rollDir = " -r=-";
                    break;
                case "bodyMR3Btn":
                    direction = " -v=";
                    rollDir = " -r=-";
                    break;
                case "bodyMR4Btn":
                    direction = " -v=";
                    rollDir = " -r=";
                    break;
                default:
                    direction = "";
                    break;
            }

            switch(moveTarget.SelectedIndex)
            {
                case 0:
                    mvType = "mr";
                    break;
                case 1:
                    mvType = "move2 -c=lf";
                    break;
                case 2:
                    mvType = "move2 -c=rf";
                    break;
                default:
                    mvType = "mr";
                    break;
            }

            string mvParam;
            if (btnName.Contains("bodyMR"))
            {
                mvParam = direction + distance + rollDir + angle;
            }
            else
            {
                mvParam = direction + distance;
            }
            command.Text = mvType + mvParam;
        }

        /*将控件行为解析为wk命令*/

        private void wk_Tapped(object sender, TappedRoutedEventArgs e)
        {
            wkUpdateCmd(sender as Button, "Tapped");
        }

        private void wkUpdateCmd(Button btn, String gesture)
        {
            string wkParam = " -n=" + wkNum.Value.ToString();
            string wkCmd;
            string btnName = btn.Name.ToString();
            switch (btnName)
            {
                case "wkForwardBtn":
                    wkCmd = "wk -d=" + wkDistance + wkParam;
                    break;
                case "wkBackwardBtn":
                    wkCmd = "wk -d=-" + wkDistance + wkParam;
                    break;
                case "wkLeftBtn":
                    wkCmd = "wk -d=" + wkDistance + " -a=1.57" + wkParam;
                    break;
                case "wkRightBtn":
                    wkCmd = "wk -d=" + wkDistance + " -a=-1.57" + wkParam;
                    break;
                case "wkClockwiseBtn":
                    wkCmd = "wk -d=0 -b=-0.2618" + wkParam;
                    break;
                case "wkCounterclockwiseBtn":
                    wkCmd = "wk -d=0 -b=0.2618" + wkParam;
                    break;
                case "Pull1":
                    wkCmd = "wk -d=0.26105 -a=3.0107";
                    break;
                case "Pull2":
                    wkCmd = "wk -d=0.26105 -a=2.7489";
                    break;
                case "Pull3":
                    wkCmd = "wk -d=0.26105 -a=2.4871";
                    break;
                default:
                    wkCmd = "wk -d=0";
                    break;
            }
            command.Text = wkCmd;
        }

        private void moveTarget_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox combo = sender as ComboBox;

            //Move Body
            if (combo.SelectedIndex == 0)
            {
                //Set mvbody button states
                bodyForwardBtn.IsEnabled = false;
                bodyBackwardBtn.IsEnabled = false;
                bodyLeftBtn.IsEnabled = false;
                bodyRightBtn.IsEnabled = false;
                bodyDownBtn.IsEnabled = false;
                bodyUpBtn.IsEnabled = false;

                //Set cmfj button visibility
                Start.Visibility = Visibility.Visible;
                Pull.Visibility = Visibility.Visible;
                Push.Visibility = Visibility.Visible;
                PauseContinue.Visibility = Visibility.Visible;
                Stop.Visibility = Visibility.Visible;

                //Set Button EventHandler
                Start.Click += Start_Click;
                Pull.Click += Pull_Click;
                Push.Click += Push_Click;
                Stop.Click += Stop_Click;

                bodyForwardBtn.Holding += mvBody_Holding;
                bodyBackwardBtn.Holding += mvBody_Holding;
                bodyLeftBtn.Holding += mvBody_Holding;
                bodyRightBtn.Holding += mvBody_Holding;
                bodyUpBtn.Holding += mvBody_Holding;
                bodyDownBtn.Holding += mvBody_Holding;

                bodyForwardBtn.Tapped -= mvBody_Tapped;
                bodyForwardBtn.RightTapped -= mvBody_RightTapped;
                bodyBackwardBtn.Tapped -= mvBody_Tapped;
                bodyBackwardBtn.RightTapped -= mvBody_RightTapped;
                bodyLeftBtn.Tapped -= mvBody_Tapped;
                bodyLeftBtn.RightTapped -= mvBody_RightTapped;
                bodyRightBtn.Tapped -= mvBody_Tapped;
                bodyRightBtn.RightTapped -= mvBody_RightTapped;
                bodyUpBtn.Tapped -= mvBody_Tapped;
                bodyUpBtn.RightTapped -= mvBody_RightTapped;
                bodyDownBtn.Tapped -= mvBody_Tapped;
                bodyDownBtn.RightTapped -= mvBody_RightTapped;
            }
            //Move Single Leg
            else
            {
                //Set mvbody button states
                bodyForwardBtn.IsEnabled = true;
                bodyBackwardBtn.IsEnabled = true;
                bodyLeftBtn.IsEnabled = true;
                bodyRightBtn.IsEnabled = true;
                bodyDownBtn.IsEnabled = true;
                bodyUpBtn.IsEnabled = true;

                //Set cmfj button visibility
                Start.Visibility = Visibility.Collapsed;
                Pull.Visibility = Visibility.Collapsed;
                Push.Visibility = Visibility.Collapsed;
                PauseContinue.Visibility = Visibility.Collapsed;
                Stop.Visibility = Visibility.Collapsed;

                //Set Button EventHandler
                Start.Click -= Start_Click;
                Pull.Click -= Pull_Click;
                Push.Click -= Push_Click;
                Stop.Click -= Stop_Click;

                bodyForwardBtn.Holding -= mvBody_Holding;
                bodyBackwardBtn.Holding -= mvBody_Holding;
                bodyLeftBtn.Holding -= mvBody_Holding;
                bodyRightBtn.Holding -= mvBody_Holding;
                bodyUpBtn.Holding -= mvBody_Holding;
                bodyDownBtn.Holding -= mvBody_Holding;

                bodyForwardBtn.Tapped += mvBody_Tapped;
                bodyForwardBtn.RightTapped += mvBody_RightTapped;
                bodyBackwardBtn.Tapped += mvBody_Tapped;
                bodyBackwardBtn.RightTapped += mvBody_RightTapped;
                bodyLeftBtn.Tapped += mvBody_Tapped;
                bodyLeftBtn.RightTapped += mvBody_RightTapped;
                bodyRightBtn.Tapped += mvBody_Tapped;
                bodyRightBtn.RightTapped += mvBody_RightTapped;
                bodyUpBtn.Tapped += mvBody_Tapped;
                bodyUpBtn.RightTapped += mvBody_RightTapped;
                bodyDownBtn.Tapped += mvBody_Tapped;
                bodyDownBtn.RightTapped += mvBody_RightTapped;
            }

        }

    }
}
