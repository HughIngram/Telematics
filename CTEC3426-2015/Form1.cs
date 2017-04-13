using System;
using System.Threading;
using System.Windows.Forms;
using System.IO.Ports;
using System.IO;

namespace CTEC3426_2015
{

    /**
    This class contains Serial port interaction code, and MVP 'presenter' code
        Do not add any 'Model' code.
    **/
    public partial class CTEC3426 : Form
    {
        delegate void SetTextCallback(string text);

        private Thread readThread = null;
        bool reading = false;

        public SerialPort serialPort = new SerialPort();

        /* Default settings */
        string _PortName = "COM1";
        int _BaudRate = 115200;
        int _DataBits = 8;
        StopBits _StopBits = StopBits.One;
        Parity _Parity = Parity.None;

        // The App Model
        ApplicationModel model;

        public CTEC3426()
        {
            InitializeComponent();
            initMenu();
            model = new ApplicationModel(this);
        }

        private void GUI_FormClosing(object sender, FormClosingEventArgs e)
        {
            reading = false;
            this.readThread.Abort();
            serialPort.Close();
        }

        /* Initialise the toolbar menu */
        public void initMenu() {
            foreach (string s in SerialPort.GetPortNames())
            {
                this.portNameComboBox.Items.Add(s);
            }
            foreach (string s in Enum.GetNames(typeof(Parity)))
            {
                this.parityComboBox.Items.Add(s);
            }
            foreach (string s in Enum.GetNames(typeof(StopBits)))
            {
                this.stopBitsComboBox.Items.Add(s);
            }
            this.portNameComboBox.Text = _PortName;
            this.baudRateTextBox.Text = _BaudRate.ToString();
            this.dataBitsTextBox.Text = _DataBits.ToString();
            this.stopBitsComboBox.Text = _StopBits.ToString();
            this.parityComboBox.Text = _Parity.ToString();
        }

        /* Methods related to each entry in the toolbar menu */
        private void connectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                setSerialPort();
                serialPort.Open();
            }
            catch (IOException open_exception)
            {
                Console.WriteLine("an error occured when opening the serial port");
            }
            this.readThread = new Thread(new ThreadStart(this.readThreadProcSafe));
            this.readThread.Start();
            reading = true;
            sendCommand(serialPort, "@");
        }

        private void portNameComboBox_TextChanged(object sender, EventArgs e)
        {
            _PortName = portNameComboBox.Text;
        }

        private void baudRateTextBox_TextChanged(object sender, EventArgs e)
        {
            _BaudRate = Convert.ToInt32(baudRateTextBox.Text);
        }

        private void dataBitsTextBox_TextChanged(object sender, EventArgs e)
        {
            _DataBits = Convert.ToInt32(dataBitsTextBox.Text);
        }

        private void stopBitsToolStripMenuItem_TextChanged(object sender, EventArgs e)
        {
            _StopBits = (StopBits)Enum.Parse(typeof(StopBits), stopBitsComboBox.Text, true);
        }

        private void parityToolStripMenuItem_TextChanged(object sender, EventArgs e)
        {
            _Parity = (Parity)Enum.Parse(typeof(Parity), parityComboBox.Text, true);
        }

        /* Set values for the serial port */
        private void setSerialPort()
        {
            serialPort.PortName = _PortName;
            serialPort.BaudRate = _BaudRate;
            serialPort.DataBits = _DataBits;
            serialPort.StopBits = _StopBits;
            serialPort.Parity = _Parity;
        }

        /* Thread reading the serial port */
        private void readThreadProcSafe()
        {
            Byte[] data = new Byte[256];
            String line = "";

            while (reading)
            {
                try
                {
                    serialPort.Read(data, 0, data.Length);
                }
                catch (IOException read_exception)
                { }

                for (int i = 0; i < data.Length; i++)
                {
                    switch (data[i])
                    {
                        case (0):
                            break;
                        case (13): // carriage return
                            line += Convert.ToChar(data[i]);
                            if ((line.Contains("#")) && (!line.Contains("send")))
                                this.getCANbusData(line);
                                line = "";
                            break;
                        default:
                            line += Convert.ToChar(data[i]);
                            this.SetTerminal(Convert.ToString(Convert.ToChar(data[i])));
                            break;
                    }
                }
                Array.Clear(data, 0, data.Length);
            }
        }

        /* Add the line read on the serial port to the terminal window
        in a thread safe way */
        private void SetTerminal(string text)
        {
            if (this.terminal.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(SetTerminal);
                this.Invoke(d, new object[] { text });
            }
            else
            {
                terminal.AppendText(text);
            }
        }

        /* Sends the characters written in the terminal emulator to the serial port */
        private void terminal_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (serialPort.IsOpen)
            {
                serialPort.Write(Convert.ToString(e.KeyChar));
                e.Handled = true;
            }
        }

        /* Make the data retrieve from the CANbus available to the form */
        private void getCANbusData (string text)
        {
            if (this.InvokeRequired)
            {
                SetTextCallback c = new SetTextCallback(getCANbusData);
                this.Invoke(c, new object[] { text });
            }
            else
            {
                string delimStr = "#";
                char[] delimiter = delimStr.ToCharArray();
                string[] raw = text.Split(delimiter);
                String identifier = raw[1].Remove(8);

                String dataStr = raw[1].Remove(0, 10);
                delimStr = " ";
                delimiter = delimStr.ToCharArray();
                String[] data = dataStr.Split(delimiter);
                // this context is a convenient hook for doing something on loop.
                // code here will be called every time a new remote board status is available.

                // update the incoming data display
                textBoxReading.ResetText();
                textBoxReading.AppendText(dataStr);

                // parse the data into the application state
                model.updateRemoteBoardState(data);

                // now update the GUI
                displayRemoteBoardStatus(model.remoteBoardState);

                // update the temperature set point
                if (setPointRadioButton.Checked && !textBoxTempSetPoint.Text.Equals(""))
                {              
                    String setPoint = textBoxTempSetPoint.Text;
                    // ensure the String has one decimal place
                    setPoint = String.Format("{0:0.0}", Math.Truncate(Double.Parse(setPoint) * 10) / 10);
                    model.tempSetPoint(setPoint);
                }
            }
        }

        // updates the UI's representation of the remote board status.
        // this method should contain 'View' code only
        private void displayRemoteBoardStatus(BoardState remoteBoardState)
        {
            // heater
            textBoxHeaterStatus.ResetText();
            String heaterStatus = getOnOrOff(remoteBoardState.isHeaterOn);
            textBoxHeaterStatus.AppendText(heaterStatus);

            // fan (motor)
            textBoxFanStatus.ResetText();
            textBoxFanStatus.AppendText(remoteBoardState.motorToString());

            // temperature
            textBoxTempDisplay.ResetText();
            textBoxTempDisplay.AppendText(remoteBoardState.temperature);

            // LED's
            textBoxLed0Status.Clear();
            textBoxLed1Status.Clear();
            textBoxLed2Status.Clear();
            textBoxLed3Status.Clear();
            textBoxLed0Status.AppendText(getOnOrOff(remoteBoardState.ledArray[0]));
            textBoxLed1Status.AppendText(getOnOrOff(remoteBoardState.ledArray[1]));
            textBoxLed2Status.AppendText(getOnOrOff(remoteBoardState.ledArray[2]));
            textBoxLed3Status.AppendText(getOnOrOff(remoteBoardState.ledArray[3]));

            // keypad status
            textBoxKeypad0.Clear();
            textBoxKeypad1.Clear();
            textBoxKeypad2.Clear();
            textBoxKeypad3.Clear();
            textBoxKeypad4.Clear();
            textBoxKeypad5.Clear();
            textBoxKeypad6.Clear();
            textBoxKeypad6.Clear();
            textBoxKeypad7.Clear();
            textBoxKeypad8.Clear();
            textBoxKeypad9.Clear();
            textBoxKeypadStar.Clear();
            textBoxKeypadHash.Clear();
            textBoxKeypad0.AppendText(getOnOrOff(remoteBoardState.keypad[0]));
            textBoxKeypad1.AppendText(getOnOrOff(remoteBoardState.keypad[1]));
            textBoxKeypad2.AppendText(getOnOrOff(remoteBoardState.keypad[2]));
            textBoxKeypad3.AppendText(getOnOrOff(remoteBoardState.keypad[3]));
            textBoxKeypad4.AppendText(getOnOrOff(remoteBoardState.keypad[4]));
            textBoxKeypad5.AppendText(getOnOrOff(remoteBoardState.keypad[5]));
            textBoxKeypad6.AppendText(getOnOrOff(remoteBoardState.keypad[6]));
            textBoxKeypad7.AppendText(getOnOrOff(remoteBoardState.keypad[7]));
            textBoxKeypad8.AppendText(getOnOrOff(remoteBoardState.keypad[8]));
            textBoxKeypad9.AppendText(getOnOrOff(remoteBoardState.keypad[9]));
            textBoxKeypadStar.AppendText(getOnOrOff(remoteBoardState.keypad[10]));
            textBoxKeypadHash.AppendText(getOnOrOff(remoteBoardState.keypad[11]));

            // switches
            textBoxSwitch0.Clear();
            textBoxSwitch1.Clear();
            textBoxSwitch2.Clear();
            textBoxSwitch3.Clear();
            textBoxSwitch0.AppendText(getOnOrOff(remoteBoardState.switches[0]));
            textBoxSwitch1.AppendText(getOnOrOff(remoteBoardState.switches[1]));
            textBoxSwitch2.AppendText(getOnOrOff(remoteBoardState.switches[2]));
            textBoxSwitch3.AppendText(getOnOrOff(remoteBoardState.switches[3]));

            updateSmsString();
        }

        private void updateSmsString()
        {
            textboxSmsMessage.Clear();
            textboxSmsMessage.AppendText(model.getStatusString());
        }

        private String getOnOrOff(Boolean b)
        {
            return BoardState.getOnOrOff(b);
        }

        // Writes a command with some optional data to the serial port
        public void sendCommand(SerialPort sp, String command, String payload = null)
        {
            sp.Write(command);
            if (payload != null)
            {
                sp.Write(payload);
            }
        }

        private void buttonIncomingFilterAccept_Click(object sender, EventArgs e)
        {
            model.setUpIncomingFilter(textBoxIncomingId.Text);
        }

        private void buttonOutgoingIdAccept_Click(object sender, EventArgs e)
        {
            model.setUpBroadcastId(textBoxOutgoingId.Text);
        }

        private void radioButtonSetPoint_CheckedChanged(object sender, EventArgs e)
        {
            if (setPointRadioButton.Checked)
            {
                textBoxTempSetPoint.Enabled = true;
                // the fan should always go forward in set point mode
                radioButtonFanForward.Checked = true;
            } else
            {
                textBoxTempSetPoint.Enabled = false;
            }
        }

        private void manualRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            if (manualRadioButton.Checked)
            {
                buttonHeaterToggle.Enabled = true;
                radioButtonFanOff.Enabled = true;
                radioButtonFanForward.Enabled = true;
                radioButtonFanBackward.Enabled = true;
                // reset the fan control to off when exiting set point mode
                radioButtonFanOff.Checked = true;        
            } else
            {
                buttonHeaterToggle.Enabled = false;
                radioButtonFanOff.Enabled = false;
                radioButtonFanForward.Enabled = false;
                radioButtonFanBackward.Enabled = false;
            }
        }

        private void buttonHeaterToggle_Click(object sender, EventArgs e)
        {
            model.toggleHeater();
        }

        private void radioButtonFanForward_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButtonFanForward.Checked && manualRadioButton.Checked) {
                model.fanForward();
            }
        }

        
        private void radioButtonFanBackward_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButtonFanBackward.Checked)
            {
                model.fanReverse();
            }
        }

        private void radioButtonFanOff_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButtonFanOff.Checked)
            {
                model.fanOff();
            }
        }

        private void checkBoxLed0_CheckedChanged(object sender, EventArgs e)
        {
            updateLeds();
        }

        private void checkBoxLed1_CheckedChanged(object sender, EventArgs e)
        {
            updateLeds();
        }

        private void checkBoxLed2_CheckedChanged(object sender, EventArgs e)
        {
            updateLeds();
        }

        private void checkBoxLed3_CheckedChanged(object sender, EventArgs e)
        {
            updateLeds();
        }

        /*
        Sends the state of the check boxes over to the Model
        */
        private void updateLeds()
        {
            Boolean[] values =
                { checkBoxLed0.Checked, checkBoxLed1.Checked, checkBoxLed2.Checked, checkBoxLed3.Checked };
            model.setLeds(values);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            String number = textboxSmsNumber.Text;
            String message = textboxSmsMessage.Text;
            model.sendSms(number, message);
        }

        private void buttonApplyMask_Click(object sender, EventArgs e)
        {
            String mask = textBoxMask.Text;
            model.setUpMask(mask);
        }

    }

}
