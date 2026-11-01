using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PCControl.Core
{
    public record IpcMessage(string Type, string? Payload = null, int? ClientCount = null, int? Volume = null);

    public class NamedPipeIpcServer
    {
        private const string PipeName = "PCControl_IPC_Pipe";
        private CancellationTokenSource? _cts;
        public event Action<IpcMessage>? MessageReceived;

        public void Start()
        {
            _cts = new CancellationTokenSource();
            Task.Run(() => ServerLoopAsync(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
        }

        private async Task ServerLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // Create Named Pipe with permissions for both LocalSystem and standard user sessions
                    PipeSecurity? pipeSec = new();
                    pipeSec.AddAccessRule(new PipeAccessRule(
                        new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                        PipeAccessRights.ReadWrite,
                        AccessControlType.Allow));

                    using var server = NamedPipeServerStreamAcl.Create(
                        PipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous,
                        0,
                        0,
                        pipeSec);

                    await server.WaitForConnectionAsync(ct);

                    using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
                    using var writer = new StreamWriter(server, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

                    while (!ct.IsCancellationRequested && server.IsConnected)
                    {
                        string? line = await reader.ReadLineAsync(ct);
                        if (line == null) break;

                        var msg = JsonSerializer.Deserialize<IpcMessage>(line);
                        if (msg != null)
                        {
                            MessageReceived?.Invoke(msg);
                        }

                        // Echo back acknowledgement
                        await writer.WriteLineAsync(JsonSerializer.Serialize(new { status = "ack" }));
                    }
                }
                catch when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    await Task.Delay(500, ct);
                }
            }
        }
    }

    public class NamedPipeIpcClient
    {
        private const string PipeName = "PCControl_IPC_Pipe";

        public static async Task<bool> SendMessageAsync(IpcMessage message, int timeoutMs = 1000)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                using var cts = new CancellationTokenSource(timeoutMs);
                await client.ConnectAsync(cts.Token);

                using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
                string json = JsonSerializer.Serialize(message);
                await writer.WriteLineAsync(json.AsMemory(), cts.Token);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsServiceRunning()
        {
            try
            {
                return System.Diagnostics.Process.GetProcessesByName("PCControl.Service").Length > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
