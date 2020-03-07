using HauppaugeIrBlaster.Controllers;
using System;

namespace HauppaugeIrBlaster
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Uses Hauppauge ir blaster to control a satellite receiver");
                Console.WriteLine();
                Console.WriteLine("Usage:");
                Console.WriteLine("\t-w <channel>\t\tWakes the satellite receiver from standby mode and switches the channel to <channel>");
                Console.WriteLine();

                return 1;
            }

            HirContoller controller = new HirContoller();
            if (controller.IsReady == false)
                return 1;

            int returnResult = 0;

            // execute the requested action
            switch (args[0].ToLower())
            {
                case "-w":
                    returnResult = controller.WakeUp(args[1]);
                    break;
                default:
                    Console.WriteLine("Unknown argument: {0}", args[0].ToLower());
                    break;
            }

            return returnResult;
        }
    }
}