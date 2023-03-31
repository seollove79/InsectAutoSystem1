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
        Thread scaleThread;
        Thread getWeightThread;
        Thread feedThread;
        Thread runThread;

        private bool scaleConnectCheck;
        private float weight;
        private String controllerData;
        private double targetFeedWeight = 3;
        private String rfidCode;
        private bool runThreadEnable = false;
        private bool motorRun = false;

        public Form1()
        {
            InitializeComponent();
            getWeightThread = new Thread(refreshWeight);
            scaleThread = new Thread(readScale);
            feedThread = new Thread(feed);
            runThread = new Thread(run);
            scaleConnectCheck = false;
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

        private void monitorControllerData(String strData)
        {
            var responseValues = strData.Split(',');
            if (Int32.Parse(responseValues[3])==1) //센서1에 물체가 감지되면
            {
                if (DeviceState.getFeedState() == DeviceState.FeedState.None)
                {
                    DeviceState.setFeedState(DeviceState.FeedState.NewBox);
                }
            }
            else if(Int32.Parse(responseValues[3]) == 0)
            {
                DeviceState.setFeedState(DeviceState.FeedState.None);
            }

/*            if (Int32.Parse(responseValues[4]) == 1) //센서2에 물체가 감지되면
            {
                runThread.Start();
            }
            else if (Int32.Parse(responseValues[4]) == 0)
            {
                runThread.Abort();
            }*/
        }

        private void setSerialPort()
        {
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Caption like '%(COM%'"))
            {
                var portnames = SerialPort.GetPortNames();
                var ports = searcher.Get().Cast<ManagementBaseObject>().ToList().Select(p => p["Caption"].ToString());
                cbScalePort.DataSource = portnames.Select(n => n + " - " + ports.FirstOrDefault(s => s.Contains(n))).ToList();
                cbControlPort.DataSource = portnames.Select(n => n + " - " + ports.FirstOrDefault(s => s.Contains(n))).ToList();
            }
        }

        private void readScale()
        {
            scale.setSerialPort();
        }

        private void btnSnapshot_Click(object sender, EventArgs e)
        {
            camera.makeSnapshot();
        }

        private void showVideoFrame(Bitmap videoFrame)
        {
            if (pictureBox1.Image != null)
            {
                pictureBox1.Image.Dispose();
            }
            pictureBox1.Image = videoFrame;
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
            }));
        }

        private void btnConnectScale_Click(object sender, EventArgs e)
        {
            ShowMessageDelegate del = showMessage;
            string str = (cbScalePort.Text).Split('-')[0];
            str = str.Replace(" ", "");
            scale = new Scale(str, del);
            scaleThread.Start();
            getWeightThread.Start();
        }

        private void refreshWeight()
        {
            while (true)
            {
                weight = scale.getWeight();
                this.Invoke(new Action(delegate () {
                    tbWeight.Text = weight.ToString();
                }));
            }
        }

        private void Form1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar != '#')
            {
                rfidCode += e.KeyChar;
            }
            else
            {
                if (rfidCode != tbBoxCode.Text) {
                    tbBoxCode.Text = rfidCode;
                }
                rfidCode = "";
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            motorRun = false;
            controller.sendCommand("motor_stop");
            controller.sendCommand("shuttle_stop");
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
            if (!runThread.IsAlive) { 
                runThread.Start();
            }
            if (!feedThread.IsAlive)
            {
                feedThread.Start();
            }
        }

        private void feed()
        {
            while (true)
            {
                Console.WriteLine(DeviceState.getFeedState());
                if (DeviceState.getFeedState() == DeviceState.FeedState.NewBox)
                {
                    controller.sendCommand("shuttle_run");
                    DeviceState.setFeedState(DeviceState.FeedState.Feeding);
                    while (weight < targetFeedWeight && DeviceState.getFeedState() == DeviceState.FeedState.Feeding)
                    {

                    }
                    controller.sendCommand("shuttle_stop");
                    DeviceState.setFeedState(DeviceState.FeedState.Full);
                    DeviceState.setFeedState(DeviceState.FeedState.End);
                }
                }
        }

        private void run()
        {
            while (true)
            {
                if (motorRun==true && (DeviceState.getFeedState() == DeviceState.FeedState.None || DeviceState.getFeedState() == DeviceState.FeedState.End))
                {
                    controller.sendCommand("motor_run");
                    Console.WriteLine("모터런 신호 줬다.");
                }
                Thread.Sleep(2000);
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if(scaleThread.IsAlive) { 
                scaleThread.Abort();
            }
            if(getWeightThread.IsAlive)
            {
                getWeightThread.Abort();
            }
            if(feedThread.IsAlive)
            {
                feedThread.Abort();
            }
            if(runThread.IsAlive)
            {
                runThread.Abort();
            }
        }
    }
}
