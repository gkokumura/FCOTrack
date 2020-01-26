using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.IO;


[assembly: log4net.Config.XmlConfigurator(Watch = true)]
namespace FcoMgr
{
    public class LogHelper
    {
        private static log4net.ILog instance;
        public static log4net.ILog Instance
        {
            get
            {
                if (instance == null)
                    instance = GetLogger();
                return instance;
            }
        }

        public static log4net.ILog GetLogger([CallerFilePath]string filename="")
        {
            return log4net.LogManager.GetLogger(filename);
        }

        public static void ExportToFile(string filePath)
        {
            string connStr = "data source=" + System.AppDomain.CurrentDomain.BaseDirectory + "db\\FcoLog.db";
            string query = "SELECT * FROM Log";
            //string logFile = "AppLog.txt";
            string strDelimiter = " ";
            StringBuilder sb = new StringBuilder();
            using (SQLiteConnection conn = new SQLiteConnection(connStr))
            {
                conn.Open();
                using (SQLiteDataReader reader = new SQLiteCommand(query, conn).ExecuteReader())
                {
                    if(reader.HasRows)
                    {

                        Object[] items = new Object[reader.FieldCount];

                        while (reader.Read())
                        {
                            reader.GetValues(items);
                            sb.Append(items[1]); //Timestamp
                            sb.Append(strDelimiter);
                            sb.Append(items[2]); //Level
                            sb.Append(" - ");
                            sb.Append(items[5]); //Message
                            sb.Append(Environment.NewLine);
                        }
                    }
                }
                conn.Close();
            }
            
            File.WriteAllText(filePath, sb.ToString());
        }
    }
}
