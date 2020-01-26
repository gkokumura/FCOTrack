using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.Data.OleDb;
using System.Data;
using System.IO;

namespace FcoMgr
{
    //All sqlite operations here;
    public class DataProcessor
    {
        public List<String> FCOList
        {
            get
            {
                using (FcoDBEntities dbContext = new FcoDBEntities())
                {
                    return dbContext.FCOes.Select(record => record.FcoNumber).ToList();
                }
            }
        }

        public static DateTime Today
        {
            get { return DateTime.Today; }
        }

        //UAL spreadsheet column header
        private static string UALColHeaderShippedSN = "ShippedSystemSerialNo";
        private static string UALColHeaderMaintainedSN = "MaintainedSystemSerialNo";
        private static string UALColHeaderCountryName = "ShippedCountryName";
        private static string UALColHeaderUpgradeCode = "UpgradeCode";

        //Upgrade Code spreadsheet column header
        private static string UCColHeaderSN = "Serial Number";
        private static string UCColHeaderUpgradeCode = "Upgrade Code";

        //Daily Report column header
        private static string rptDailyColHeaderNo = "No";
        private static string rptDailyColHeaderSN = "SerialNumber";
        private static string rptDailyColHeaderUpgradeCode = "UpgradeCode";
        private static string rptDailyColHeaderCompletionStat = "CompletionStatus";
        private static string rptDailyColHeaderProcessedDate = "ProcessedDate";

        //weekly Report column header
        private static string rptWeeklyColHeaderNo = "No";
        private static string rptWeeklyColHeaderSN = "SerialNumber";
        private static string rptWeeklyColHeaderCompletionDate = "CompletionDate";
        private static string rptWeeklyColHeaderCountryName = "CountryName";

        private long GetDBCurMaxId(FcoDBEntities dbContext, int table)
        {
            switch (table)
            {
                case (int)Constants.DBTABLE.MAINUAL:
                    return dbContext.MainUALs.Count() == 0 ? 0 : dbContext.MainUALs.Max(record => record.Id);
                case (int)Constants.DBTABLE.UPGRADERESULT:
                    return dbContext.UpgradeResults.Count() == 0 ? 0 : dbContext.UpgradeResults.Max(record => record.Id);
                case (int)Constants.DBTABLE.FCO:
                    return dbContext.FCOes.Count() == 0 ? 0 : dbContext.FCOes.Max(record => record.Id);
            }
            return 0;
        }

        private DataTable ImportSpreadSheet(string filePath)
        {
            DataTable dt = new DataTable();

            var connString = string.Format(@"Provider=Microsoft.Jet.OleDb.4.0; Data Source={0};Extended Properties=""Text;HDR=YES;FMT=Delimited""",
                Path.GetDirectoryName(filePath));
            
            using (var conn = new OleDbConnection(connString))
            {
                conn.Open();
                var query = "SELECT * FROM [" + Path.GetFileName(filePath) + "]";
                using (var adapter = new OleDbDataAdapter(query, conn))
                {
                    var ds = new DataSet("CSV File");
                    adapter.Fill(ds);
                    dt = ds.Tables[0];
                }

            }

            return dt;
        }

        public bool StoreUAL(string filepath, String fcoNumber)
        {
            /*Import .csv to datatable*/
            DataTable dt = ImportSpreadSheet(filepath);

            /*Check if the ual has already imported*/
            if (FCOList.Contains(fcoNumber))
                return false;

            /*Store datatable to database;*/
            using (var dbContext = new FcoDBEntities())
            {
                long id = GetDBCurMaxId(dbContext, (int)Constants.DBTABLE.MAINUAL);
               
                foreach (DataRow dr in dt.Rows)
                {
                    //Insert new item where no duplicates for specific fco
                    MainUAL newItem = new MainUAL();
                    newItem.Id = ++id;
                    newItem.ShippedSystemSerialNo = dr[UALColHeaderShippedSN].ToString();
                    newItem.MaintainedSystemSerialNo = dr[UALColHeaderMaintainedSN].ToString();
                    newItem.ShippedCountryName = dr[UALColHeaderCountryName].ToString();
                    newItem.UpgradeCode = dr[UALColHeaderUpgradeCode].ToString();
                    newItem.FCONo = fcoNumber;
                    dbContext.MainUALs.Add(newItem);
                }

                //Add FCO number to databse if not exists
                long fcoId = GetDBCurMaxId(dbContext, (int)Constants.DBTABLE.FCO);

                FCO item = new FCO();
                item.Id = ++fcoId;
                item.FcoNumber = fcoNumber;
                dbContext.FCOes.Add(item);
                
                dbContext.SaveChanges();
            }

            return true;
        }

        public void StoreUpgradeCode(string filePath, String fcoNumber)
        {
            DataTable dt = ImportSpreadSheet(filePath);

            using (var dbContext = new FcoDBEntities())
            {
                long id = GetDBCurMaxId(dbContext, (int)Constants.DBTABLE.UPGRADERESULT);
                foreach (DataRow dr in dt.Rows)
                {
                    UpgradeResult item = new UpgradeResult();
                    item.Id = ++id;
                    item.SystemSerialNo = dr[UCColHeaderSN].ToString();
                    item.UpgradeCode = dr[UCColHeaderUpgradeCode].ToString();
                    item.FcoNo = fcoNumber;
                    dbContext.UpgradeResults.Add(item);
                }
                dbContext.SaveChanges();
            }
        }


        public void ProcessUpgradeCode()
        {
            using (var dbContext = new FcoDBEntities())
            {
                /*** Find those serial number exists in main UAL ***/
                var query = from a in dbContext.MainUALs.AsQueryable()
                            join r in dbContext.UpgradeResults on a.ShippedSystemSerialNo equals r.SystemSerialNo
                            where a.FCONo == r.FcoNo
                            select new
                            {
                                UAL = a,
                                UR = r
                            };

                foreach (var o in query)
                {
                    //if completion date is not empty, do not update UAL table
                    if (o.UAL.CompletionDate != null)
                    {
                        o.UR.CompletionStat = o.UAL.CompletionStat;
                        continue;
                    }
                    if (o.UAL.UpgradeCode.Equals(o.UR.UpgradeCode))
                    {
                       
                        o.UAL.ProcessedDate = ReportMgr.today;
                        o.UAL.CompletionDate = Today;
                        o.UAL.CompletionStat = (int)Constants.COMPLETIONSTAT.SUCCESS;
                        o.UR.CompletionStat = (int)Constants.COMPLETIONSTAT.SUCCESS;
                    }
                    else
                    {
                        o.UAL.ProcessedDate = ReportMgr.today;
                        o.UAL.CompletionStat = (int)Constants.COMPLETIONSTAT.UNMATCHUPGRADECODE;
                        o.UR.CompletionStat = (int)Constants.COMPLETIONSTAT.UNMATCHUPGRADECODE;
                    }

                }
                dbContext.SaveChanges();

                /*** Find those serial number not exists in main UAL ***/
                List<MainUAL> emptyUALs = (from a in dbContext.MainUALs
                                           where a.CompletionStat != (int)Constants.COMPLETIONSTAT.SUCCESS && 
                                           a.CompletionStat != (int)Constants.COMPLETIONSTAT.UNMATCHUPGRADECODE
                                           select a).ToList();

                List < UpgradeResult > emptyURs = (from r in dbContext.UpgradeResults
                                                  where r.CompletionStat == null
                                                  select r).ToList();

                foreach (UpgradeResult ur in emptyURs)
                {
                    foreach (MainUAL ua in emptyUALs)
                    {
                        if (ua.FCONo == ur.FcoNo)
                        {
                            if (ua.UpgradeCode == ur.UpgradeCode)
                            {
                                ur.CompletionStat = (int)Constants.COMPLETIONSTAT.NOTFOUND | (int)Constants.COMPLETIONSTAT.UNMATCHUPGRADECODE;
                                ua.CompletionStat = ur.CompletionStat;
                            }
                            else
                            {
                                ur.CompletionStat = (int)Constants.COMPLETIONSTAT.NOTFOUND;
                                ua.CompletionStat = ur.CompletionStat;
                            }
                            ua.ProcessedDate = ReportMgr.today;
                        }
                    }
                }
                dbContext.SaveChanges();
            }
        }

        public void ExportFailureReport(string filePath)
        {

            DataTable dt = new DataTable();
            using (var dbContext = new FcoDBEntities())
            {
                var query = from r in dbContext.UpgradeResults
                            where r.CompletionStat != 0
                            select new
                            {
                                SystemSerialNo = r.SystemSerialNo,
                                UpgradeCode = r.UpgradeCode,
                                CompletionStat = r.CompletionStat
                            };

                DataColumn dtcol = dt.Columns.Add(rptDailyColHeaderNo, typeof(Int32));
                dtcol.AllowDBNull = false;
                dtcol.Unique = true;
                dt.Columns.Add(rptDailyColHeaderSN, typeof(String));
                dt.Columns.Add(rptDailyColHeaderUpgradeCode, typeof(String));
                dt.Columns.Add(rptDailyColHeaderCompletionStat, typeof(Int32));
                dt.Columns.Add(rptDailyColHeaderProcessedDate, typeof(DateTime));
                foreach (var q in query)
                {
                    DataRow dr = dt.NewRow();
                    dr[rptDailyColHeaderNo] = dt.Rows.Count + 1;
                    dr[rptDailyColHeaderSN] = q.SystemSerialNo;
                    dr[rptDailyColHeaderUpgradeCode] = q.UpgradeCode;
                    dr[rptDailyColHeaderCompletionStat] = q.CompletionStat;
                    dr[rptDailyColHeaderProcessedDate] = Today.ToShortDateString();
                    dt.Rows.Add(dr);
                }
            }
            var lines = new List<string>();
            string[] columnNames = dt.Columns.Cast<DataColumn>().Select(column => column.ColumnName).ToArray();
            var header = string.Join(",", columnNames);
            lines.Add(header);
            var valueLines = dt.AsEnumerable().Select(row => string.Join(",", row.ItemArray));
            lines.AddRange(valueLines);
            //if (File.Exists(filePath))
            //   Console.WriteLine("Fail to generate daily report due to file exists");
            File.WriteAllLines(filePath, lines);
        }

        public void ExportWeeklyReport(string fcoNumber, string filePath)
        {
            DataTable dt = new DataTable();
            using (var dbContext = new FcoDBEntities())
            {
                var query = from a in dbContext.MainUALs
                            where a.FCONo.Equals(fcoNumber) && a.CompletionStat == 0
                            select new
                            {
                                SystemSerialNo = a.ShippedSystemSerialNo,
                                CompletionDate = a.CompletionDate,
                                Country = a.ShippedCountryName
                            };
                DataColumn dtcol = dt.Columns.Add(rptWeeklyColHeaderNo, typeof(Int32));
                dtcol.AllowDBNull = false;
                dtcol.Unique = true;
                dt.Columns.Add(rptWeeklyColHeaderSN, typeof(String));
                dt.Columns.Add(rptWeeklyColHeaderCompletionDate, typeof(DateTime));
                dt.Columns.Add(rptWeeklyColHeaderCountryName, typeof(String));
                foreach (var q in query)
                {
                    DataRow dr = dt.NewRow();
                    dr[rptWeeklyColHeaderNo] = dt.Rows.Count + 1;
                    dr[rptWeeklyColHeaderSN] = q.SystemSerialNo;
                    dr[rptWeeklyColHeaderCompletionDate] = q.CompletionDate;
                    dr[rptWeeklyColHeaderCountryName] = q.Country;
                    dt.Rows.Add(dr);
                }

                List<string> lines = new List<string>();
                string[] columnNames = dt.Columns.Cast<DataColumn>().Select(column => column.ColumnName).ToArray();
                string header = string.Join(",", columnNames);
                lines.Add(header);
                var valueLines = dt.AsEnumerable().Select(row => string.Join(",", row.ItemArray));
                lines.AddRange(valueLines);


                Dictionary<string, double> dic = new Dictionary<string, double>();

                var query2 = from a in dbContext.MainUALs
                             where a.FCONo.Equals(fcoNumber)
                             group a by a.ShippedCountryName into countryGroup
                             select new
                             {
                                 CountryName = countryGroup.Key,
                                 RequestCount = countryGroup.Count()
                             };
                foreach (var q in query2)
                    dic.Add(q.CountryName, q.RequestCount);

                var query3 = from a in dbContext.MainUALs
                             where a.CompletionStat == 0 && a.FCONo.Equals(fcoNumber)
                             group a by a.ShippedCountryName into coutryGroup
                             select new
                             {
                                 CountryName = coutryGroup.Key,
                                 CompletedCount = coutryGroup.Count()
                             };
                foreach (var q in query3)
                    dic[q.CountryName] = (double)q.CompletedCount / (double)dic[q.CountryName]; //percentage of each country;

                foreach (KeyValuePair<string, double> kvp in dic)
                {
                    string line = string.Format("Country: {0}, Completed Percentage: {1:P1}", kvp.Key, kvp.Value);
                    lines.Add(line);
                }

                if (!File.Exists(filePath))
                    File.WriteAllLines(filePath, lines);
            }
        }

        public void RemoveAllFromUpgradeResult()
        {
            using (var dbContext = new FcoDBEntities())
            {
                dbContext.Database.ExecuteSqlCommand("delete from UpgradeResult");
            }
        }



    }
}
