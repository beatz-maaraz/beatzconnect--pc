#pragma once
#include "Common.h"
#include "CommandDispatcher.h"
#include <vector>
#include <thread>
#include <atomic>
#include <mutex>

class NetworkServer {
public:
    NetworkServer(int port, std::shared_ptr<CommandDispatcher> dispatcher);
    ~NetworkServer();

    // Starts WebSocket server + UDP discovery; blocks until Stop() is called
    bool Start();

    // Stops the server and disconnects all clients
    void Stop();

    bool IsRunning() const { return m_running; }
    int GetPort() const { return m_port; }

private:
    int m_port;
    std::shared_ptr<CommandDispatcher> m_dispatcher;
    std::atomic<bool> m_running;
    SOCKET m_listenSocket;

    std::vector<std::thread> m_clientThreads;
    std::mutex m_threadsMutex;

    // ----- WebSocket client handler -----
    void HandleClient(SOCKET clientSocket, sockaddr_in clientAddr);

    // WebSocket RFC 6455 handshake: reads HTTP Upgrade request, sends 101 response.
    // Returns true on success, false if the request is not a valid WS upgrade.
    bool WebSocketHandshake(SOCKET sock);

    // Receive one complete WebSocket text frame from the client.
    // Returns false on connection close / error.
    bool WebSocketReceiveFrame(SOCKET sock, std::string& outPayload);

    // Send a WebSocket text frame to the client.
    bool WebSocketSendFrame(SOCKET sock, const std::string& payload);

    // ----- UDP auto-discovery -----
    // Listens on port (m_port + 1) for "ZPC_DISCOVER" broadcast and replies
    // with "ZPC_RESPONSE:ws://<localIp>:<port>" so the Flutter auto-scan works.
    void RunUdpDiscovery();
};
