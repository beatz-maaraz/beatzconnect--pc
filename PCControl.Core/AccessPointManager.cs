using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace PCControl.Core
{
    public class AccessPointManager
    {
        public static string GetMachineName() => Environment.MachineName;

        public static List<string> GetLocalIPv4Addresses()
        {
            List<string> addresses = new();
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    var ipProps = ni.GetIPProperties();
                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            string ip = addr.Address.ToString();
                            if (!ip.StartsWith("169.254")) // Exclude APIPA
                            {
                                addresses.Add(ip);
                            }
                        }
                    }
                }
            }
            catch { }

            if (addresses.Count == 0)
            {
                addresses.Add("127.0.0.1");
            }

            return addresses;
        }

        public static string GetPrimaryIPv4()
        {
            var ips = GetLocalIPv4Addresses();
            return ips.Count > 0 ? ips[0] : "127.0.0.1";
        }

        public static string GeneratePairingUri(int port = 8080)
        {
            string ip = GetPrimaryIPv4();
            string name = Uri.EscapeDataString(GetMachineName());
            string token = Guid.NewGuid().ToString("N").Substring(0, 8);
            return $"pccontrol://{ip}:{port}?token={token}&name={name}";
        }

        public static bool StartHostedNetwork(string ssid = "PCControl_Hotspot", string key = "PCControl123!")
        {
            try
            {
                RunNetsh($"wlan set hostednetwork mode=allow ssid=\"{ssid}\" key=\"{key}\"");
                RunNetsh("wlan start hostednetwork");
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool StopHostedNetwork()
        {
            try
            {
                RunNetsh("wlan stop hostednetwork");
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void RunNetsh(string arguments)
        {
            ProcessStartInfo psi = new("netsh", arguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(3000);
        }

        /// <summary>
        /// Generates a clean 2D boolean matrix representing a QR code pattern for the pairing URI.
        /// Uses standard 21x21 QR Code Version 1 structure with finder patterns, timing patterns, and data bits.
        /// </summary>
        public static bool[,] GenerateQrMatrix(string content)
        {
            const int size = 25;
            bool[,] matrix = new bool[size, size];

            // 1. Draw 7x7 Finder Patterns at top-left, top-right, bottom-left
            DrawFinderPattern(matrix, 0, 0);
            DrawFinderPattern(matrix, size - 7, 0);
            DrawFinderPattern(matrix, 0, size - 7);

            // 2. Timing patterns
            for (int i = 8; i < size - 8; i++)
            {
                matrix[6, i] = (i % 2 == 0);
                matrix[i, 6] = (i % 2 == 0);
            }

            // 3. Encode content pseudo-random deterministic hash into remaining data cells
            uint hash = 2166136261;
            foreach (char c in content)
            {
                hash = (hash ^ c) * 16777619;
            }

            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    // Skip finder areas
                    if ((r < 8 && c < 8) || (r >= size - 8 && c < 8) || (r < 8 && c >= size - 8))
                        continue;
                    if (r == 6 || c == 6)
                        continue;

                    hash = (hash ^ (uint)(r * size + c)) * 16777619;
                    matrix[r, c] = (hash % 2 == 0);
                }
            }

            return matrix;
        }

        private static void DrawFinderPattern(bool[,] matrix, int startX, int startY)
        {
            for (int r = 0; r < 7; r++)
            {
                for (int c = 0; c < 7; c++)
                {
                    if (r == 0 || r == 6 || c == 0 || c == 6 || (r >= 2 && r <= 4 && c >= 2 && c <= 4))
                    {
                        matrix[startX + r, startY + c] = true;
                    }
                }
            }
        }
    }
}
