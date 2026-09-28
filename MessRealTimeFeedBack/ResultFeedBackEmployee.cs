using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Windows.Forms;

namespace MessRealTimeFeedBack
{
    public partial class ResultFeedBackEmployee : Form
    {
        string connectionString = ConfigurationManager.ConnectionStrings["EmployeeFeedbackDB"].ConnectionString;

        public ResultFeedBackEmployee()
        {
            InitializeComponent();
        }

        private void ResultFeedBackEmployee_Load(object sender, EventArgs e)
        {
            AngerLbl.Text = "0%";
            NormalLbl.Text = "0%";
            HappyLbl.Text = "0%";
            ShowFeedbackPercentages();
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

        private void ShowFeedbackPercentages()
        {
            DateTime feedbackDate = GetCurrentFeedbackDate();

            string query = @"
                SELECT FeedbackType, COUNT(*) AS FeedbackCount
                FROM FeedbackData
                WHERE CAST(Timestamp AS DATE) = @FeedbackDate
                GROUP BY FeedbackType";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@FeedbackDate", feedbackDate);
                    SqlDataAdapter da = new SqlDataAdapter(cmd);
                    DataTable dt = new DataTable();
                    da.Fill(dt);

                    int totalFeedbacks = dt.AsEnumerable().Sum(row => row.Field<int>("FeedbackCount"));
                    if (totalFeedbacks > 0)
                    {
                        foreach (DataRow row in dt.Rows)
                        {
                            string feedbackType = row["FeedbackType"].ToString();
                            int feedbackCount = Convert.ToInt32(row["FeedbackCount"]);
                            double percentage = (double)feedbackCount / totalFeedbacks * 100;
                            SetFeedbackLabel(feedbackType, percentage);
                        }
                    }
                    else
                    {
                        ResetFeedbackLabels();
                    }
                }
            }
        }

        private void SetFeedbackLabel(string feedbackType, double percentage)
        {
            if (feedbackType == "Anger")
            {
                AngerLbl.Text = $"{percentage:F2}%";
            }
            else if (feedbackType == "Normal")
            {
                NormalLbl.Text = $"{percentage:F2}%";
            }
            else if (feedbackType == "Happy")
            {
                HappyLbl.Text = $"{percentage:F2}%";
            }
        }

        public void UpdateFeedbackStats()
        {
            DateTime feedbackDate = GetCurrentFeedbackDate();

            string query = @"
                SELECT FeedbackType, COUNT(*) AS Count
                FROM FeedbackData
                WHERE CAST(Timestamp AS DATE) = @FeedbackDate
                GROUP BY FeedbackType";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@FeedbackDate", feedbackDate);
                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    DataTable dataTable = new DataTable();
                    adapter.Fill(dataTable);

                    int totalFeedbacks = dataTable.AsEnumerable().Sum(row => row.Field<int>("Count"));
                    if (totalFeedbacks > 0)
                    {
                        foreach (DataRow row in dataTable.Rows)
                        {
                            string feedbackType = row["FeedbackType"].ToString();
                            int count = Convert.ToInt32(row["Count"]);
                            double percentage = (double)count / totalFeedbacks * 100;
                            SetFeedbackLabel(feedbackType, percentage);
                        }
                    }
                    else
                    {
                        ResetFeedbackLabels();
                    }
                }
            }
        }

        public void ResetFeedbackLabels()
        {
            AngerLbl.Text = "0%";
            NormalLbl.Text = "0%";
            HappyLbl.Text = "0%";
        }

        private void label1_Click(object sender, EventArgs e)
        {
            // Optional: handle click
        }

        private void ReportView_Click(object sender, EventArgs e)
        {
            bool isFormOpen = false;

            foreach (Form form in Application.OpenForms)
            {
                if (form is FeedbackReportForm)
                {
                    isFormOpen = true;
                    form.WindowState = FormWindowState.Normal;
                    break;
                }
            }

            if (!isFormOpen)
            {
                FeedbackReportForm reportForm = new FeedbackReportForm();
                reportForm.Show();
            }
        }

        private void pictureBox2_Click(object sender, EventArgs e)
        {
            // Optional: handle click
        }

        private void NormalLbl_Click(object sender, EventArgs e)
        {
            // Optional: handle click
        }
    }
}
