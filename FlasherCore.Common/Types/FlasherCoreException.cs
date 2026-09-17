using System;
using System.Collections.Generic;
using System.Text;

namespace FlasherCore.Common.Types
{
    public class FlasherCoreException : Exception
    {
        public FlasherCoreResult FCResult { get;  }
        public FlasherCoreException(FlasherCoreResult result) { FCResult = result; }
    }
}
