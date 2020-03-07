using System;
using System.ServiceProcess;

namespace HauppaugeIrBlaster.Controllers
{
    public class WindowsServiceController
    {
        private string _serviceName;

        public WindowsServiceController(string serviceName)
        {
            _serviceName = serviceName;
        }

        public void StopService()
        {
            using (ServiceController serviceController = new ServiceController(_serviceName))
            {
                try
                {
                    if (serviceController.Status == ServiceControllerStatus.Running)
                    {
                        serviceController.Stop();
                        serviceController.WaitForStatus(ServiceControllerStatus.Stopped);
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"ERROR: Cannot stop the windows service: {_serviceName}", ex);
                }
            }
        }

        public void StartService()
        {
            using (ServiceController serviceController = new ServiceController(_serviceName))
            {
                try
                {
                    if (serviceController.Status == ServiceControllerStatus.Stopped)
                    {
                        serviceController.Start();
                        serviceController.WaitForStatus(ServiceControllerStatus.Running);
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"ERROR: Cannot start the windows service: {_serviceName}", ex);
                }
            }
        }
    }
}
