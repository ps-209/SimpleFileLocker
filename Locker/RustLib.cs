using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace SimpleFileLocker.Locker
{
    internal class RustLib
    {
        private const string DllPath = "simple_file_locker.dll";

        [DllImport("simple_file_locker.dll",
        EntryPoint = "simple_file", // dumpbin에서 확인한 이름 그대로 입력
        CallingConvention = CallingConvention.Cdecl,
        CharSet = CharSet.Ansi)]
        public static extern int simple_file(string mode, string filePath, string password, string protection);
    }
}
