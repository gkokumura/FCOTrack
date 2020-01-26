using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data;
using System.Data.OleDb;
using System.Data.SQLite;
using System.Runtime.InteropServices;
using Excel = Microsoft.Office.Interop.Excel;

namespace FcoMgr
{

    public class DailyReport
    {
        //Daily Report column header
        private static string rptDailyColHeaderNo = "No";
        private static string rptDailyColHeaderSN = "SerialNumber";
        private static string rptDailyColHeaderUpgradeCode = "UpgradeCode";
        private static string rptDailyColHeaderModelNumber = "ModelNumber";
        private static string rptDailyColHeaderCompletionStat = "CompletionStatus";
        private static string rptDailyColHeaderProcessedDate = "ProcessedDate";

        //Upgrade Code spreadsheet column header
        private static string UCColHeaderSN = "Serial Number";
        private static string UCColHeaderUpgradeCode = "Upgrade Code";
        private static string UCColHeaderModelNumber = "Model Number";

 //       private string m_fcoNum;
        private DateTime m_dt;


        public DailyReport(DateTime dt)
        {
            m_dt = dt;
        }


        /// <summary>
        /// Determines a text file's encoding by analyzing its byte order mark (BOM).
        /// Defaults to ASCII when detection of the text file's endianness fails.
        /// </summary>
        /// <param name="filename">The text file to analyze.</param>
        /// <returns>The detected encoding.</returns>
        public static Encoding GetEncoding(string filename)
        {
            // Read the BOM
            var bom = new byte[4];
            
            using (var file = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                file.Read(bom, 0, 4);
            }
            
            LogHelper.Instance.Info(string.Format("bom of the file {0} are {1:d} {2:d} {3:d} {4:d}.", filename, bom[0],bom[1],bom[2],bom[3]));

            if (bom[0] == 0x2b && bom[1] == 0x2f && bom[2] == 0x76) return Encoding.UTF7;
            if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf) return Encoding.UTF8;
            if (bom[0] == 0xff && bom[1] == 0xfe) return Encoding.Unicode; //UTF-16LE
            if (bom[0] == 0xfe && bom[1] == 0xff) return Encoding.BigEndianUnicode; //UTF-16BE
            if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xfe && bom[3] == 0xff) return Encoding.UTF32;

            // For case 1&2, go to here, specially for the Philips Upgrade Code files
            if (bom[1] == 0 && bom[3] == 0) return Encoding.Unicode;
            return Encoding.Default;
        }
 
 
        public void Import(string upgradeCodeDir)
        {
            //foreach spread sheet under the dir, import it to database
            DirectoryInfo dirUpgradeCode = new DirectoryInfo(upgradeCodeDir);
            FileInfo[] files = dirUpgradeCode.GetFiles("*.csv");


            foreach (FileInfo fInfo in files)
            {
                Dictionary<string, int> mapHeaderIndex = new Dictionary<string, int>();
                string fileName = fInfo.FullName;
                LogHelper.Instance.Info("START importing UpgradeCode spreadsheet " + fileName);
                Encoding curEncoding = GetEncoding(fileName);

                try
                {

                    using (var reader = new StreamReader(fileName, curEncoding))
                    {

                        var header = reader.ReadLine().Split(',');

                        int i = 0;
                        while (i < header.Length)
                        {
                            if (!mapHeaderIndex.ContainsKey(header[i].Trim()))
                            {
                                mapHeaderIndex.Add(header[i].Trim(), i);
                                i++;
                            }
                            else
                            {
                                LogHelper.Instance.Error("Duplicate column header " + header[i] + "in " + fileName);
                                break;
                            }
                        }

                        if (i != header.Length) // break the loop due to duplicate column header name
                            continue;

                        if (!mapHeaderIndex.ContainsKey(UCColHeaderSN))
                        {
                            LogHelper.Instance.Error("Unable to find column" + UCColHeaderSN + "in " + fileName);
                            
                            continue;
                        }
                        if (!mapHeaderIndex.ContainsKey(UCColHeaderUpgradeCode))
                        {
                            LogHelper.Instance.Error("Unable to find column" + UCColHeaderUpgradeCode + "in " + fileName);
                            
                            continue;
                        }

                        using (var dbContext = new FcoDBEntities())
                        {
                            while (!reader.EndOfStream)
                            {
                                UpgradeResult item = new UpgradeResult();
                                var line = reader.ReadLine();
                                var values = line.Split(',');
                                if (string.IsNullOrWhiteSpace(values[mapHeaderIndex[UCColHeaderSN]]) &&
                                    string.IsNullOrWhiteSpace(values[mapHeaderIndex[UCColHeaderUpgradeCode]]))
                                    continue; //skip the empty line

                                item.Id = Guid.NewGuid().ToString();
                                item.SystemSerialNo = values[mapHeaderIndex[UCColHeaderSN]].Trim().ToUpper();
                                item.UpgradeCode = values[mapHeaderIndex[UCColHeaderUpgradeCode]].Trim().ToUpper();
                                if (mapHeaderIndex.ContainsKey(UCColHeaderModelNumber)) //TC upgrade code
                                    item.ModelNumber = values[mapHeaderIndex[UCColHeaderModelNumber]].Trim().ToUpper();

                                dbContext.UpgradeResults.Add(item);
                            }

                            dbContext.SaveChanges();
                        } //End dbcontext
                    }//End stream reader

                        FCOPath fcoPath = new FCOPath(m_dt);
                        string archiveFilePath = fcoPath.GetArchiveFilePath();
                        ArchiveTo(fileName, archiveFilePath);
                        LogHelper.Instance.Info(string.Format("SUCCESSFULLY imported {0} to database.", fileName));
                    
                }
                catch (Exception e1)
                {
                    LogHelper.Instance.Error(string.Format("FAIL to import upgradecode file {0} due to {1}.", fileName, e1.Message));
                    continue;
                }
            }

        }

        public void GenerateReportTo(string filePath)
        {
            LogHelper.Instance.Info("START generating daily report to" + filePath);

            if (!ProcessData())
                return;

            string[] strCompletionStatus = new string[] 
            {
                "Match",
                "UnMatch",
                "Not Found",
                "Unknown"
            };

            //generate .csv file to file stream
            DataTable dt = new DataTable();
            try
            {
                using (var dbContext = new FcoDBEntities())
                {
                    var query = from r in dbContext.UpgradeResults
                                where r.CompletionStat != 0
                                select new
                                {
                                    SystemSerialNo = r.SystemSerialNo,
                                    UpgradeCode = r.UpgradeCode,
                                    ModelNumber = r.ModelNumber,
                                    CompletionStat = r.CompletionStat
                                };

                    //if (query.Count() == 0)
                    //    return;

                    DataColumn dtcol = dt.Columns.Add(rptDailyColHeaderNo, typeof(Int32));
                    dtcol.AllowDBNull = false;
                    dtcol.Unique = true;
                    dt.Columns.Add(rptDailyColHeaderSN, typeof(String));
                    dt.Columns.Add(rptDailyColHeaderUpgradeCode, typeof(String));
                    dt.Columns.Add(rptDailyColHeaderModelNumber, typeof(String));
                    dt.Columns.Add(rptDailyColHeaderCompletionStat, typeof(String));
                    dt.Columns.Add(rptDailyColHeaderProcessedDate, typeof(DateTime));
                    foreach (var q in query)
                    {
                        DataRow dr = dt.NewRow();
                        dr[rptDailyColHeaderNo] = dt.Rows.Count + 1;
                        dr[rptDailyColHeaderSN] = q.SystemSerialNo;
                        dr[rptDailyColHeaderUpgradeCode] = q.UpgradeCode;
                        dr[rptDailyColHeaderModelNumber] = q.ModelNumber;
                        dr[rptDailyColHeaderCompletionStat] = strCompletionStatus[(int)q.CompletionStat]; //Required to display string other than number
                        dr[rptDailyColHeaderProcessedDate] = m_dt;
                        dt.Rows.Add(dr);
                    }
                }
                if (dt.Rows.Count > 0) 
                {
                    var lines = new List<string>();
                    string[] columnNames = dt.Columns.Cast<DataColumn>().Select(column => column.ColumnName).ToArray();
                    var header = string.Join(",", columnNames);
                    lines.Add(header);
                    var valueLines = dt.AsEnumerable().Select(row => string.Join(",", row.ItemArray));
                    lines.AddRange(valueLines);
                    File.WriteAllLines(filePath, lines);

                    LogHelper.Instance.Info("SUCCESSFULLY generated daily report to " + filePath);
                }
                /*Clear the database for next report*/
                ClearUpgradResultTable();
            }
            catch(Exception e)
            {
                LogHelper.Instance.Error("Fail to generated daily report due to " + e.Message);
            }
        }

        

        private void ArchiveTo(string importFilePath, string archiveFilePath)
        {
            LogHelper.Instance.Info(string.Format("ARCHIVE file {0} to {1}.", importFilePath, archiveFilePath));
            System.IO.File.Move(importFilePath, archiveFilePath);
        }

        private bool ProcessData()
        {

            using (var dbContext = new FcoDBEntities())
            {
                try
                {
                    /*** Find those serial number exists in main UAL ***/
                    var query = from r in dbContext.UpgradeResults
                                join a in dbContext.MainUALs on r.UpgradeCode equals a.UpgradeCode
                                select new
                                {
                                    UAL = a,
                                    UR = r
                                };

                    foreach (var g in query)
                    {
                        //Match
                        if (string.Compare(g.UR.SystemSerialNo,g.UAL.ShippedSystemSerialNo,true) == 0 && 
                            string.Compare(g.UR.ModelNumber, g.UAL.ModelNumber,true) == 0)
                        {
                            if (g.UAL.CompletionDate != null)
                            {
                                g.UR.CompletionStat = (int)Constants.COMPLETIONSTAT.SUCCESS;
                                continue;
                            }

                            g.UAL.ProcessedDate = m_dt;
                            g.UAL.CompletionDate = m_dt;
                            g.UAL.CompletionStat = (int)Constants.COMPLETIONSTAT.SUCCESS;
                            g.UR.CompletionStat = (int)Constants.COMPLETIONSTAT.SUCCESS;
                        }
                        //Unmatch
                        else
                        {
                            //never matched before
                            if (g.UAL.CompletionDate == null)
                            {
                                g.UAL.ProcessedDate = m_dt;
                                g.UAL.CompletionStat = (int)Constants.COMPLETIONSTAT.UNMATCH;
                            }

                            //never matched this time
                            if (g.UR.CompletionStat == null)
                                g.UR.CompletionStat = (int)Constants.COMPLETIONSTAT.UNMATCH;
                        }
                    }

                    dbContext.SaveChanges();

                    //Not found
                    var notFoundQuery = from r in dbContext.UpgradeResults
                                        where r.CompletionStat == null
                                        select new
                                        {
                                            UR = r
                                        };
                    foreach (var g in notFoundQuery)
                    {
                        g.UR.CompletionStat = (int)Constants.COMPLETIONSTAT.NOTFOUND;
                    }

                    dbContext.SaveChanges();

                    return true;
                }
                catch (Exception e)
                {
                    LogHelper.Instance.Error("Fail to process data due to " + e.Message);
                    return false;
                }
                finally
                {
                    dbContext.Dispose();
                }
            }
        }

        private void ClearUpgradResultTable()
        {
            //Clear database data
            using (var dbContext = new FcoDBEntities())
            {
                dbContext.Database.ExecuteSqlCommand("Delete from UpgradeResult");
                LogHelper.Instance.Info("Clear Upgrade Result Table.");
            }
        }
    }

    public class WeeklyReport
    {
        //weekly Report column header
        private static string rptWeeklyColHeaderNo = "No";
        private static string rptWeeklyColHeaderSN = "SerialNumber";
        private static string rptWeeklyColHeaderUpgradeCode = "UpgradeCode";
        private static string rptWeeklyColHeaderModelNumber = "ModelNumber";
        private static string rptWeeklyColHeaderCompletionDate = "CompletionDate";
        private static string rptWeeklyColHeaderCountryName = "CountryName";
        private static string rptWeeklyColHeaderFcoNo = "FCO";
        private static string rptWeeklyColHeaderFcoRev = "FCORev";

        private string m_fco;
        private DateTime m_dt;

        public WeeklyReport(string fco, DateTime dt)
        {
            m_fco = fco;
            m_dt = dt;
        }

        //Generate weekly report from ual table
        public void GenerateReportTo(string filePath)
        {
            LogHelper.Instance.Info("START generating weekly report to " + filePath);
            DataTable dt = new DataTable();
            try
            {
                using (var dbContext = new FcoDBEntities())
                {
                    /********* Add Header to stream lines ********/

                    List<string> lines = new List<string>();
                    string header = rptWeeklyColHeaderNo + "," + rptWeeklyColHeaderSN + "," + 
                        rptWeeklyColHeaderUpgradeCode + "," + rptWeeklyColHeaderModelNumber + "," + 
                        rptWeeklyColHeaderCompletionDate + "," + rptWeeklyColHeaderCountryName + "," + 
                        rptWeeklyColHeaderFcoNo + "," + rptWeeklyColHeaderFcoRev;
                    lines.Add(header);

                    /******* Part I - List all completed items ********/
                
                    /*select the completed items from UAL*/
                    var queryCompleted = from a in dbContext.MainUALs
                                       where a.FCONo.Equals(m_fco) && a.CompletionStat == (int)Constants.COMPLETIONSTAT.SUCCESS
                                       select new
                                       {
                                           SystemSerialNo = a.ShippedSystemSerialNo,
                                           UpgradeCode = a.UpgradeCode,
                                           ModelNumber = a.ModelNumber,
                                           CompletionDate = a.CompletionDate,
                                           Country = a.CountryName,
                                           Fco = a.FCONo,
                                           FcoRev = a.FCORev,
                                       };

                    
                    int count = 1;
                    Dictionary<string, int> mapCountry = new Dictionary<string, int>();
                    foreach (var q in queryCompleted)
                    {
                        string line = count.ToString();
                        line += "," + q.SystemSerialNo;
                        line += "," + q.UpgradeCode;
                        line += "," + q.ModelNumber;
                        line += "," + q.CompletionDate;
                        line += "," + q.Country;
                        line += "," + q.Fco;
                        line += "," + q.FcoRev;
                        lines.Add(line);
                        //if (!mapCountry.ContainsKey(q.Country))
                        //    mapCountry.Add(q.Country, 1);
                        //else
                        //    mapCountry[q.Country]++;
                        count++;
                    }

                    /******** Part II - Query total number of each country from FCO table ********/
                    var queryAll = from f in dbContext.FCOes
                                   where f.FcoNumber.Equals(m_fco)
                                   select new
                                   {
                                       country = f.CountryName,
                                       countAll = f.TotalCount
                                   };

                    int worldWideTotal = 0;
                    int worldWideCompleted = 0;
                    List<string> summaryLines = new List<string>();
                    foreach (var q in queryAll)
                    {

                        int completed = queryCompleted.ToList().Where(x => x.Country.Equals(q.country)).Select(x => x.SystemSerialNo).Distinct().Count();
                        worldWideCompleted += completed;
                        worldWideTotal += q.countAll;
                        double percentage = (double)completed / (double)q.countAll;
                        //complted/total
                       
                        string line = string.Format("Country: {0}, Completed Number: {1}, Completed Percentage: {2:P1}, InCompleted Percentage: {3:P1}", q.country, completed, percentage, 1 - percentage);
                        summaryLines.Add(line);
                    }
                    string worldWide = string.Format("WorldWide, Completed Number: {0}, Completed Percentage: {1:P1}, InCompleted Percentage: {2:P1}", worldWideCompleted, (double)worldWideCompleted / (double)worldWideTotal, 1 - (double)worldWideCompleted / (double)worldWideTotal);
                    summaryLines.Add(worldWide);

                    /******** PartIII - Write To File********/
                    summaryLines.AddRange(lines);
                    File.WriteAllLines(filePath, summaryLines);

                    LogHelper.Instance.Info("SUCCESSFULLY generating weekly report to " + filePath);
                }
            }

            catch (Exception e)
            {
                LogHelper.Instance.Error("Fail to generate weekly report due to " + e.Message);
            }
        }
    }
}
