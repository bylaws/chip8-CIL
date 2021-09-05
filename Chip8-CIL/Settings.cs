using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;

namespace Chip8_CIL
{
    static class Settings
    {
        private static string _exePath = null;

        private static string _dataPath = null;

        private static string _dumpPath = null;

        public const bool LogFileEnabled = true;

        public const bool DumpEnabled = true;

        static public string DumpFolderName => "dumps";

        public const bool BenchmarkEnabled = false;


        static public void Initialise()
        {
            _dataPath = string.Format("{0}/{1}", 
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Process.GetCurrentProcess().ProcessName);

            Directory.CreateDirectory(_dataPath);


            _dumpPath = string.Format("{0}/{1}", _dataPath, DumpFolderName);

            Directory.CreateDirectory(_dumpPath);

            _exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        static public string GetDataPathFor(string name)
        {
            return string.Format("{0}/{1}", _dataPath, name);
        }

        static public string GetDumpPathFor(string name)
        {
            return string.Format("{0}/{1}", _dumpPath, name);
        }
    }
}
