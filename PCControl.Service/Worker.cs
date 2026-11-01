using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PCControl.Core;

namespace PCControl.Service
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly NativeHardwareController _hardware;
        private readonly AudioEndpointController _audio;
        private WebSocketAccessPointServer? _server;
        private NamedPipeIpcServer? _ipcServer;

        public Worker(ILogger<Worker> logger, NativeHardwareController hardware, AudioEndpointController audio)
        {
            _logger = logger;
            _hardware = hardware;
            _audio = audio;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PCControl.Service starting background Access Point listener on port 8080...");

            _server = new WebSocketAccessPointServer(8080, _hardware, _audio);
            _server.ClientConnected += endpoint =>
            {
                _logger.LogInformation("Mobile phone connected: {Endpoint}", endpoint);
            };

            _server.ClientDisconnected += endpoint =>
            {
                _logger.LogInformation("Mobile phone disconnected: {Endpoint}", endpoint);
            };

            _server.CommandExecuted += (endpoint, cmd) =>
            {
                _logger.LogInformation("Executed remote command from {Endpoint}: {Command}", endpoint, cmd);
            };

            _server.Start();

            // Start IPC server for WPF UI communication
            _ipcServer = new NamedPipeIpcServer();
            _ipcServer.MessageReceived += msg =>
            {
                _logger.LogInformation("Received IPC message from WPF UI: {Type}", msg.Type);
            };
            _ipcServer.Start();

            _logger.LogInformation("PCControl.Service is active and listening for mobile connections.");

            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) { }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping PCControl.Service...");
            _server?.Stop();
            _ipcServer?.Stop();
            return base.StopAsync(cancellationToken);
        }
    }
}
