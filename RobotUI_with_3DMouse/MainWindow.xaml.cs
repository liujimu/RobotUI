using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net;
using System.Net.Sockets;

namespace RobotUI_with_3DMouse
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        //timer
        System.Windows.Threading.DispatcherTimer timer;
        int counter;

        //used for socket
        const string sensorServer = "192.168.40.164";
        const int sensorPort = 8000;

        TcpClient sensorClient;
        NetworkStream sensorStream;
        TcpClient controlClient;
        NetworkStream controlStream;

        private bool isConnected = false;
        private bool isClosing = false;

        //used for controlling robot
        bool isMouseEnabled = false;
        const int WALK_COUNT = 2000;
        bool isWalking = false;
        int walkingSteps = 0;

        enum MOVE_DIRECTION
        {
            DAMAND_ERROR = 999,
            STOP = 1000,
            LEFT_FORWARD,
            LEFT,
            LEFT_BACKWARD,
            FORWARD,
            BACKWARD,
            RIGNT_FORWARD,
            RIGHT,
            RIGHT_BACKWARD,
            TURN_LEFT,
            TURN_RIGHT,
            UPWARD,
            DOWNWARD
        };
        MOVE_DIRECTION eMoveDirection = MOVE_DIRECTION.STOP;
        struct FORCE_DATA
        {
            public float Xp;
            public float Yp;
            public float Zp;
            public float Arfa;
            public float Beita;
            public float Gama;
        }
        FORCE_DATA forceData = new FORCE_DATA();
        string mvType;
        

        public MainWindow()
        {
            InitializeComponent();
            timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            timer.Tick += Timer_Tick;

            counter = 0;

            float pi = 3.1415926f;
            StatusText.Text = pi.ToString();
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            if (isConnected)
            {
                StatusText.Text = "Already connected";
                return;
            }
            try
            {
                StatusText.Text = "Trying to connect ...";
                controlClient = new TcpClient(ServerIP.Text, int.Parse(ControlPort.Text));
                sensorClient = new TcpClient(sensorServer, sensorPort);
                isConnected = true;
                StatusText.Text = "Already connected";
                controlStream = controlClient.GetStream();
                sensorStream = sensorClient.GetStream();
                //注册按钮事件
                sendCmdBtn.Click += sendCmd_Click;
                mouseControlBtn.Click += MouseControlBtn_Click;
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.ToString());
                return;
            }
        }

        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            controlStream.Close();
            sensorStream.Close();
            controlClient.Close();
            sensorClient.Close();
            isConnected = false;
        }

        private void moveType_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton rb = sender as RadioButton;
            mvType = rb.Content.ToString();
        }

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="sData"></param>
        private void SendMsg(byte[] sData)
        {
            if (!isConnected)
            {
                StatusText.Text = "Must be connected to send!";
                return;
            }

            try
            {
                byte[] sendData = ConvSendMsg(sData, 0);
                controlStream.Write(sendData, 0, sendData.Length);
            }
            catch (ArgumentNullException e)
            {
                MessageBox.Show("ArgumentNullException: " + e.ToString());
            }
            catch (SocketException e)
            {
                MessageBox.Show("SocketException: " + e.ToString());
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
        private void sendCmd_Click(object sender, RoutedEventArgs e)
        {
            byte[] Pm = System.Text.UnicodeEncoding.UTF8.GetBytes(command.Text);
            SendMsg(Pm);
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
            string commonCmd;
            string btnContent = btn.Content.ToString();
            switch (btnContent)
            {
                case "Enable":
                    commonCmd = "en -a";
                    break;
                case "Disable":
                    commonCmd = "ds -a";
                    break;
                case "GoHome1":
                    commonCmd = "hm -f";
                    break;
                case "GoHome2":
                    commonCmd = "hm -s";
                    break;
                case "Recover1":
                    commonCmd = "rc -f";
                    break;
                case "Recover2":
                    commonCmd = "rc -s";
                    break;
                case "ResetOrigin":
                    commonCmd = "ro";
                    break;
                case "FastWalk":
                    commonCmd = "fw -n=1";
                    break;
                default:
                    commonCmd = "";
                    break;
            }
            byte[] sendBytes = System.Text.UnicodeEncoding.UTF8.GetBytes(commonCmd);
            SendMsg(sendBytes);
        }

        private async void Timer_Tick(object sender, EventArgs e)
        {
            this.counter++;
            await GetForceData(mvType);
            
        }

        private async Task GetForceData(string sendMsg)
        {
            //发数据
            byte[] sendData = System.Text.UnicodeEncoding.UTF8.GetBytes(sendMsg);
            byte[] writeBuffer = new byte[sendData.Length + 1];
            Array.Copy(sendData, 0, writeBuffer, 0, sendData.Length);
            sensorStream.Write(writeBuffer, 0, writeBuffer.Length);

            byte[] readBuffer;
            //读数据长度
            readBuffer = new byte[4];
            await sensorStream.ReadAsync(readBuffer,0,readBuffer.Length);
            int dataLength = System.BitConverter.ToInt32(readBuffer, 0);
            //读数据内容
            readBuffer = new byte[dataLength];
            await sensorStream.ReadAsync(readBuffer, 0, readBuffer.Length);
            int mvDirection = System.BitConverter.ToInt32(readBuffer, 0);
            eMoveDirection = (MOVE_DIRECTION)mvDirection;

            forceData.Xp= System.BitConverter.ToSingle(readBuffer, 4);
            forceData.Yp = System.BitConverter.ToSingle(readBuffer, 8);
            forceData.Zp = System.BitConverter.ToSingle(readBuffer, 12);
            forceData.Arfa = System.BitConverter.ToSingle(readBuffer, 16);
            forceData.Beita = System.BitConverter.ToSingle(readBuffer, 20);
            forceData.Gama = System.BitConverter.ToSingle(readBuffer, 24);
            FxLabel.Content = forceData.Xp.ToString();
            FyLabel.Content = forceData.Yp.ToString();
            FzLabel.Content = forceData.Zp.ToString();
            MxLabel.Content = forceData.Arfa.ToString();
            MyLabel.Content = forceData.Beita.ToString();
            MzLabel.Content = forceData.Gama.ToString();

            StatusText.Text = eMoveDirection.ToString();
        }

        private void MouseControlBtn_Click(object sender, RoutedEventArgs e)
        {
            if(!isMouseEnabled)
            {
                timer.Start();
                mouseControlBtn.Content = "Stop 6-DOFs-Mouse Control";
            }
            else
            {
                timer.Stop();
                mouseControlBtn.Content = "Start 6-DOFs-Mouse Control";
            }
            isMouseEnabled = !isMouseEnabled;
        }
    }
}