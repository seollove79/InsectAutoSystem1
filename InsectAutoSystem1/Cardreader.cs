using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InsectAutoSystem1
{
    class Cardreader
    {
        private SerialPort serialPort = new SerialPort();
        private string serialPortName;
        private ShowMessageDelegate showMessageDelegate;
        private string cardNumber;

        public Cardreader(string strSerialPortName, ShowMessageDelegate del)
        {
            showMessageDelegate = del;
            serialPortName = strSerialPortName;
        }

        public void setSerialPort()
        {
            serialPort.PortName = serialPortName;  // TODO: 장치이름으로 자동으로 잡히게 해야 한다.
            serialPort.BaudRate = 9600;
            serialPort.DataBits = 8;
            serialPort.StopBits = StopBits.One;
            serialPort.Parity = Parity.None;
            serialPort.DataReceived += new SerialDataReceivedEventHandler(serialPort_DataReceived);
            serialPort.Open();

            if (serialPort.IsOpen)
            {
                showMessageDelegate("카드리더가 연결되었습니다.\r\n");
            }
            else
            {
                showMessageDelegate("카드리더 연결에 실패하였습니다.\r\n");
            }
        }

        private void serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)  //수신 이벤트가 발생하면 이 부분이 실행된다.
        {

        }

        private void read()
        {

        }
    }
}
