using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MovementSystemServer
{
    public static class Logger
    {
        public static void Log(string s) { Console.ForegroundColor = ConsoleColor.White;  Console.WriteLine(s); }

        public static void LogWarning(string s) { Console.ForegroundColor = ConsoleColor.Yellow; Console.WriteLine(s); }

        public static void LogError(string s) { Console.ForegroundColor = ConsoleColor.Red;  Console.WriteLine(s); }
    }
}
