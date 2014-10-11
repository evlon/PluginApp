using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DSS.Platform.Plugin.Emit
{
    public static class Md5String
    {

        public static string ComputeMd5(this string str)
        {
            var bytes = System.Security.Cryptography.MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(str));
            string ret = BitConverter.ToString(bytes).Replace("-","");

            return ret;
                
        }

    }
}
