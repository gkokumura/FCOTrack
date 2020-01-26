using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FcoMgr
{
    class Program
    {
        static void Main(string[] args)
        {
            int dbVersion = ReportManager.Instance.GetDBVersion();

            if (dbVersion == 0)
            {
                return;
            }

            if (dbVersion == 2)
            {
                UpgradeDb_2_3();
            }

        }

  
        private static void UpgradeDb_2_3()
        {
            LogHelper.Instance.Info("Upgrading db version from 2 to 3.");
            string appPath = AppDomain.CurrentDomain.BaseDirectory;
            string cmdText = "/C " + appPath + "sqlite3.exe " + appPath + "db\\fcodb.db < " + appPath + "dbupgrade_2_3.sql";
            System.Diagnostics.Process.Start("CMD.exe", cmdText);
        }
    }
}
