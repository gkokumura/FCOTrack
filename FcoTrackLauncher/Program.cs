using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FcoMgr;
using System.IO;

namespace FcoTrack
{
    class Program
    {
        static void Main(string[] args)
        {
            Task.WaitAll(LaunchAll(args));
        }

        static async Task LaunchAll(string[] args)
        {
            LogHelper.Instance.Info("******FcoTrackLauncher.exe started******");
            LogHelper.Instance.Info("Revision " + Common.VersionNumber);
            Common.CheckDataFolder();
            string help = "Rev " + Common.VersionNumber + "\n" +
                "'-d' generate daily report  '-w' generate weekly report  '-h' help";

            if (args.Length == 0)
                Console.WriteLine(help);

            if (args[0].Equals("-d"))
            {
                LogHelper.Instance.Info("Run command with argument -d to generate daily report.");
                await Task.Run(async () => await ReportManager.Instance.GenerateDailyReport());
            }
            else if (args[0].Equals("-w"))
            {
                LogHelper.Instance.Info("Run command with argument -w to generate weekly report.");
                await Task.Run(async () => await ReportManager.Instance.GenerateWeeklyReport());
            }
            else
                Console.WriteLine(help);
        }

    }
}
            
    
    


    
    

