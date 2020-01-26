using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace FcoMgr
{

    public class Common
    {
        private static string majorVer = "2";
        private static string minorVer = "2";
        public static string VersionNumber
        {
            get { return majorVer + "." + minorVer; }
        }

        public static void CheckDataFolder()
        {
            string file_fcoDB = "FcoDB.db";
            string file_logDB = "FcoLog.db";
            try
            {
                string folder = System.AppDomain.CurrentDomain.BaseDirectory + "db" + "\\";
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);
                if (!File.Exists(folder + file_fcoDB))
                    File.Copy(System.AppDomain.CurrentDomain.BaseDirectory + file_fcoDB, folder + file_fcoDB);
                if (!File.Exists(folder + file_logDB))
                    File.Copy(System.AppDomain.CurrentDomain.BaseDirectory + file_logDB, folder + file_logDB);
            }
            catch (Exception ex)
            {
                LogHelper.Instance.Error("Check Database folder error due to" + ex.Message);
            }
        }

    }
}
