using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;

namespace FcoMgr
{
    public class ReportMgr
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int GetPrivateProfileString(string section, string key, string defaultValue, StringBuilder value, int size, string filePath);

        public static string[] months = new string[] { "", "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        public static DateTime today = DateTime.Today;

        private DataProcessor dataprocessor = new DataProcessor();
        private static string dtSubFolder = DataProcessor.Today.Year.ToString() + "\\" + months[DataProcessor.Today.Month] + "\\";

        private static string GetReportRootPath()
        {
            string path = System.IO.Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory) + "\\config.ini";
            const int max_chars = 512;
            StringBuilder buffer = new StringBuilder();
            if (GetPrivateProfileString("common", "ReportPath", string.Empty, buffer, max_chars, path) != 0)
            {
                path = buffer.ToString();
                if (!string.IsNullOrEmpty(path))
                {
                    if (path[path.Length - 1] != '\\')
                        return path + "\\";
                    return path;
                }
            }
            Console.WriteLine("Report Root Path is not set.");
            return string.Empty;
        }

        public static string GetReportPath(string fcoNumber)
        {
            string fcoFolder = "FCO" + fcoNumber.ToString();
            string rptRootDir = GetReportRootPath();
            if (string.IsNullOrEmpty(rptRootDir))
                return string.Empty;

            string reportFilePath = rptRootDir + fcoFolder + "\\" + dtSubFolder;
            try
            {
                Directory.CreateDirectory(reportFilePath);
                return reportFilePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fail to create Report Path due to " + ex.Message);
                return string.Empty;
            }
        }

        private static string GetArchiveRootPath()
        {
            string path = System.IO.Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory) + "\\config.ini";
            const int max_chars = 512;
            StringBuilder buffer = new StringBuilder();
            if (GetPrivateProfileString("common", "ArchivePath", string.Empty, buffer, max_chars, path) != 0)
            {
                path = buffer.ToString();
                if (!string.IsNullOrEmpty(path))
                {
                    if (path[path.Length - 1] != '\\')
                        return path + "\\";
                    return path;
                }
            }

            Console.WriteLine("Archive Root Path is not set.");
            return string.Empty;
        }

        public static string GetArchivePath(string fcoNumber)
        {
            string fcoFolder = "FCO" + fcoNumber;
            string archiveRootDir = GetArchiveRootPath();
            if (string.IsNullOrEmpty(archiveRootDir))
                return string.Empty;

            string archiveFilePath = archiveRootDir + fcoFolder + "\\" + dtSubFolder;

            try
            {
                Directory.CreateDirectory(archiveFilePath);
                return archiveFilePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fail to create Archive Path due to " + ex.Message);
                return string.Empty;
            }
        }

        public static string GetUpgradeCodeRootPath()
        {
            string path = System.IO.Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory) + "\\config.ini";
            const int max_chars = 512;
            StringBuilder buffer = new StringBuilder();
            if (GetPrivateProfileString("common", "UpgradeCodePath", string.Empty, buffer, max_chars, path) != 0)
            {
                path = buffer.ToString();
                if (!string.IsNullOrEmpty(path))
                {
                    if (path[path.Length - 1] != '\\')
                        return path + "\\";
                    return path;
                }
            }

            return string.Empty;
        }


        public bool ArchiveUAL(string ualFilePath, string fcoNumber)
        {
            return dataprocessor.StoreUAL(ualFilePath, fcoNumber);
        }

        public bool GenerateDailyReport()
        {
            /*
             * 1. For each FCO
             *      1.1 import all .csv files under UpgradeCode path to database;
             *      1.2 Move files to the archive folder;
             * 2. Update Main UAL and UpgradeResult table per comparison result;
             * 3. Generate Daily report;
             * 4. Delete records in UpgradeResult table;
             * 5. Back to step 1 for next FCO 
             */

            string strRoot = GetUpgradeCodeRootPath();
            if (string.IsNullOrEmpty(strRoot))
            {
                Console.WriteLine("Upgrade Code directory is not set.");
                return false;
            }
            DirectoryInfo rootDir = new DirectoryInfo(GetUpgradeCodeRootPath());
            DirectoryInfo[] subDirs = rootDir.GetDirectories();
            if (subDirs.Length == 0)
            {
                Console.WriteLine("No FCO folder under upgrade code directory.");
                return false;
            }

            foreach (DirectoryInfo subDir in subDirs)
            {
                string fcoNumber = subDir.Name;

                if (!dataprocessor.FCOList.Contains(fcoNumber))
                {
                    Console.WriteLine(string.Format("UAL for FCO {0} has not been imported.", fcoNumber));
                    return false;
                }
                
                FileInfo[] files = subDir.GetFiles("*.csv");
                string archiveDir = GetArchivePath(fcoNumber);
                string reportDir = GetReportPath(fcoNumber);
                string dailyReportFilePath = reportDir + "FCO" + fcoNumber + "_Daily_" + DataProcessor.Today.ToString("MMddyyyy") + ".csv";
                /*if archive failed, stops processing and generate daily report.*/
                if (string.IsNullOrEmpty(archiveDir) || string.IsNullOrEmpty(reportDir))
                {
                    return false;
                }
                if (File.Exists(dailyReportFilePath))
                {
                    Console.WriteLine("Daily Report File exists.");
                    return false;
                }
                int fileCount = 1;
                foreach (FileInfo file in files)
                {
                    dataprocessor.StoreUpgradeCode(file.FullName, fcoNumber);
                    string desFileName = archiveDir + "UpgradeCode_" + DataProcessor.Today.ToString("MMddyyyy") + "_" + fileCount.ToString() + ".csv";
                    //Move file to destination archive folder
                    Console.WriteLine(string.Format("Moving file from {0} to {1}.", file.FullName, desFileName));
                    try
                    {
                        System.IO.File.Move(file.FullName, desFileName);
                        fileCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        return false;
                    }
                }
                
                dataprocessor.ProcessUpgradeCode();
                dataprocessor.ExportFailureReport(dailyReportFilePath);
                dataprocessor.RemoveAllFromUpgradeResult();
                
            }
            return true;
        }

        public void GenerateWeeklyReport()
        {
            //1. Generate Weekly Report according to the mainUAL data and generate it to .csv file;
            //2. Copy the report to the destination folder;

            foreach (string fcoNumber in dataprocessor.FCOList)
            {
                string weeklyReportFilePath = GetReportPath(fcoNumber) + "FCO"+fcoNumber.ToString()+ "_Weekly_" + DataProcessor.Today.ToString("MMddyyyy") + ".csv";
                if (File.Exists(weeklyReportFilePath))
                {
                    Console.WriteLine("Weekly Report file exists.");
                    return;
                }
                dataprocessor.ExportWeeklyReport(fcoNumber, weeklyReportFilePath);
            }

        }

    }
}
