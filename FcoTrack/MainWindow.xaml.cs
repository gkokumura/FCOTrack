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
using System.Windows.Forms;
using FcoMgr;
using System.IO;


namespace FcoTrack
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            Title = "FCOTrackTool Rev " + Common.VersionNumber;
            Common.CheckDataFolder();
            LogHelper.Instance.Info("******FcoTrack.exe started******");
            LogHelper.Instance.Info("Rev " + Common.VersionNumber);
            list_ImportedFco.ItemsSource = ReportManager.UniqueFCONumber;

        }


        private async void btnAddFco_Click(object sender, RoutedEventArgs e)
        {
            int intFcoNumber = 0;
            bool success = Int32.TryParse(textFco.Text, out intFcoNumber);
            if (!success)
                labelMsg.Text = "Invalid FCO Input";
            else
            {
                string fcoNumber = textFco.Text;
                string fcoRev = text_fco_rev.Text;
                labelMsg.Text = string.Empty;
                string filePath = string.Empty;
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.InitialDirectory = Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments);
                    openFileDialog.Filter = "csv files (*.csv)|*.csv";
                    if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        filePath = openFileDialog.FileName;
                        labelMsg.Text = "UAL table importing...";
                        SetUIStat(false);

                        bool result = await Task.Run(async() => await ReportManager.Instance.StoreUAL(filePath, fcoNumber, fcoRev));
                        if (result)
                        {
                            labelMsg.Text = string.Format("FCO {0} UAL table has been successfully imported.", fcoNumber);
                            list_ImportedFco.ItemsSource = ReportManager.UniqueFCONumber;

                            list_ImportedFco.Items.Refresh();
                        }
                        else
                        {
                            labelMsg.Text = string.Format("Unable to import UAL table, please check log for detail.");
                        }
                        SetUIStat(true);
                        textFco.Text = string.Empty;
                        text_fco_rev.Text = string.Empty;
                    }
                }
                
            }
            
        }

        private async void btn_DailyReport_Click(object sender, RoutedEventArgs e)
        {
            LogHelper.Instance.Info("Daily Report button clicked.");
            labelMsg.Text = "Generating daily report...";
            SetUIStat(false);
            await Task.Run(async() => await ReportManager.Instance.GenerateDailyReport());

            SetUIStat(true);
            labelMsg.Text = "Daily report is completed.";
        }

        private async void btn_WeeklyReport_Click(object sender, RoutedEventArgs e)
        {
            LogHelper.Instance.Info("Weekly Report button clicked.");
            labelMsg.Text = "Generating weekly report...";
            SetUIStat(false);
            await Task.Run(async() => await ReportManager.Instance.GenerateWeeklyReport());

            SetUIStat(true);
            labelMsg.Text = "Weekly report is completed.";
        }

        private void btn_ExportLog_Click(object sender, RoutedEventArgs e)
        {
            string filePath = string.Empty;
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Text File|*.txt";
                saveFileDialog.Title = "Save Log File To";
                saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                saveFileDialog.RestoreDirectory = true;
                if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    if (saveFileDialog.FileName != "")
                    {
                        LogHelper.ExportToFile(saveFileDialog.FileName);
                        textLogPath.Text = saveFileDialog.FileName;
                        LogHelper.Instance.Info("Export database log to " + saveFileDialog.FileName);
                    }

                }

                
            }
        }

        private void SetUIStat(bool isEnable)
        {
            btnAddFco.IsEnabled = isEnable;
            btn_DailyReport.IsEnabled = isEnable;
            btn_WeeklyReport.IsEnabled = isEnable;
            textFco.IsEnabled = isEnable;
            text_fco_rev.IsEnabled = isEnable;
        }
    }
}
