using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Chip8_CIL
{
    class Logger
    {
        public enum Level
        {
            Trace,
            Verbose,
            Debug,
            Warning,
            Fatal
        }

        public delegate void LoggerOutputAction(Level level, int indent, string msg);

        public Level CurrentLevel;

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

        private readonly Stack<FunctionEntry> functionStack = new();
        private int _indent = 0;

        private Mutex mutex = new();
        private LoggerOutputAction _outputAction;

        public Logger(Level level, LoggerOutputAction outputAction)
        {
            CurrentLevel = level;
            _outputAction = outputAction;
        }

        // Indents all log messages until StopFunction is called and logs how much time was taken
        public void StartFunction(string name, Level level)
        {
            // Released in StopFunction
            mutex.WaitOne();
            functionStack.Push(new(name, level));

            Log(level, "--- {0}: Start ---", name);
            _indent++;
        }

        public void StopFunction()
        {
            _indent--;

            FunctionEntry entry = functionStack.Pop();
            entry.Stopwatch.Stop();
            Log(entry.Level, "--- {0}: End: {1}ms ---", entry.Name, entry.Stopwatch.Elapsed.TotalMilliseconds);
            mutex.ReleaseMutex();
        }

        public void Log(string msg, in Level level)
        {
            mutex.WaitOne();

            if (CurrentLevel <= level)
                _outputAction(level, _indent, msg);

            mutex.ReleaseMutex();
        }

        public void Log(Level level, string msg, params Object[] args)
        {
            if (CurrentLevel <= level)
                Log(string.Format(msg, args), level);
        }

        public void LogTrace(string msg) => Log(msg, Level.Trace);

        public void LogTrace(string msg, params Object[] args) => Log(Level.Trace, msg, args);

        public void LogVerbose(string msg) => Log(msg, Level.Verbose);

        public void LogVerbose(string msg, params Object[] args) => Log(Level.Verbose, msg, args);

        public void LogDebug(string msg) => Log(msg, Level.Debug);

        public void LogDebug(string msg, params Object[] args) => Log(Level.Debug, msg, args);

        public void LogWarning(string msg) => Log(msg, Level.Warning);

        public void LogWarning(string msg, params Object[] args) => Log(Level.Warning, msg, args);

        public void LogFatal(string msg)
        {
            Log(msg, Level.Fatal);
            throw new InvalidOperationException();
        }

        public void LogFatal(string msg, params Object[] args)
        {
            Log(Level.Fatal, msg, args);
            throw new InvalidOperationException();
        }

        public bool IsLogging(Level level) => CurrentLevel <= level;
    }
}
