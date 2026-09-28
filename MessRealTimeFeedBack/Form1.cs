using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace MessRealTimeFeedBack
{
    public partial class Form1 : Form
    {
        private ResultFeedBackEmployee resultForm;

        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["EmployeeFeedbackDB"].ConnectionString;

        private TextBox rfidTextBox;
        private SerialPort rfidPort;

        private const int RFID_BAUD_RATE = 9600;

        private const int INITIAL_RETRY_COUNT = 3;
        private const int RETRY_DELAY_MS = 1500;
        private const int RECONNECT_INTERVAL_MS = 5000;

        private Timer rfidReconnectTimer;


        private readonly StringBuilder rfidBuffer = new StringBuilder();

        private bool isConnecting = false;
        private bool formClosing = false;

        public Form1()
        {
            InitializeComponent();

            SetFeedbackResetTimer();

            rfidTextBox = new TextBox
            {
                Visible = false,
                TabStop = false
            };

            this.Controls.Add(rfidTextBox);

            InitializeRFIDScanner();

            rfidReconnectTimer = new Timer
            {
                Interval = RECONNECT_INTERVAL_MS
            };

            rfidReconnectTimer.Tick += RfidReconnectTimer_Tick;
            rfidReconnectTimer.Start();
        }



        private void InitializeRFIDScanner()
        {
            if (formClosing)
                return;

            if (isConnecting)
                return;

            isConnecting = true;

            try
            {
                for (int attempt = 1; attempt <= INITIAL_RETRY_COUNT; attempt++)
                {
                    if (formClosing)
                        return;

                    CloseRfidPort();

                    string[] availablePorts;

                    try
                    {
                        availablePorts = SerialPort.GetPortNames();
                    }
                    catch
                    {
                        availablePorts = new string[0];
                    }

                    if (availablePorts.Length == 0)
                    {

                        return;
                    }

                    var portsToTry = GetPortsToTry(availablePorts);

                    foreach (string portName in portsToTry)
                    {
                        if (formClosing)
                            return;

                        string error;
                        bool accessDenied;

                        bool connected = TryOpenPort(
                            portName,
                            out error,
                            out accessDenied);

                        if (connected)
                        {
                            return;
                        }
                    }

                    if (attempt < INITIAL_RETRY_COUNT)
                    {
                        System.Threading.Thread.Sleep(RETRY_DELAY_MS);
                    }
                }
            }
            finally
            {
                isConnecting = false;
            }
        }

        private System.Collections.Generic.List<string> GetPortsToTry(
            string[] availablePorts)
        {
            var portsToTry =
                new System.Collections.Generic.List<string>();

            string configuredPort = null;

            try
            {
                configuredPort =
                    ConfigurationManager.AppSettings["RfidComPort"];
            }
            catch
            {
                configuredPort = null;
            }

            if (!string.IsNullOrWhiteSpace(configuredPort))
            {
                configuredPort = configuredPort.Trim().ToUpper();

                string matchedPort = availablePorts.FirstOrDefault(
                    p => p.Equals(
                        configuredPort,
                        StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(matchedPort))
                {
                    portsToTry.Add(matchedPort);
                }
            }

            foreach (string port in availablePorts)
            {
                if (!portsToTry.Any(
                    p => p.Equals(
                        port,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    portsToTry.Add(port);
                }
            }

            return portsToTry;
        }



        private bool TryOpenPort(
            string portName,
            out string error,
            out bool accessDenied)
        {
            error = string.Empty;
            accessDenied = false;

            SerialPort port = null;

            try
            {
                port = new SerialPort
                {
                    PortName = portName,
                    BaudRate = RFID_BAUD_RATE,
                    Parity = Parity.None,
                    DataBits = 8,
                    StopBits = StopBits.One,
                    Handshake = Handshake.None,

                    ReadTimeout = 500,
                    WriteTimeout = 500,

                    NewLine = "\r\n",

                    DtrEnable = false,
                    RtsEnable = false
                };

                port.DataReceived += RFIDPort_DataReceived;
                port.ErrorReceived += RFIDPort_ErrorReceived;

                port.Open();

                if (!port.IsOpen)
                {
                    error = "Port could not be opened.";
                    port.DataReceived -= RFIDPort_DataReceived;
                    port.ErrorReceived -= RFIDPort_ErrorReceived;
                    port.Dispose();

                    return false;
                }

                try
                {
                    port.DiscardInBuffer();
                    port.DiscardOutBuffer();
                }
                catch
                {

                }

                rfidPort = port;

                lock (rfidBuffer)
                {
                    rfidBuffer.Clear();
                }

                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                accessDenied = true;

                error =
                    "Access denied. The COM port is probably already in use. " +
                    ex.Message;


                try
                {
                    if (port != null)
                    {
                        port.DataReceived -= RFIDPort_DataReceived;
                        port.ErrorReceived -= RFIDPort_ErrorReceived;

                        if (port.IsOpen)
                            port.Close();

                        port.Dispose();
                    }
                }
                catch
                {

                }

                return false;
            }
            catch (IOException ex)
            {
                error =
                    "I/O error. RFID device may be disconnected. " +
                    ex.Message;

                try
                {
                    if (port != null)
                    {
                        port.DataReceived -= RFIDPort_DataReceived;
                        port.ErrorReceived -= RFIDPort_ErrorReceived;

                        if (port.IsOpen)
                            port.Close();

                        port.Dispose();
                    }
                }
                catch
                {

                }

                return false;
            }
            catch (InvalidOperationException ex)
            {
                error =
                    "Invalid COM port operation. " +
                    ex.Message;

                try
                {
                    if (port != null)
                    {
                        port.DataReceived -= RFIDPort_DataReceived;
                        port.ErrorReceived -= RFIDPort_ErrorReceived;

                        if (port.IsOpen)
                            port.Close();

                        port.Dispose();
                    }
                }
                catch
                {

                }

                return false;
            }
            catch (Exception ex)
            {
                error =
                    ex.GetType().Name + ": " + ex.Message;

                try
                {
                    if (port != null)
                    {
                        port.DataReceived -= RFIDPort_DataReceived;
                        port.ErrorReceived -= RFIDPort_ErrorReceived;

                        if (port.IsOpen)
                            port.Close();

                        port.Dispose();
                    }
                }
                catch
                {

                }

                return false;
            }
        }



        private void CloseRfidPort()
        {
            SerialPort port = rfidPort;

            rfidPort = null;

            if (port == null)
                return;

            try
            {
                port.DataReceived -= RFIDPort_DataReceived;
                port.ErrorReceived -= RFIDPort_ErrorReceived;
            }
            catch
            {
            }

            try
            {
                if (port.IsOpen)
                {
                    port.Close();
                }
            }
            catch
            {
            }

            try
            {
                port.Dispose();
            }
            catch
            {
            }

            lock (rfidBuffer)
            {
                rfidBuffer.Clear();
            }
        }



        private void RfidReconnectTimer_Tick(object sender, EventArgs e)
        {
            if (formClosing)
                return;

            if (rfidPort != null && rfidPort.IsOpen)
                return;

            InitializeRFIDScanner();
        }



        private void RFIDPort_DataReceived(
     object sender,
     SerialDataReceivedEventArgs e)
        {
            try
            {
                SerialPort port = sender as SerialPort;

                if (port == null || !port.IsOpen)
                    return;

                string incomingData = port.ReadExisting();

                if (string.IsNullOrEmpty(incomingData))
                    return;

                lock (rfidBuffer)
                {
                    rfidBuffer.Append(incomingData);

                    string currentData = rfidBuffer.ToString();
                    int endIndex = currentData.IndexOfAny(new[] { '\r', '\n' });

                    if (endIndex < 0)
                        return;

                    string scannedData = currentData
                        .Substring(0, endIndex)
                        .Trim();

                    int nextIndex = endIndex;

                    while (nextIndex < currentData.Length &&
                           (currentData[nextIndex] == '\r' ||
                            currentData[nextIndex] == '\n'))
                    {
                        nextIndex++;
                    }

                    rfidBuffer.Clear();

                    if (nextIndex < currentData.Length)
                        rfidBuffer.Append(currentData.Substring(nextIndex));

                    if (!string.IsNullOrWhiteSpace(scannedData))
                        ProcessRFIDScan(scannedData);
                }
            }
            catch (IOException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch
            {
            }
        }



        private void RFIDPort_ErrorReceived(
            object sender,
            SerialErrorReceivedEventArgs e)
        {

        }



        private void ProcessRFIDScan(string scannedData)
        {
            string employeeId = ExtractEmployeeId(scannedData);

            SafeInvoke(() =>
            {
                if (formClosing)
                    return;

                if (string.IsNullOrEmpty(employeeId))
                {
                    rfidTextBox.Text = string.Empty;

                    ShowTemporaryLabel(
                        "❌ Invalid RFID. Please try again.",
                        Color.Red);

                    return;
                }

                rfidTextBox.Text = employeeId;
                ShowFriendlyFeedbackPrompt();
            });
        }



        private void SafeInvoke(Action action)
        {
            try
            {
                if (formClosing)
                    return;

                if (IsDisposed || Disposing)
                    return;

                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (!formClosing &&
                                !IsDisposed &&
                                !Disposing)
                            {
                                action();
                            }
                        }
                        catch
                        {
                        }
                    }));
                }
                else
                {
                    action();
                }
            }
            catch
            {
            }
        }



        private void ShowFriendlyFeedbackPrompt()
        {
            if (formClosing)
                return;

            Label friendlyPrompt = new Label();

            friendlyPrompt.Text =
                "Thank you for scanning your card!\n" +
                "Please share your feedback. 😊";

            friendlyPrompt.AutoSize = true;

            friendlyPrompt.Font =
                new Font(
                    "Arial",
                    14,
                    FontStyle.Bold);

            friendlyPrompt.ForeColor =
                Color.FromArgb(255, 102, 102);

            friendlyPrompt.Location =
                new Point(
                    (this.ClientSize.Width -
                     friendlyPrompt.PreferredWidth) / 2,

                    this.ClientSize.Height -
                    friendlyPrompt.PreferredHeight -
                    100
                );

            this.Controls.Add(friendlyPrompt);
            friendlyPrompt.BringToFront();

            Timer timer = new Timer
            {
                Interval = 2000
            };

            timer.Tick += (s, args) =>
            {
                try
                {
                    if (!friendlyPrompt.IsDisposed)
                    {
                        this.Controls.Remove(friendlyPrompt);
                        friendlyPrompt.Dispose();
                    }

                    timer.Stop();
                    timer.Dispose();
                }
                catch
                {
                }
            };

            timer.Start();
        }



        private string ExtractEmployeeId(string scannedData)
        {
            if (string.IsNullOrWhiteSpace(scannedData))
                return string.Empty;

            string cleanedData = scannedData.Trim();
            string employeeId = Regex.Replace(cleanedData, @"[^0-9]", "");

            return employeeId.Trim();
        }



        private void HappyBtn_Click(
            object sender,
            EventArgs e)
        {
            SaveFeedback("Happy");
        }

        private void NormalBtn_Click(
            object sender,
            EventArgs e)
        {
            SaveFeedback("Normal");
        }

        private void AngryBtn_Click(
            object sender,
            EventArgs e)
        {
            SaveFeedback("Anger");
        }



        private DateTime GetServerDateTime()
        {
            using (SqlConnection conn =
                   new SqlConnection(connectionString))
            using (SqlCommand cmd =
                   new SqlCommand("SELECT GETDATE()", conn))
            {
                conn.Open();

                object result =
                    cmd.ExecuteScalar();

                return result != null
                    ? (DateTime)result
                    : DateTime.Now;
            }
        }



        private void SaveFeedback(string feedbackType)
        {
            string employeeId =
                rfidTextBox.Text.Trim();

            if (string.IsNullOrEmpty(employeeId))
            {
                MessageBox.Show(
                    "Please scan your Employee Card first!",
                    "RFID Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            try
            {
                DateTime serverNow =
                    GetServerDateTime();

                using (SqlConnection conn =
                       new SqlConnection(connectionString))
                using (SqlCommand cmd =
                       new SqlCommand(
                           "InsertFeedbackData",
                           conn))
                {
                    cmd.CommandType =
                        CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue(
                        "@FeedbackType",
                        feedbackType);

                    cmd.Parameters.AddWithValue(
                        "@Timestamp",
                        serverNow);

                    cmd.Parameters.AddWithValue(
                        "@UserId",
                        employeeId);

                    conn.Open();

                    cmd.ExecuteNonQuery();
                }

                rfidTextBox.Text = string.Empty;

                ShowTemporaryLabel(
                    "✔ Feedback Submitted! Thank you.",
                    Color.Green);
            }
            catch (SqlException ex)
                when (ex.Message.Contains(
                    "Feedback Already Submitted"))
            {
                rfidTextBox.Text = string.Empty;

                ShowTemporaryLabel(
                    "⚠ " + ex.Message,
                    Color.Orange);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }



        private void ShowTemporaryLabel(
            string message,
            Color color)
        {
            if (formClosing)
                return;

            Label lbl = new Label();

            lbl.Text = message;
            lbl.AutoSize = true;

            lbl.Font =
                new Font(
                    "Arial",
                    14);

            lbl.ForeColor = color;

            lbl.Location =
                new Point(
                    (this.ClientSize.Width -
                     lbl.PreferredWidth) / 2,

                    this.ClientSize.Height -
                    lbl.PreferredHeight -
                    100
                );

            this.Controls.Add(lbl);
            lbl.BringToFront();

            Timer timer = new Timer
            {
                Interval = 2000
            };

            timer.Tick += (s, args) =>
            {
                try
                {
                    if (!lbl.IsDisposed)
                    {
                        this.Controls.Remove(lbl);
                        lbl.Dispose();
                    }

                    timer.Stop();
                    timer.Dispose();
                }
                catch
                {
                }
            };

            timer.Start();
        }



        private void ShowAndRefreshResultForm()
        {
            if (resultForm == null ||
                resultForm.IsDisposed)
            {
                resultForm =
                    new ResultFeedBackEmployee();

                resultForm.Show();
            }

            resultForm.UpdateFeedbackStats();
        }



        private void ResetFeedbackData(
            object sender,
            EventArgs e)
        {
            resultForm?.UpdateFeedbackStats();

            SetFeedbackResetTimer();

            MessageBox.Show(
                "Feedback data for today is displayed!");
        }



        private void SetFeedbackResetTimer()
        {
            DateTime now = DateTime.Now;

            DateTime nextResetTime =
                now.Date.AddHours(21);

            if (now >= nextResetTime)
            {
                nextResetTime =
                    nextResetTime.AddDays(1);
            }

            double milliseconds =
                (nextResetTime - now).TotalMilliseconds;

            int interval;

            if (milliseconds >
                Int32.MaxValue)
            {
                interval =
                    Int32.MaxValue;
            }
            else
            {
                interval =
                    Math.Max(
                        1000,
                        (int)milliseconds);
            }

            Timer feedbackResetTimer =
                new Timer
                {
                    Interval = interval
                };

            feedbackResetTimer.Tick +=
                new EventHandler(
                    ResetFeedbackData);

            feedbackResetTimer.Start();
        }



        private void Form1_Load(
            object sender,
            EventArgs e)
        {
        }



        private void pictureBox1_Click(
            object sender,
            EventArgs e)
        {
            new ResultFeedBackEmployee().Show();
        }



        private void ReportView_Click(
            object sender,
            EventArgs e)
        {


        }



        private void button1_Click(
            object sender,
            EventArgs e)
        {
            ShowAndRefreshResultForm();
        }



        protected override void OnFormClosing(
            FormClosingEventArgs e)
        {
            formClosing = true;

            try
            {
                if (rfidReconnectTimer != null)
                {
                    rfidReconnectTimer.Stop();
                    rfidReconnectTimer.Dispose();
                    rfidReconnectTimer = null;
                }
            }
            catch
            {
            }

            CloseRfidPort();

            base.OnFormClosing(e);
        }
    }
}
