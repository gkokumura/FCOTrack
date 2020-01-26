using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;


namespace FcoMgr
{
    public class ReportManager
    {
        //UAL spreadsheet column header
        private static string UALColHeaderShippedSN = "ShippedSystemSerialNo";
        private static string UALColHeaderMaintainedSN = "MaintainedSystemSerialNo";
        private static string UALColHeaderCountryName = "ResponsibleCountry";
        private static string UALColHeaderUpgradeCode = "UpgradeCode";
        private static string UALColHeaderModelNumber = "ModelNumber";

        private static readonly ReportManager instance = new ReportManager();

        public static ReportManager Instance
        {
            get { return instance; }
        }

        public static List<string> FCOList
        {
            get
            {
                using (FcoDBEntities dbContext = new FcoDBEntities())
                {
                    return dbContext.FCOes.Select(record => record.FcoNumber).Distinct().ToList();
                }
            }
        }

        public static List<string> UniqueFCONumber

        {
            get
            {
                using (FcoDBEntities dbContext = new FcoDBEntities())
                {
                    return dbContext.FcoLists.Select(record => record.uniqueFcoNumber).ToList();
                }
            }
        }

        static ReportManager()
        { }

        private ReportManager()
        { }
        //Create new daily report and copy to report folder despite of the fco number, archive to archive folder
        public async Task GenerateDailyReport()
        {
            if (!FCOPath.TestRootDir())
                return;


            FCOPath fcoPath = new FCOPath(DateTime.Now);
            //DailyReport dailyReport = new DailyReport(fco, DateTime.Now);
            DailyReport dailyReport = new DailyReport(DateTime.Now);
            if (Directory.Exists(fcoPath.GetUpgradeCodeDir()))
            {
                dailyReport.Import(fcoPath.GetUpgradeCodeDir());
                dailyReport.GenerateReportTo(fcoPath.GetReportFilePath((int)Constants.REPORTTYPE.DAILY));
            }
          
        }

        //foreach fco in fco table, create weekly report and copy to report folder
        public async Task GenerateWeeklyReport()
        {
            if (!FCOPath.TestRootDir())
            {
                LogHelper.Instance.Error("Fail to generate weekly report.");
                return;
            }

            foreach(string fco in FCOList)
            {
                FCOPath fcoPath = new FCOPath(fco, DateTime.Now);
                WeeklyReport weeklyReport = new WeeklyReport(fco, DateTime.Now);
                weeklyReport.GenerateReportTo(fcoPath.GetReportFilePath((int)Constants.REPORTTYPE.WEEKLY));
                LogHelper.Instance.Info(string.Format("Generate weekly report for FCO {0}.", fco));
            }
        }

        /* 2019-03-14 Change StoreUAL() function to use SQL for Store UAL bulk insertion, which improves the performance a lot,
         * when insertion 33982 data into database using FCOEntity, it cost around 1-2 minutes,
         * while using insertion command directly, it cost 3-4 seconds.
         * And add error handling.
         */

        public async Task<bool> StoreUAL(string ualFilePath, string fcoNumber, string fcoRev)
        {
            LogHelper.Instance.Info(string.Format("START importing UAL file of FCONumber {0} from {1} to database.", fcoNumber, ualFilePath));
            /*Import .csv to datatable*/
            //DataTable dt = DailyReport.ImportSpreadSheet(ualFilePath);

            /*Check if the ual has already imported*/
            string uniqueFcoNumber = fcoNumber + fcoRev;
            if (UniqueFCONumber.Contains(uniqueFcoNumber))
            {
                LogHelper.Instance.Warn("Terminate importing as fco was already in database.");
                return false;
            }

            try
            {
                Encoding curEncoding = DailyReport.GetEncoding(ualFilePath);


                using (StreamReader reader = new StreamReader(ualFilePath, curEncoding))
                {
                    //Map header and index, to make sure the contents are correctly mapped.
                    var headers = reader.ReadLine().Split(',');
                    Dictionary<string, int> mapHeaderIndex = new Dictionary<string, int>();

                    for (int i = 0; i < headers.Length; i++)
                    {
                        if (mapHeaderIndex.ContainsKey(headers[i].Trim()))
                        {
                            LogHelper.Instance.Error(string.Format("Duplicate column header {0}, please check the UAL file.", headers[i]));
                            return false;
                        }
                        else
                        {
                            mapHeaderIndex.Add(headers[i].Trim(), i);
                        }
                    }

                    //Check if all the required columns are contained in the file.
                    if (!mapHeaderIndex.ContainsKey(UALColHeaderShippedSN))
                    {
                        LogHelper.Instance.Error("Fail to import UAL due to unable to find column " + UALColHeaderShippedSN);
                        return false;
                    }

                    if (!mapHeaderIndex.ContainsKey(UALColHeaderMaintainedSN))
                    {
                        LogHelper.Instance.Error("Fail to import UAL due to unable to find column " + UALColHeaderMaintainedSN);
                        return false;
                    }

                    if (!mapHeaderIndex.ContainsKey(UALColHeaderUpgradeCode))
                    {
                        LogHelper.Instance.Error("Fail to import UAL due to unable to find column " + UALColHeaderUpgradeCode);
                        return false;
                    }

                    if (!mapHeaderIndex.ContainsKey(UALColHeaderCountryName))
                    {
                        LogHelper.Instance.Error("Fail to import UAL due to unable to find column " + UALColHeaderCountryName);
                        return false;
                    }

                    if (mapHeaderIndex.ContainsKey(UALColHeaderModelNumber))
                    {
                        LogHelper.Instance.Info("Model Number column in the UAL table.");
                    }

                    //Connect database and insert all the items from UAL file.
                    var connectionString = "Data Source=" + System.AppDomain.CurrentDomain.BaseDirectory + "db\\FcoDB.db";

                    using (var conn = new SQLiteConnection(connectionString))
                    {
                        conn.Open();

                        var command = conn.CreateCommand();
                        var transaction = conn.BeginTransaction();
                        command.Connection = conn;
                        command.Transaction = transaction;

                        command.CommandText = "INSERT INTO MainUAL(Id, ShippedSystemSerialNo, MaintainedSystemSerialNo, CountryName, UpgradeCode, ModelNumber, FCONo, FCORev) " +
                            "VALUES($Id, $shippedSN, $maintainedSN, $countryName,$upgradeCode, $modelNumber, $fcoNumber, $fcoRev);";

                        var idParameter = command.CreateParameter();
                        idParameter.ParameterName = "$Id";
                        command.Parameters.Add(idParameter);

                        var shippedSNParameter = command.CreateParameter();
                        shippedSNParameter.ParameterName = "$shippedSN";
                        command.Parameters.Add(shippedSNParameter);

                        var maintainedSNParameter = command.CreateParameter();
                        maintainedSNParameter.ParameterName = "$maintainedSN";
                        command.Parameters.Add(maintainedSNParameter);

                        var countryNameParameter = command.CreateParameter();
                        countryNameParameter.ParameterName = "$countryName";
                        command.Parameters.Add(countryNameParameter);

                        var upgradeCodeParameter = command.CreateParameter();
                        upgradeCodeParameter.ParameterName = "$upgradeCode";
                        command.Parameters.Add(upgradeCodeParameter);

                        var modelNumberParameter = command.CreateParameter();
                        modelNumberParameter.ParameterName = "$modelNumber";
                        command.Parameters.Add(modelNumberParameter);

                        var fcoNumberParameter = command.CreateParameter();
                        fcoNumberParameter.ParameterName = "$fcoNumber";
                        command.Parameters.Add(fcoNumberParameter);

                        var fcoRevParameter = command.CreateParameter();
                        fcoRevParameter.ParameterName = "$fcoRev";
                        command.Parameters.Add(fcoRevParameter);

                        while (!reader.EndOfStream)
                        {
                            var line = reader.ReadLine();
                            var values = line.Split(',');

                            idParameter.Value = Guid.NewGuid().ToString();
                            shippedSNParameter.Value = values[mapHeaderIndex[UALColHeaderShippedSN]].Trim();
                            maintainedSNParameter.Value = values[mapHeaderIndex[UALColHeaderMaintainedSN]].Trim();
                            countryNameParameter.Value = values[mapHeaderIndex[UALColHeaderCountryName]].Trim();
                            upgradeCodeParameter.Value = values[mapHeaderIndex[UALColHeaderUpgradeCode]].Trim();
                            if (mapHeaderIndex.ContainsKey(UALColHeaderModelNumber)) //TC ual
                                modelNumberParameter.Value = values[mapHeaderIndex[UALColHeaderModelNumber]].Trim();
                            fcoNumberParameter.Value = fcoNumber;
                            fcoRevParameter.Value = fcoRev;
                            command.ExecuteNonQuery();
                        }
                        try
                        {
                            transaction.Commit();
                        }
                        catch (Exception e)
                        {
                            LogHelper.Instance.Error("Fail to import UAL table due to " + e.Message);

                            //Attempt to roll back the transaction
                            try
                            {
                                transaction.Rollback();
                            }
                            catch (Exception e2)
                            {
                                LogHelper.Instance.Error("Fail to rollback transaction due to " + e2.Message);
                            }

                            return false;
                        } //Catch the exception and rollback transaction

                        LogHelper.Instance.Info("COMPLETE importing UAL table of FCO number" + fcoNumber);
                    }//End using db connection
                }//End using stream reader

                /* 
                 * 2019-05-01 - Modify FCO template and store the total number of each country to save the 
                 * multiple calculation time in weekly report generating.
                 */
                using (var dbContext = new FcoDBEntities())
                {
                    var queryAll = from a in dbContext.MainUALs
                                   where a.FCONo.Equals(fcoNumber)
                                   group a by a.CountryName into gp
                                   select new
                                   {
                                       country = gp.Key,
                                       countAll = gp.Select(q => q.ShippedSystemSerialNo).Distinct().Count(),
                                   };
                    foreach (var q in queryAll)
                    {
                        var queryCurrent = from b in dbContext.FCOes
                         where b.FcoNumber.Equals(fcoNumber) && b.CountryName.Equals(q.country)
                         select b;
                                            
                        if (!queryCurrent.Any())
                        {
                            FCO item = new FCO();
                            item.Id = Guid.NewGuid().ToString();
                            item.FcoNumber = fcoNumber;
                            item.CountryName = q.country;
                            item.TotalCount = q.countAll;
                            dbContext.FCOes.Add(item);
                        }
                        else
                        {
                            queryCurrent.ToList().ForEach(x => x.TotalCount = q.countAll);
                        }
                    }

                    FcoList fcoListItem = new FcoList();
                    fcoListItem.uniqueFcoNumber = uniqueFcoNumber;
                    dbContext.FcoLists.Add(fcoListItem);

                    dbContext.SaveChanges();
                    LogHelper.Instance.Info("Completed insertion into FCO table.");
                } //End using db context

                
                return true; 
            }
            catch (Exception e3)
            {
                LogHelper.Instance.Error("Fail to Store UAL or FCO table due to " + e3.Message);
                return false;
            } //catch all the exceptions

        }

        public int GetDBVersion()
        {
            int dbVersion = 0;
            //should check fcodb in future
            try
            {
                using (var dbContext = new FcoDBEntities())
                {
                    dbVersion = dbContext.Versions
                        .Where(record => record.Id == "MainUAL")
                        .Select(record => record.TableVersion)
                        .FirstOrDefault();
                }
            }
            catch
            {
                LogHelper.Instance.Warn("No db file found");
                return dbVersion;
            }

            return dbVersion;
        }
    }
}
