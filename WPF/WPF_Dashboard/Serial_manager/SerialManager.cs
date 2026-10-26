using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPF_Dashboard.Serial_manager
{
    public class SerialManager
    {
        static bool serial_open;
        static bool serial_config;
        static SerialPort _serialPort = new SerialPort();
        StringBuilder sb = new StringBuilder();
        private Action<byte[], int> RxRecived_CallbackFunc = null;
        public bool logwrite = false;
        // Event notifies the main class when data arrives
        public event Action<string> DataReceived;

        public void setSerialPort(string port, int baudrate, string Parity, int databit, int stopbit)
        {
            // _serialPort = new SerialPort();

            // Allow the user to set the appropriate properties.
            _serialPort.Close();

            _serialPort.PortName = port;

            _serialPort.BaudRate = baudrate;
            _serialPort.Parity = (Parity)Enum.Parse(typeof(Parity), Parity);
            _serialPort.DataBits = databit;
            _serialPort.StopBits = StopBits.One;
            _serialPort.Handshake = Handshake.None;
            // Set the read/write timeouts  
            _serialPort.ReadTimeout = 3500;
            _serialPort.WriteTimeout = 3500;

            if (_serialPort.IsOpen == true)
            {
                _serialPort.Close();
                _serialPort.DataReceived += OnDataReceived;
            }
            else { _serialPort.Open(); }
            // _serialPort.Open();
            serial_set(true);
            serial_config = true;


        }

        private void OnDataReceived(
         object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string json = _serialPort.ReadLine().Trim();

                // Notify all subscribed classes
                DataReceived?.Invoke(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private SerialErrorReceivedEventHandler _serialPort_ErrorReceived(Action<object, SerialErrorReceivedEventArgs> scanningPort_ErrorReceived)
        {
            throw new NotImplementedException();
        }

        private void ErrorReceivedHandler(object sender, SerialErrorReceivedEventArgs e)
        {
            //Form1 form1 = new Form1();
            //form1.disconnectfun();
        }



        public void setCallback(Action<byte[], int> callBackFunc)
        {
            RxRecived_CallbackFunc = callBackFunc;
        }

        public void closeSerialPort()
        {
            _serialPort.Close();
            serial_set(false);
        }

        public void openSerialPort()
        {
            _serialPort.Open();
            serial_set(true);
        }

        public void start_serial()
        {
            if (!isSerialOpen() && (serial_config == true))
            {
                openSerialPort();
            }

        }

        public void toggle_serial()
        {
            if (!isSerialOpen() && (serial_config == true))
            {
                openSerialPort();
                //parent.toolStrip1.Items[1].Image = Properties.Resources.Stop_16x;
            }
            else if (isSerialOpen() && (serial_config == true))
            {
                closeSerialPort();
                //parent.toolStrip1.Items[1].Image = Properties.Resources.Action_16x;
            }
        }
        public string Serialconnect()
        {
            try
            {
                if (_serialPort.IsOpen)
                {
                    return "Allready Connect";
                }
                else
                {
                    _serialPort.Open();
                    return "Connected";
                }

            }
            catch
            { return "Not Avaliable"; }
        }

        public bool Serialstatus()
        {
            try
            {
                if (_serialPort.IsOpen)
                {
                    return true;
                }
                else
                {
                    _serialPort.Open();
                    return true;
                }

            }
            catch
            { return false; }
        }
        public string Serialdisconnect()
        {
            try
            {
                if (_serialPort.IsOpen)
                {
                    _serialPort.Close();
                    return "Disconnected";
                }
                else
                {
                    _serialPort.Close();
                    return "Disconnected";
                }

            }
            catch (Exception ex)
            { return ex.Message.ToString(); }
        }

        public string Read_serial(string msg)
        {

            try
            {
                if (!_serialPort.IsOpen)
                { _serialPort.Open(); }
                if (_serialPort.IsOpen)
                {
                    _serialPort.WriteLine(msg);
                    string message = string.Empty;


                    //System.Threading.Thread.Sleep(500);

                    //message = _serialPort.ReadExisting();


                    message = _serialPort.ReadLine().Trim();
                    //int bytes = _serialPort.BytesToRead;  //find the size of the array needed
                    //System.Threading.Thread.Sleep(500);
                    //byte[] buffer = new byte[bytes];  //create the array
                    //_serialPort.Read(buffer, 0, bytes);  //read the message and save it into the buffer
                    //byte[] ReceivedMessage = new byte[bytes];  //create an array of the same size
                    //Array.Copy(buffer, ReceivedMessage, bytes);  //copy it across

                    //System.Threading.Thread.Sleep(500);
                    //message = Encoding.Default.GetString(ReceivedMessage);
                    //System.Threading.Thread.Sleep(500);

                    message = message.Replace(System.Environment.NewLine, "");
                    //logrs485(msg, message);
                    return message;
                }
                else { return "COM Port Not avaliable"; }
            }
            //catch (TimeoutException xt  Exception ex)
            catch (Exception ex)
            {
                //logrs485(msg, ex.Message.ToString());
                return ex.Message.ToString();
            }

        }

        public string writehex()
        {
            if (_serialPort.IsOpen)
            {
                byte[] bytestosend = { 0xC5, 0x5C, 0xAA, 0x55, 0x02, 0x00, 0x00, 0x6a, 0x0D };

                _serialPort.Write(bytestosend, 0, bytestosend.Length);
                System.Threading.Thread.Sleep(500);
                string r = _serialPort.ReadByte().ToString();
                return r;
            }
            return "Serial not working";
        }

        public string Read()
        {

            try
            {
                string message = _serialPort.ReadLine().Trim();
                System.Threading.Thread.Sleep(500);
                return message;
            }
            catch (TimeoutException) { return ""; }


        }
        public string write_serialascii(byte[] msg)
        {
            string message = string.Empty;

            if (_serialPort.IsOpen)
            {
                //_serialPort.Open();
                _serialPort.Write(msg, 0, msg.Length);

                //System.Threading.Thread.Sleep(500);

                //message = _serialPort.ReadExisting();
                int bytes = _serialPort.BytesToRead;  //find the size of the array needed
                                                      // System.Threading.Thread.Sleep(500);
                byte[] buffer = new byte[bytes];  //create the array
                                                  // System.Threading.Thread.Sleep(500);
                _serialPort.Read(buffer, 0, bytes);  //read the message and save it into the buffer
                byte[] ReceivedMessage = new byte[bytes];  //create an array of the same size
                Array.Copy(buffer, ReceivedMessage, bytes);  //copy it across

                //System.Threading.Thread.Sleep(500);
                message = Encoding.Default.GetString(ReceivedMessage);
                message = message.Replace(@"\r\n", "");
                return message;

            }
            else { return "COM Port Not avaliable"; }
        }
        public string write_serial(string msg)
        {
            if (_serialPort.IsOpen)
            {
                string da = Read_serial(msg);
                //System.Threading.Thread.Sleep(500);

                return da;
            }
            else { return ""; }
        }

        public void stop_serial()
        {
            if (isSerialOpen() && (serial_config == true))
            {
                closeSerialPort();
            }
        }
        public string serialstatus()
        {
            bool val = _serialPort.IsOpen;
            return val.ToString();
        }
        public void serial_init()
        {
            serial_open = false;
            serial_config = false;
        }

        bool isSerialOpen()
        {
            return serial_open;
        }

        private void serial_set(bool flag)
        {
            serial_open = flag;
        }

        // called when data arrives at the serial port
        // broken packets are not yet handled.
        private void port_DataReceived(object sender,
                    SerialDataReceivedEventArgs e)
        {

            int bytes = _serialPort.BytesToRead;
            if (bytes > 0)
            {
                byte[] buffer = new byte[bytes];

                _serialPort.Read(buffer, 0, bytes);


                sb.Clear();
                foreach (byte b in buffer)
                    sb.Append(b.ToString("X2"));

                string hexString = sb.ToString();


                //Console.WriteLine(hexString);
                //updateLog(hexString);
                //Callback(hexString);
                if (null != RxRecived_CallbackFunc)
                {
                    RxRecived_CallbackFunc(buffer, hexString.Length);

                }
            }
        }

    }
}
