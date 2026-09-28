using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Office.Interop.Excel;

namespace MessRealTimeFeedBack
{
    public partial class FeedbackReportForm : Form
    {
        string connectionString = ConfigurationManager.ConnectionStrings["EmployeeFeedbackDB"].ConnectionString;
        private string currentPeriod = "Day";

        public FeedbackReportForm()
        {
            InitializeComponent();
        }

        private void FeedbackReportForm_Load(object sender, EventArgs e)
        {
            lblpercentageHappy.Text = "Happy: 0% (0 feedbacks)";
            lblpercentageNormal.Text = "Normal: 0% (0 feedbacks)";
            lblpercentageAnger.Text = "Anger: 0% (0 feedbacks)";
        }

        private DateTime GetCurrentFeedbackDate()
        {
            DateTime now = DateTime.Now;
            TimeSpan dayStart = new TimeSpan(9, 0, 0);
            TimeSpan dayEnd = new TimeSpan(22, 0, 0);

            if (now.TimeOfDay < dayStart || now.TimeOfDay >= dayEnd)
            {
                return now.AddDays(-1).Date;
            }

            return now.Date;
        }

        private void ShowFeedbackReport(string period)
        {
            try
            {
                DateTime selectedDate = GetCurrentFeedbackDate();
                string query = GetQuery(period, selectedDate);

                System.Data.DataTable feedbackData = new System.Data.DataTable();

                using (SqlConnection conn = new SqlConnection(connectionString))
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (period == "Day")
                    {
                        cmd.Parameters.AddWithValue("@FeedbackDate", selectedDate);
                    }

                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    adapter.Fill(feedbackData);
                }

                if (feedbackData.Rows.Count == 0)
                {
                    MessageBox.Show("No feedback records found for the selected period.");
                    return;
                }

                dataGridView1.DataSource = feedbackData;
                dataGridView1.Columns["Timestamp"].DefaultCellStyle.Format = "yyyy-MM-dd HH:mm:ss";

                if (!dataGridView1.Columns.Contains("Count"))
                {
                    dataGridView1.Columns.Add("Count", "Count");
                }

                if (!dataGridView1.Columns.Contains("Percentage"))
                {
                    dataGridView1.Columns.Add("Percentage", "Percentage");
                }

                int totalFeedbacks = feedbackData.Rows.Count;
                int happyCount = feedbackData.AsEnumerable().Count(row => row.Field<string>("FeedbackType") == "Happy");
                int normalCount = feedbackData.AsEnumerable().Count(row => row.Field<string>("FeedbackType") == "Normal");
                int angerCount = feedbackData.AsEnumerable().Count(row => row.Field<string>("FeedbackType") == "Anger");

                double happyPercentage = (totalFeedbacks > 0) ? (double)happyCount / totalFeedbacks * 100 : 0;
                double normalPercentage = (totalFeedbacks > 0) ? (double)normalCount / totalFeedbacks * 100 : 0;
                double angerPercentage = (totalFeedbacks > 0) ? (double)angerCount / totalFeedbacks * 100 : 0;

                lblpercentageHappy.Text = $"Happy: {happyPercentage:F2}% ({happyCount} feedbacks)";
                lblpercentageNormal.Text = $"Normal: {normalPercentage:F2}% ({normalCount} feedbacks)";
                lblpercentageAnger.Text = $"Anger: {angerPercentage:F2}% ({angerCount} feedbacks)";

                foreach (DataRow row in feedbackData.Rows)
                {
                    string feedbackType = row["FeedbackType"].ToString();
                    double percentage = 0;

                    if (feedbackType == "Happy") percentage = happyPercentage;
                    else if (feedbackType == "Normal") percentage = normalPercentage;
                    else if (feedbackType == "Anger") percentage = angerPercentage;

                    var dgvRow = dataGridView1.Rows
                        .Cast<DataGridViewRow>()
                        .FirstOrDefault(r => r.Cells["FeedbackType"].Value.ToString() == feedbackType);

                    if (dgvRow != null)
                    {
                        dgvRow.Cells["Count"].Value = feedbackType == "Happy" ? happyCount :
                                                      feedbackType == "Normal" ? normalCount :
                                                      angerCount;

                        dgvRow.Cells["Percentage"].Value = $"{percentage:F2}%";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading feedback report: {ex.Message}");
            }
        }

        private string GetQuery(string period, DateTime date)
        {
            string dateCondition = "";

            if (period == "Day")
            {
                dateCondition = "CAST(Timestamp AS DATE) = @FeedbackDate";
            }
            else if (period == "Week")
            {
                dateCondition = "DATEPART(WEEK, Timestamp) = DATEPART(WEEK, GETDATE()) AND YEAR(Timestamp) = YEAR(GETDATE())";
            }
            else if (period == "Month")
            {
                dateCondition = "MONTH(Timestamp) = MONTH(GETDATE()) AND YEAR(Timestamp) = YEAR(GETDATE())";
            }

            return $@"
                SELECT FeedbackType, Timestamp
                FROM FeedbackData
                WHERE {dateCondition}
            ";
        }

        private void dayRadioBtn_CheckedChanged_1(object sender, EventArgs e)
        {
            currentPeriod = "Day";
            ShowFeedbackReport(currentPeriod);
        }

        private void weekRadioBtn_CheckedChanged_1(object sender, EventArgs e)
        {
            currentPeriod = "Week";
            ShowFeedbackReport(currentPeriod);
        }

        private void monthRadioBtn_CheckedChanged_1(object sender, EventArgs e)
        {
            currentPeriod = "Month";
            ShowFeedbackReport(currentPeriod);
        }

        private void btnExportExcel_Click(object sender, EventArgs e)
        {
            if (dataGridView1.Rows.Count > 0)
            {
                var excelApp = new Microsoft.Office.Interop.Excel.Application();
                excelApp.Visible = true;

                Workbook workbook = excelApp.Workbooks.Add();
                Worksheet worksheet = workbook.ActiveSheet;

                for (int i = 0; i < dataGridView1.Columns.Count; i++)
                {
                    worksheet.Cells[1, i + 1] = dataGridView1.Columns[i].HeaderText;
                }

                for (int row = 0; row < dataGridView1.Rows.Count; row++)
                {
                    for (int col = 0; col < dataGridView1.Columns.Count; col++)
                    {
                        worksheet.Cells[row + 2, col + 1] = dataGridView1.Rows[row].Cells[col].Value?.ToString();
                    }
                }

                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.Filter = "Excel Files|*.xls;*.xlsx";

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    workbook.SaveAs(saveFileDialog.FileName);
                    MessageBox.Show("Report exported successfully!");
                }
            }
            else
            {
                MessageBox.Show("No data to export.");
            }
        }

        private void MainMenuBtn_Click(object sender, EventArgs e)
        {
            // Optionally open Form1
            // Form1 mainForm = new Form1();
            // mainForm.Show();
            // this.Close();
        }
    }
}
