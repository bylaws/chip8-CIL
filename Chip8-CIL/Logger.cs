using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Chip8_CIL
{
    static class Logger
    {
        public enum Level
        {
            Trace,
            Verbose,
            Debug,
            Error,
            Disable
        }

        private struct FunctionEntry
        {
            public FunctionEntry(string name, Level level)
            {
                Name = name;
                Level = level;

                Stopwatch = new();
                Stopwatch.Start();
            }

            public Stopwatch Stopwatch;
            public readonly string Name;
            public readonly Level Level;
        }

        private static StreamWriter _logWriter;

        private static int _indent = 0;
        private static Level _curLevel = Level.Disable;

        public static bool Shown { get; private set; } = false;

        private readonly static Stack<FunctionEntry> functionStack = new();

        private static void Indent()
        {
            _indent += 4;
        }

        private static void Unindent()
        {
            _indent -= 4;
        }

        public static void Initialise(string path, Level level)
        {
            _logWriter = new(path);
            _curLevel = level;
        }

        public static void StartFunction(string name, Level level)
        {
            functionStack.Push(new FunctionEntry(name, level));

            Log(level, "--- {0}: Start ---", name);
            Indent();
        }

        public static void StopFunction()
        {
            Unindent();

            FunctionEntry entry = functionStack.Pop();
            entry.Stopwatch.Stop();
            Log(entry.Level, "--- {0}: End: {1}ms ---", entry.Name, entry.Stopwatch.Elapsed.TotalMilliseconds);
        }

        public static void Log(string msg, in Level level)
        {
            if (_curLevel <= level)
            {
                string logMessage = new string(' ', _indent) + msg;

                if (Shown)
                    Console.WriteLine(logMessage);

                if (Settings.LogFileEnabled)
                    _logWriter.WriteLine(logMessage);
            }
        }

        public static void Log(Level level, string msg, params Object[] args)
        {
            if (_curLevel <= level)
                Log(string.Format(msg, args), level);
        }


        public static void LogTrace(string msg)
        {
            Log(msg, Level.Trace);
        }

        public static void LogTrace(string msg, params Object[] args)
        {
            Log(Level.Trace, msg, args);
        }

        public static void LogVerbose(string msg)
        {
            Log(msg, Level.Verbose);
        }

        public static void LogVerbose(string msg, params Object[] args)
        {
            Log(Level.Verbose, msg, args);
        }

        public static void LogDebug(string msg)
        {
            Log(msg, Level.Debug);
        }

        public static void LogDebug(string msg, params Object[] args)
        {
            Log(Level.Debug, msg, args);
        }

        public static void LogError(string msg)
        {
            Log(msg, Level.Error);
        }

        public static void LogError(string msg, params Object[] args)
        {
            Log(Level.Error, msg, args);
        }

        public static void ShowLog()
        {
            Terminal.SwitchToAlternativeBuffer();
            Shown = true;
        }

        public static void HideLog()
        {
            Shown = false;
            Terminal.SwitchToMainBuffer();
        }

        public static bool IsLogging(Level level) => _curLevel <= level;
    }
}
