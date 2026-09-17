using System;
using System.Collections.Generic;
using System.Text;

namespace FlasherCore.Common
{
    public class BaseSession : IDisposable
    {
       
        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                }

                disposedValue = true;
            }
        }
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
