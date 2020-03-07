using HauppaugeIrBlaster.Properties;
using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace HauppaugeIrBlaster.Controllers
{
    public class HirContoller
    {
        // Based on: https://github.com/google/sagetv/blob/master/third_party/Hauppauge/hcwIRblast.h

        #region Definitions

        [DllImport("hcwIRblast.dll")]
        private static extern ushort UIR_Open(uint bVerbose, ushort wIRPort);

        [DllImport("hcwIRblast.dll")]
        private static extern int UIR_Close();

        [DllImport("hcwIRblast.dll")]
        private static extern int UIR_GetConfig(int device, int codeset, ref UIR_CFG cfgPtr);

        [DllImport("hcwIRblast.dll")]
        private static extern int UIR_GotoChannel(int device, int codeset, int channel);

        [DllImport("hcwIRblast.dll")]
        private static extern int UIR_SendKeyCode(int device, int codeset, int key);

        [DllImport("kernel32.dll")]
        private static extern bool SetDllDirectory(string PathName);

        [DllImport("Powrprof.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern bool SetSuspendState(bool hiberate, bool forceCritical, bool disableWakeEvent);

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        private struct UIR_CFG
        {
            public int cfgDataSize;     // Size of this structure, to allow for future expansion - 0x38;
            public int cfgVersion;      // Version of this structure, to allow for future expansion
            public int cfgRegion;       // Current "region" for device selection in UI 
            public int cfgDevice;       // Current Device Type, within Region
            public int cfgVendor;       // Current Vendor, within DeviceType, for codeset selection in UI
            public int cfgCodeset;      // Current CodeSet to use
            public int SendPowerOn;     // Should we send power on each time
            public int PowerOnDelay;    // Time to wait after sending power on
            public int MinDigits;       // Minimum number of digits to send per channel change
            public int DigitDelay;      // Interdigit time to wait
            public int NeedsEnter;      // Should send Enter after digits
            public int EnterDelay;      // Time to wait after Enter key
            public int TuneDelay;       // Time to wait after last digit, if NeedsEnter not set
            public int OneDigitDelay;   // Time to wait after single digit channel, if NeedsEnter not set
        }

        private enum UIRError
        {
            UIRError_Success,
            UIRError_Fail,
            UIRError_DeviceNotInDB,
            UIRError_CodeNotInDB,
            UIRError_KeyNotInDB,
            UIRError_NotSupported,
            UIRError_CorruptDataReceived,
            UIRError_TimeOut,
            UIRError_ChecksumFailed,
            UIRError_FWIncompatible,
            UIRError_InvalidParameter,
            UIRError_NotInitialized
        }

        #endregion

        private const int WAKE_UP_CODE = 10;    // 10 seems to be the magic key for Dish VIP211K receiver, probably emulates a "SELECT" key press

        public bool IsReady { get; } = false;

        public HirContoller()
        {
            // get the ir blaster utilities path
            string irBlasterUtilitiesPath = GetIrBlasterUtilitiesPath();
            if (String.IsNullOrEmpty(irBlasterUtilitiesPath) == false)
                IsReady = true;

            // set the default search ddl path for all interop calls
            HirContoller.SetDllDirectory(irBlasterUtilitiesPath);
        }

        public int WakeUp(string channel)
        {
            // 1. Send WAKE UP to the receiver
            // 2. Wait 1 second
            // 3. Change the channel of the receiver
            // 4. Wait 1 second
            // 5. Stop Hauppauge Recording Service
            // 6. Start Hauppauge Recording Service
            // 7. Put the machine back to sleep mode if there are no active processes

            int returnResult = 0;
            WindowsServiceController windowsServiceController = new WindowsServiceController(Settings.Default.HauppaugeRecordingServiceName);

            try
            {
                int channelNumber;
                if (Int32.TryParse(channel, out channelNumber) == false)
                    throw new Exception($"ERROR: Failed to convert command line parameter: {channel} to channel number");

                // activate the IR blaster port
                OpenIrBlasterPort();

                // read the IR blaster configuration structure
                UIR_CFG irBlasterConfiguration = new UIR_CFG();
                LoadBlasterConfig(ref irBlasterConfiguration);

                // 1. Send WAKE UP to the receiver
                SendKeyCodeToReceiver(WAKE_UP_CODE, irBlasterConfiguration);

                // 2. Wait 1 second
                Thread.Sleep(1000);

                // 3. Change the channel of the receiver
                GoToChannel(channelNumber, irBlasterConfiguration);

                // 4. Wait 1 second
                Thread.Sleep(1000);

                // close the ir blaster port
                CloseIrBlasterPort();

                // 5. Stop Hauppauge Recording Service
                windowsServiceController.StopService();
            }
            catch (Exception ex)
            {
                Console.WriteLine("HirContoller.WakeUp() ERROR: {0}", ex.ToString());
                returnResult = 1;
            }
            finally
            {
                // 6. Start Hauppauge Recording Service
                windowsServiceController.StartService();

                // 7. Put the MCE back to sleep mode if there are no active processes
                SetSuspendState(false, false, false); // sleep mode, do not force processes, enable wake up events
            }

            return returnResult;
        }

        private void OpenIrBlasterPort()
        {
            int portNumber = UIR_Open(0, 0);
            if (portNumber == 0) // returned port must be > 0 for successfull operation
                throw new Exception("ERROR: Failed to open IR blaster port");
        }

        private void CloseIrBlasterPort()
        {
            UIR_Close();
        }

        private string GetIrBlasterUtilitiesPath()
        {
            string irBlasterUtilitiesPath = String.Empty;

            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Wow6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Hauppauge WinTV Infrared Remote"))
                {
                    if (key != null)
                    {
                        irBlasterUtilitiesPath = key.GetValue("UninstallString").ToString();
                        if (irBlasterUtilitiesPath.IndexOf("UNir32") > 0)
                            irBlasterUtilitiesPath = irBlasterUtilitiesPath.Substring(0, irBlasterUtilitiesPath.IndexOf("UNir32"));
                        else if (irBlasterUtilitiesPath.IndexOf("UNIR32") > 0)
                            irBlasterUtilitiesPath = irBlasterUtilitiesPath.Substring(0, irBlasterUtilitiesPath.IndexOf("UNIR32"));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Cannot find IR blaster key in the registry: {ex}");
                irBlasterUtilitiesPath = String.Empty;
            }

            if (String.IsNullOrEmpty(irBlasterUtilitiesPath) == true)
                Console.WriteLine("ERROR: Cannot find default installation location of Hauppauge IR blaster");

            // check for required IR remote assemblies            
            //else
            //{
            //    if (File.Exists(irBlasterUtilitiesPath + "irremote.dll") == false)
            //    {
            //        Console.WriteLine($"ERROR: Cannot find irremote.dll at {irBlasterUtilitiesPath}");
            //        irBlasterUtilitiesPath = String.Empty;
            //    }

            //    if (File.Exists(irBlasterUtilitiesPath + "ir.exe") == false)
            //    {
            //        Console.WriteLine($"ERROR: Cannot find ir.exe at {irBlasterUtilitiesPath}");
            //        irBlasterUtilitiesPath = String.Empty;
            //    }
            //}

            return irBlasterUtilitiesPath;
        }

        private bool LoadBlasterConfig(ref UIR_CFG configuration)
        {
            configuration.cfgDataSize = 0x38;

            int returnValue = UIR_GetConfig(-1, -1, ref configuration);
            if (returnValue == (int)UIRError.UIRError_Success)
            {
                Console.WriteLine("Hauppauge Ir Blaster configuration ...");
                Console.WriteLine(String.Format("Device:          {0}    Vendor:         {1}", configuration.cfgDevice, configuration.cfgVendor));
                Console.WriteLine(String.Format("Region:          {0}    Code Set:       {1}", configuration.cfgRegion, configuration.cfgCodeset));
                Console.WriteLine(String.Format("Digit Delay:     {0}    Minimum Digits: {1}", configuration.DigitDelay, configuration.MinDigits));
                Console.WriteLine(String.Format("Send Power On:   {0}    Power On Delay: {1}", configuration.SendPowerOn, configuration.PowerOnDelay));
                Console.WriteLine(String.Format("One Digit Delay: {0}    Tune Delay:     {1}", configuration.OneDigitDelay, configuration.TuneDelay));
                Console.WriteLine(String.Format("Need Enter:      {0}    Enter Delay:    {1}", configuration.NeedsEnter, configuration.EnterDelay));
            }
            else
                throw new Exception($"ERROR: Failed to read IR blaster configuration: {returnValue}");

            return true;
        }

        private void SendKeyCodeToReceiver(int keyCode, UIR_CFG irBlasterConfiguration)
        {
            int returnValue = UIR_SendKeyCode(irBlasterConfiguration.cfgDevice, irBlasterConfiguration.cfgCodeset, keyCode);
            if (returnValue != (int)UIRError.UIRError_Success)
                throw new Exception($"ERROR: Failed to send a key code to the satellite receiver: {returnValue}");
        }

        private void GoToChannel(int channelNumber, UIR_CFG irBlasterConfiguration)
        {
            int returnValue = UIR_GotoChannel(irBlasterConfiguration.cfgDevice, irBlasterConfiguration.cfgCodeset, channelNumber);
            if (returnValue != (int)UIRError.UIRError_Success)
                throw new Exception($"ERROR: Failed to change the channel: {returnValue}");
        }

        //public bool StartIrService()
        //{
        //    bool returnResult = false;

        //    try
        //    {
        //        string exePath = GetIrBlasterUtilitiesPath() + "Ir.exe";

        //        if (Process.GetProcessesByName("Ir").Length == 0 && File.Exists(exePath) == true)
        //            Process.Start(exePath, "/QUIET");

        //        returnResult = true;
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine("StartIrService() ERROR: {0}", ex.ToString());
        //    }

        //    return returnResult;
        //}

        //public bool StopIrService()
        //{
        //    bool returnResult = false;

        //    try
        //    {
        //        string exePath = GetIrBlasterUtilitiesPath() + "Ir.exe";

        //        if (File.Exists(exePath) == true)
        //        {
        //            Process.Start(exePath, "/QUIT");
        //            Thread.Sleep(500);
        //        }

        //        // needs help closing...
        //        if (Process.GetProcessesByName("Ir").Length != 0)
        //        {
        //            foreach (Process process in Process.GetProcessesByName("Ir"))
        //                process.Kill();
        //        }

        //        returnResult = true;
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine("StopIrService() ERROR: {0}", ex.ToString());
        //    }

        //    return returnResult;
        //}
    }
}
