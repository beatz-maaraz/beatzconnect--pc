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
        private LocalIpcServer? _localIpcServer;
        private NamedPipeIpcServer? _pipeIpcServer;
        private RelayConnectionService? _relay;

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
                _logger.LogInformation("Mobile phone connected locally: {Endpoint}", endpoint);
            };

            _server.ClientDisconnected += endpoint =>
            {
                _logger.LogInformation("Mobile phone disconnected locally: {Endpoint}", endpoint);
            };

            _server.CommandExecuted += (endpoint, cmd) =>
            {
                _logger.LogInformation("Executed remote command from {Endpoint}: {Command}", endpoint, cmd);
            };

            _server.Start();

            // Start High-Speed Local IPC Server for Electron Main Process (port 28888)
            _logger.LogInformation("Starting high-speed Local IPC Server on 127.0.0.1:28888...");
            _localIpcServer = new LocalIpcServer(28888, _hardware, _audio, _server);
            _localIpcServer.Start();

            // Start Relay connection to https://backend-n4yg.onrender.com
            try
            {
                _logger.LogInformation("Starting Cloud Relay listener to https://backend-n4yg.onrender.com...");
                _relay = new RelayConnectionService(_hardware, _audio, "https://backend-n4yg.onrender.com", _server);
                _relay.StatusChanged += msg => _logger.LogInformation("[Relay] {Msg}", msg);
                _relay.ConnectionChanged += connected =>
                {
                    _localIpcServer.UpdateRelayState(connected, _relay.RoomId);
                };
                _relay.Start();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Relay initialization warning: {Message}", ex.Message);
            }

            // Also start Named Pipe server for compatibility
            _pipeIpcServer = new NamedPipeIpcServer();
            _pipeIpcServer.Start();

            _logger.LogInformation("PCControl.Service is active, listening for mobile connections on 8080 and Electron IPC on 28888.");

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
            _relay?.Stop();
            _localIpcServer?.Stop();
            _pipeIpcServer?.Stop();
            return base.StopAsync(cancellationToken);
        }
    }
}
