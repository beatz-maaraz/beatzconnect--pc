using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PCControl.Core;

namespace PCControl.Service
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                string cmd = args[0].ToLowerInvariant();
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? Assembly.GetExecutingAssembly().Location;
                exePath = exePath.Replace(".dll", ".exe");

                switch (cmd)
                {
                    case "--install":
                        RunSc($"create PCControl.Service binPath= \"{exePath}\" start= auto obj= LocalSystem DisplayName= \"PC Remote Access Point Service\"");
                        RunSc("description PCControl.Service \"Maintains background mobile connectivity and pre-login lock screen unlocking for PC Remote Control.\"");
                        RunSc("start PCControl.Service");
                        return;

                    case "--uninstall":
                        RunSc("stop PCControl.Service");
                        RunSc("delete PCControl.Service");
                        return;

                    case "--start":
                        RunSc("start PCControl.Service");
                        return;

                    case "--stop":
                        RunSc("stop PCControl.Service");
                        return;
                }
            }

            var builder = Host.CreateApplicationBuilder(args);
            if (Microsoft.Extensions.Hosting.WindowsServices.WindowsServiceHelpers.IsWindowsService())
            {
                builder.Services.AddWindowsService(options =>
                {
                    options.ServiceName = "PCControl.Service";
                });
            }

            builder.Services.AddSingleton<NativeHardwareController>();
            builder.Services.AddSingleton<AudioEndpointController>();
            builder.Services.AddHostedService<Worker>();

            var host = builder.Build();
            host.Run();
        }

        private static void RunSc(string arguments)
        {
            try
            {
                ProcessStartInfo psi = new("sc.exe", arguments)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(4000);
            }
            catch { }
        }
    }
}
