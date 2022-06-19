using System;

namespace Sample
{
    class Program
    {
        private static void Main()
        {
        #if DEBUG
            Console.WriteLine("DEBUG");
        #else
            Console.WriteLine("Hello World!");
        #endif
        }
    }
}