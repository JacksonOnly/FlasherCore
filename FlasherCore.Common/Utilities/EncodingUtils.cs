using System;
using System.Collections.Generic;
using System.Text;

namespace FlasherCore.Common.Utilities
{
    public static class EncodingUtils
    {
        public static Encoding GBK 
        { 
            get
            {
                field ??= Encoding.GetEncoding("GBK");
                return field;
            }
        }
        static EncodingUtils()
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }
    }
}
