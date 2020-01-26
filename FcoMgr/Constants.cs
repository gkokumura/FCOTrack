using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FcoMgr
{
    public class Constants
    {
        public enum COMPLETIONSTAT
        {
            SUCCESS = 0,
            UNMATCH = 1 << 0,
            NOTFOUND = 1 << 1
        };

        public enum REPORTTYPE { DAILY, WEEKLY};
    }
}
