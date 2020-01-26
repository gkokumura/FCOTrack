using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;

namespace FcoMgr
{
    public class FCOPath
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int GetPrivateProfileString(string section, string key, string defaultValue, StringBuilder value, int size, string filePath);

        private static string[] months = new string[] {"", "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        private string m_fco;
        private DateTime m_dt;

        public FCOPath(string fcoNumber, DateTime dt)
        {
            m_fco = fcoNumber;
            m_dt = dt;
        }

        public FCOPath(DateTime dt)
        {
            m_fco = string.Empty;
            m_dt = dt;
        }

        public string GetUpgradeCodeDir()
        {
            string upgradeCodeDir = GetUpgradeCodeRootPath() /*+ m_fco*/;
            return upgradeCodeDir;
        }

        public string GetReportFilePath(int rptType)
        {
            /*
            string rptDir = GetReportRootPath() + "FCO" + m_fco + "\\" + m_dt.Year + "\\" + months[m_dt.Month];
            string type = string.Empty;
            switch (rptType)
            {
                case (int)Constants.REPORTTYPE.DAILY:
                    type = "_Daily_";
                    break;
                case (int)Constants.REPORTTYPE.WEEKLY:
                    type = "_Weekly_";
                    break;
                default:
                    type = "_Unknown_";
                    break;
            }
            string rptFilePath = rptDir + "\\" + "FCO" + m_fco + type + m_dt.ToString("MMddyyyyHHmmss") + ".csv";
            */
            string rptDir = string.Empty;
            string rptFilePath = string.Empty;
            if (rptType == (int)Constants.REPORTTYPE.DAILY)
            {
                rptDir = GetReportRootPath() + "Daily" + "\\" + m_dt.Year + "\\" + months[m_dt.Month];
                rptFilePath = rptDir + "\\" + "Daily_" + m_dt.ToString("MMddyyyyHHmmss") + ".csv";
            }
            else //weekly report
            {
                rptDir = GetReportRootPath() + "Weekly" + "\\FCO" + m_fco + "\\" + m_dt.Year + "\\" + months[m_dt.Month];
                rptFilePath = rptDir + "\\" + "FCO" + m_fco + "_Weekly_" + m_dt.ToString("MMddyyyyHHmmss") + ".csv";
            }

            try
            {
                if (!Directory.Exists(rptDir))
                    Directory.CreateDirectory(rptDir);
                return rptFilePath;
            }
            catch (Exception ex)
            {
                LogHelper.Instance.Error("Fail to create report directory due to " + ex.Message);
                throw new System.AccessViolationException(ex.Message);
            }
        }

        public string GetArchiveFilePath()
        {
            //string archiveDir = GetArchiveRootPath() + "FCO" + m_fco + "\\" + m_dt.Year + "\\" + months[m_dt.Month];
            string archiveDir = GetArchiveRootPath() + m_dt.Year + "\\" + months[m_dt.Month] + "\\" + m_dt.Day;
     
            try
            {
                if (!Directory.Exists(archiveDir ))
                    Directory.CreateDirectory(archiveDir);

                int fileNum = Directory.GetFiles(archiveDir, "*.csv").Length + 1;
                string archiveFilePath = archiveDir + "\\" + "UpgradeCode_" + m_dt.ToString("MMddyyyy") + "_" + fileNum + ".csv";
                return archiveFilePath;
            }

            catch (Exception ex)
            {
                LogHelper.Instance.Error("Fail to create archive directory due to " + ex.Message);
                throw new System.AccessViolationException(ex.Message);
            }
        }

        public static bool TestRootDir()
        {
            string upgradeCodeRootDir = GetUpgradeCodeRootPath();
            string archiveRootDir = GetArchiveRootPath();
            string reportRootDir = GetReportRootPath();

            if (!Directory.Exists(upgradeCodeRootDir))
            {
                LogHelper.Instance.Error("Unable to access Upgrade Code directory.");
                return false;
            }

            if (!HasWriteAccessToFolder(archiveRootDir))
            {
                LogHelper.Instance.Error("No write permission to Archive folder.");
                return false;
            }

            if (!HasWriteAccessToFolder(reportRootDir))
            {
                LogHelper.Instance.Error("No write permission to Report folder.");
                return false;
            }

            return true;
        }

        private static bool HasWriteAccessToFolder(string folderDir)
        {
            try
            {
                System.Security.AccessControl.DirectorySecurity ds = Directory.GetAccessControl(folderDir);
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                LogHelper.Instance.Error(string.Format("Has no write access to folder {0} due to {1}.", folderDir, ex.Message));
                return false;
            }
        }

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
                        path = path + "\\";
                }
            }
            return path;
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
                        path = path + "\\";
                }
            }

            return path;
        }

        private static string GetUpgradeCodeRootPath()
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
                        path = path + "\\";
                }
            }

            return path;
        }
    }
}
