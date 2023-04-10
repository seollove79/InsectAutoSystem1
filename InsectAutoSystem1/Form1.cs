using AForge.Video;
using AForge.Video.DirectShow;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;

namespace InsectAutoSystem1
{
    delegate void ShowVideoFrameDelegate(Bitmap videoFrame);
    delegate void ShowMessageDelegate(String Str);
    delegate void MonitorControllerDataDelegate(String strData);
    

    public partial class Form1 : Form
    {
        private Camera camera;
        private Scale scale;
        private Controller controller;
        private Cardreader cardreader;

        private Thread getWeightThread;
        private Thread getDeviceInfoThread;

        private bool scaleConnectCheck;
        private bool cardreaderConnectCheck;
        private float weight;
        private String controllerData;
        private String rfidCode;
        private bool motorRun = false;

        public Form1()
        {
            InitializeComponent();
            getWeightThread = new Thread(refreshWeight);
            getDeviceInfoThread = new Thread(getDeviceInfo);
            scaleConnectCheck = false;
            cardreaderConnectCheck = false;
        }

        private void init()
        {
            //초기화
            DeviceState.setFeedState(DeviceState.FeedState.None);
            weight = 0;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            ShowVideoFrameDelegate del = showVideoFrame;
            ShowMessageDelegate del1 = showMessage;
            camera = new Camera(del, del1);
            setSerialPort();
            init();
        }

        private void getDeviceInfo()
        {
            while (true)
            {
                controller.sendCommand("get_info");
                Thread.Sleep(2000);
            }
        }

        private void monitorControllerData(String strData)
        {
            var responseValues = strData.Split(',');
            if (Int32.Parse(responseValues[3])==1) //센서1에 물체가 감지되면
            {
                if (DeviceState.getFeedState() == DeviceState.FeedState.None) //TODO end도 넣어야 하지 않나?
                {
                    if (weight > DeviceState.targetFeedWeight)
                    {
                        DeviceState.setFeedState(DeviceState.FeedState.End);
                    }
                    else
                    {
                        DeviceState.setFeedState(DeviceState.FeedState.NewBox);
                        feed();
                    }
                }
            }
            else if(Int32.Parse(responseValues[3]) == 0)
            {
                DeviceState.setFeedState(DeviceState.FeedState.None);
            }

            if (DeviceState.getFeedState() == DeviceState.FeedState.End)
            {
                if(Int32.Parse(responseValues[1]) == 0)
                {
                    controller.sendCommand("motor_run");
                }
            }
        }

        private void setSerialPort()
        {
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Caption like '%(COM%'"))
            {
                var portnames = SerialPort.GetPortNames();
                var ports = searcher.Get().Cast<ManagementBaseObject>().ToList().Select(p => p["Caption"].ToString());

                foreach(string port in ports)
                {
                    if (port.Contains("USB Serial Port"))
                    {
                        ShowMessageDelegate del1 = showMessage;
                        MonitorControllerDataDelegate del2 = monitorControllerData;
                        string str = port.Split('(')[1];
                        str = str.Replace(" ", "");
                        str = str.Replace(")", "");
                        controller = new Controller(str, del2, del1);
                    }

                    if (port.Contains("Prolific USB"))
                    {
                        ShowMessageDelegate del = showMessage;
                        string str = port.Split('(')[1];
                        str = str.Replace(" ", "");
                        str = str.Replace(")", "");
                        scale = new Scale(str, del);
                        scale.setSerialPort();
                        getWeightThread.Start();

                    }

                    if (port.Contains("Silicon Labs CP210x USB to UART Bridge"))
                    {
                        ShowMessageDelegate del = showMessage;
                        string str = port.Split('(')[1];
                        str = str.Replace(" ", "");
                        str = str.Replace(")", "");
                        cardreader = new Cardreader(str, del);
                        cardreader.setSerialPort();
                    }
                }

                cbScalePort.DataSource = portnames.Select(n => n + " - " + ports.FirstOrDefault(s => s.Contains(n))).ToList();
                cbControlPort.DataSource = portnames.Select(n => n + " - " + ports.FirstOrDefault(s => s.Contains(n))).ToList();
                cbCardreaderPort.DataSource = portnames.Select(n => n + " - " + ports.FirstOrDefault(s => s.Contains(n))).ToList();
            }
        }

        private void showVideoFrame(Bitmap videoFrame)
        {
            if (pictureBox1.Image != null)
            {
                pictureBox1.Image.Dispose();
            }
            try
            { 
                pictureBox1.Image = videoFrame;
            }
            catch (Exception ex)
            {
                tbLog.Text += ex.Message + "\r\n";
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            camera.clear();
            pictureBox1.Image = null;
            pictureBox1.Invalidate();
            Application.Exit();
        }

        private void showMessage(string str)
        {
            this.Invoke(new Action(delegate () { 
                tbLog.Text += str + "\n";
                if (str == "저울이 연결되었습니다.\r\n")
                {
                    cbScalePort.Enabled = false;
                    btnConnectScale.Enabled = false;
                    scaleConnectCheck = true;
                }
                if (str == "제어기가 연결되었습니다.\r\n")
                {
                    cbControlPort.Enabled = false;
                    btnConnectController.Enabled = false;
                    scaleConnectCheck = true;
                }
                if (str == "카드리더가 연결되었습니다.\r\n")
                {
                    cbCardreaderPort.Enabled = false;
                    btnConnectCardreader.Enabled = false;
                    cardreaderConnectCheck = true;
                }
                if (str == "사육상자 번호를 인식하였습니다.\r\n")
                {
                    tbBoxCode.Text = cardreader.getCardNumber();
                    Thread.Sleep(1000);
                    camera.makeSnapshot(cardreader.getCardNumber());
                    Console.WriteLine("현재시간 : " + DateTime.Now.ToString());
                    Thread.Sleep(1000);
                    controller.sendCommand("motor_run");
                }
            }));
        }

        private void btnConnectScale_Click(object sender, EventArgs e)
        {
            ShowMessageDelegate del = showMessage;
            string str = (cbScalePort.Text).Split('-')[0];
            str = str.Replace(" ", "");
            scale = new Scale(str, del);
            scale.setSerialPort();
            getWeightThread.Start();
        }

        private void refreshWeight()
        {
            while (true)
            {
                weight = scale.getWeight();
                if (DeviceState.getFeedState() == DeviceState.FeedState.Feeding) //셔틀동작하고 있을때
                { 
                    if (weight >= DeviceState.targetFeedWeight)
                    {
                        DeviceState.setFeedState(DeviceState.FeedState.Full);
                        controller.sendCommand("shuttle_stop");
                        controller.sendCommand("motor_run");
                        DeviceState.setFeedState(DeviceState.FeedState.End);
                    }
                }
                this.Invoke(new Action(delegate () {
                    tbWeight.Text = weight.ToString();
                }));
                Thread.Sleep(100);
            }
        }

        private void Form1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 'a' || e.KeyChar == 'A')
            {
                if (motorRun)
                {
                    cardreader.read();
                }
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            motorRun = false;
            controller.sendCommand("motor_stop");
            controller.sendCommand("shuttle_stop");
            btnStart.Enabled = true;
        }

        private void btnConnectController_Click(object sender, EventArgs e)
        {
            ShowMessageDelegate del1 = showMessage;
            MonitorControllerDataDelegate del2 = monitorControllerData;
            string str = (cbControlPort.Text).Split('-')[0];
            str = str.Replace(" ", "");
            controller = new Controller(str, del2, del1);
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            motorRun = true;
            if (!getDeviceInfoThread.IsAlive) 
            {
                getDeviceInfoThread.Start();
            }
            btnStart.Enabled = false;
        }

        private void feed()
        {
            if (DeviceState.getFeedState() == DeviceState.FeedState.NewBox)
            {
                DeviceState.setFeedState(DeviceState.FeedState.Feeding);
                controller.sendCommand("shuttle_run");
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if(getWeightThread.IsAlive)
            {
                getWeightThread.Abort();
            }

            if (getDeviceInfoThread.IsAlive)
            {
                getDeviceInfoThread.Abort();
            }
        }
    }
}
