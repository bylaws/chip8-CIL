using System;
using System.IO;

namespace Chip8_CIL
{
    class Program
    {
        static void Main(string[] args)
        {
            Settings.Initialise();

            System ch8 = new(File.ReadAllBytes("FONT"), File.ReadAllBytes(args[0]));
            ch8.Run();
            Console.ReadLine();
        }
    }
}
