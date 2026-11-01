#include "../include/NetworkServer.h"
#include "../include/NetworkUtils.h"

// =============================================================================
// WebSocket GUID (RFC 6455)
// =============================================================================
static constexpr const char* kWsGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

// =============================================================================
// Constructor / Destructor
// =============================================================================

NetworkServer::NetworkServer(int port, std::shared_ptr<CommandDispatcher> dispatcher)
    : m_port(port), m_dispatcher(std::move(dispatcher)), m_running(false), m_listenSocket(INVALID_SOCKET) {}

NetworkServer::~NetworkServer() {
    Stop();
}

// =============================================================================
// Start — initialise Winsock, bind, listen, launch UDP discovery thread
// =============================================================================

bool NetworkServer::Start() {
    WSADATA wsaData;
    int wsaRes = WSAStartup(MAKEWORD(2, 2), &wsaData);
    if (wsaRes != 0) {
        Logger::Log(Logger::Level::Error, "WSAStartup failed: " + std::to_string(wsaRes));
        return false;
    }

    m_listenSocket = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (m_listenSocket == INVALID_SOCKET) {
        Logger::Log(Logger::Level::Error, "socket() failed: " + std::to_string(WSAGetLastError()));
        WSACleanup();
        return false;
    }

    BOOL reuse = TRUE;
    setsockopt(m_listenSocket, SOL_SOCKET, SO_REUSEADDR, (const char*)&reuse, sizeof(reuse));

    sockaddr_in serverAddr = {0};
    serverAddr.sin_family      = AF_INET;
    serverAddr.sin_addr.s_addr = htonl(INADDR_ANY);
    serverAddr.sin_port        = htons(static_cast<u_short>(m_port));

    if (bind(m_listenSocket, (sockaddr*)&serverAddr, sizeof(serverAddr)) == SOCKET_ERROR) {
        Logger::Log(Logger::Level::Error,
            "Bind failed on port " + std::to_string(m_port) +
            " (Error: " + std::to_string(WSAGetLastError()) + ")");
        closesocket(m_listenSocket);
        m_listenSocket = INVALID_SOCKET;
        WSACleanup();
        return false;
    }

    if (listen(m_listenSocket, SOMAXCONN) == SOCKET_ERROR) {
        Logger::Log(Logger::Level::Error, "listen() failed: " + std::to_string(WSAGetLastError()));
        closesocket(m_listenSocket);
        m_listenSocket = INVALID_SOCKET;
        WSACleanup();
        return false;
    }

    m_running = true;
    Logger::Log(Logger::Level::Success,
        "WebSocket server listening on 0.0.0.0:" + std::to_string(m_port));

    // Launch UDP auto-discovery thread
    std::thread([this]() { RunUdpDiscovery(); }).detach();

    // Accept loop
    while (m_running) {
        sockaddr_in clientAddr = {0};
        int clientAddrLen = sizeof(clientAddr);
        SOCKET clientSocket = accept(m_listenSocket, (sockaddr*)&clientAddr, &clientAddrLen);

        if (clientSocket == INVALID_SOCKET) {
            if (m_running) {
                Logger::Log(Logger::Level::Warning, "accept() error: " + std::to_string(WSAGetLastError()));
            }
            break;
        }

        // Disable Nagle for real-time mouse/touch response
        int flag = 1;
        setsockopt(clientSocket, IPPROTO_TCP, TCP_NODELAY, (char*)&flag, sizeof(int));

        std::thread([this, clientSocket, clientAddr]() {
            HandleClient(clientSocket, clientAddr);
        }).detach();
    }

    return true;
}

// =============================================================================
// Stop
// =============================================================================

void NetworkServer::Stop() {
    if (!m_running.exchange(false)) return;

    Logger::Log(Logger::Level::Info, "Shutting down server...");

    if (m_listenSocket != INVALID_SOCKET) {
        closesocket(m_listenSocket);
        m_listenSocket = INVALID_SOCKET;
    }

    Sleep(100);
    WSACleanup();
    Logger::Log(Logger::Level::Info, "Server stopped.");
}

// =============================================================================
// HandleClient — perform WS handshake then dispatch JSON commands
// =============================================================================

void NetworkServer::HandleClient(SOCKET clientSocket, sockaddr_in clientAddr) {
    char clientIp[INET_ADDRSTRLEN] = {0};
    inet_ntop(AF_INET, &(clientAddr.sin_addr), clientIp, INET_ADDRSTRLEN);
    std::string endpoint = std::string(clientIp) + ":" + std::to_string(ntohs(clientAddr.sin_port));

    Logger::Log(Logger::Level::Info, "New TCP connection from " + endpoint + " — performing WebSocket handshake...");

    // Step 1: WebSocket handshake
    if (!WebSocketHandshake(clientSocket)) {
        Logger::Log(Logger::Level::Warning, "WebSocket handshake failed for " + endpoint + " — closing.");
        closesocket(clientSocket);
        return;
    }

    Logger::Log(Logger::Level::Success, "Mobile device connected via WebSocket: " + endpoint);

    // Step 2: Send initial PC state so Flutter navigates to the dashboard
    WebSocketSendFrame(clientSocket, R"({"pcState":"UNLOCKED"})");

    // Step 3: Message loop
    while (m_running) {
        std::string payload;
        if (!WebSocketReceiveFrame(clientSocket, payload)) {
            break; // connection closed or error
        }

        if (payload.empty()) continue;

        // Dispatch the JSON command; response may be empty (fire-and-forget)
        std::string response = m_dispatcher->Dispatch(payload, endpoint);
        if (!response.empty()) {
            WebSocketSendFrame(clientSocket, response);
        }
    }

    Logger::Log(Logger::Level::Info, "Mobile device disconnected: " + endpoint);
    shutdown(clientSocket, SD_BOTH);
    closesocket(clientSocket);
}

// =============================================================================
// WebSocketHandshake — RFC 6455 §4
// =============================================================================

bool NetworkServer::WebSocketHandshake(SOCKET sock) {
    // Receive HTTP request (up to 4KB)
    char buf[4096] = {0};
    int total = 0;
    while (total < (int)sizeof(buf) - 1) {
        int n = recv(sock, buf + total, (int)sizeof(buf) - 1 - total, 0);
        if (n <= 0) return false;
        total += n;
        // HTTP header ends with \r\n\r\n
        if (total >= 4 &&
            buf[total-4] == '\r' && buf[total-3] == '\n' &&
            buf[total-2] == '\r' && buf[total-1] == '\n') {
            break;
        }
    }
    buf[total] = '\0';
    std::string request(buf);

    // Must be a GET with Upgrade: websocket
    if (request.find("GET ") == std::string::npos ||
        request.find("Upgrade: websocket") == std::string::npos &&
        request.find("Upgrade: WebSocket") == std::string::npos) {
        return false;
    }

    // Extract Sec-WebSocket-Key
    std::string keyHeader = "Sec-WebSocket-Key: ";
    size_t keyPos = request.find(keyHeader);
    if (keyPos == std::string::npos) return false;
    keyPos += keyHeader.size();
    size_t keyEnd = request.find("\r\n", keyPos);
    if (keyEnd == std::string::npos) return false;
    std::string clientKey = request.substr(keyPos, keyEnd - keyPos);

    // Compute accept key: Base64(SHA1(clientKey + GUID))
    std::string combined = clientKey + kWsGuid;
    auto digest = Sha1::Hash(combined);
    std::string acceptKey = Base64::Encode(digest);

    // Send 101 Switching Protocols
    std::string response =
        "HTTP/1.1 101 Switching Protocols\r\n"
        "Upgrade: websocket\r\n"
        "Connection: Upgrade\r\n"
        "Sec-WebSocket-Accept: " + acceptKey + "\r\n"
        "\r\n";

    int sent = send(sock, response.c_str(), (int)response.size(), 0);
    return sent > 0;
}

// =============================================================================
// WebSocketReceiveFrame — RFC 6455 §5 (handles FIN, masking, text frames)
// =============================================================================

bool NetworkServer::WebSocketReceiveFrame(SOCKET sock, std::string& outPayload) {
    outPayload.clear();

    auto recvAll = [&](char* buf, int len) -> bool {
        int received = 0;
        while (received < len) {
            int n = recv(sock, buf + received, len - received, 0);
            if (n <= 0) return false;
            received += n;
        }
        return true;
    };

    // Read first 2 bytes: FIN/opcode and MASK/payload length
    uint8_t header[2];
    if (!recvAll(reinterpret_cast<char*>(header), 2)) return false;

    uint8_t opcode   = header[0] & 0x0F;
    bool    isMasked = (header[1] & 0x80) != 0;
    uint64_t payloadLen = header[1] & 0x7F;

    // Connection close frame
    if (opcode == 0x8) return false;

    // Extended payload length
    if (payloadLen == 126) {
        uint8_t ext[2];
        if (!recvAll(reinterpret_cast<char*>(ext), 2)) return false;
        payloadLen = (static_cast<uint64_t>(ext[0]) << 8) | ext[1];
    } else if (payloadLen == 127) {
        uint8_t ext[8];
        if (!recvAll(reinterpret_cast<char*>(ext), 8)) return false;
        payloadLen = 0;
        for (int i = 0; i < 8; ++i)
            payloadLen = (payloadLen << 8) | ext[i];
    }

    // Masking key (clients MUST mask, per RFC 6455)
    uint8_t maskKey[4] = {0};
    if (isMasked) {
        if (!recvAll(reinterpret_cast<char*>(maskKey), 4)) return false;
    }

    // Sanity limit: 64KB max payload per frame
    if (payloadLen > 65536) return false;

    // Read payload
    std::vector<char> payload(static_cast<size_t>(payloadLen));
    if (payloadLen > 0 && !recvAll(payload.data(), static_cast<int>(payloadLen))) return false;

    // Unmask
    if (isMasked) {
        for (size_t i = 0; i < static_cast<size_t>(payloadLen); ++i) {
            payload[i] ^= maskKey[i % 4];
        }
    }

    // Only handle text frames (opcode 0x1) and continuation (0x0)
    if (opcode == 0x1 || opcode == 0x0) {
        outPayload.assign(payload.begin(), payload.end());
    }
    // Ping frame — respond with pong
    else if (opcode == 0x9) {
        uint8_t pong[2] = {0x8A, 0x00}; // FIN + pong opcode, 0-len payload
        send(sock, reinterpret_cast<char*>(pong), 2, 0);
    }

    return true;
}

// =============================================================================
// WebSocketSendFrame — RFC 6455 §5, server-to-client (no masking required)
// =============================================================================

bool NetworkServer::WebSocketSendFrame(SOCKET sock, const std::string& payload) {
    size_t len = payload.size();
    std::vector<uint8_t> frame;

    // FIN + text opcode
    frame.push_back(0x81);

    // Payload length encoding
    if (len <= 125) {
        frame.push_back(static_cast<uint8_t>(len));
    } else if (len <= 65535) {
        frame.push_back(126);
        frame.push_back(static_cast<uint8_t>((len >> 8) & 0xFF));
        frame.push_back(static_cast<uint8_t>( len       & 0xFF));
    } else {
        frame.push_back(127);
        for (int i = 7; i >= 0; --i)
            frame.push_back(static_cast<uint8_t>((len >> (i * 8)) & 0xFF));
    }

    // Payload (server frames are NOT masked)
    frame.insert(frame.end(), payload.begin(), payload.end());

    int sent = send(sock, reinterpret_cast<char*>(frame.data()), static_cast<int>(frame.size()), 0);
    return sent > 0;
}

// =============================================================================
// RunUdpDiscovery — broadcasts "ZPC_RESPONSE:ws://ip:port" in reply to
//                   "ZPC_DISCOVER" so the Flutter Auto-Scan works
// =============================================================================

void NetworkServer::RunUdpDiscovery() {
    int discoveryPort = m_port + 1; // Flutter sends to 8081 when PC is on 8080

    SOCKET udpSock = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);
    if (udpSock == INVALID_SOCKET) {
        Logger::Log(Logger::Level::Warning, "UDP discovery: failed to create socket.");
        return;
    }

    BOOL bcastEnable = TRUE;
    setsockopt(udpSock, SOL_SOCKET, SO_BROADCAST, (const char*)&bcastEnable, sizeof(bcastEnable));

    BOOL reuse = TRUE;
    setsockopt(udpSock, SOL_SOCKET, SO_REUSEADDR, (const char*)&reuse, sizeof(reuse));

    sockaddr_in udpAddr = {0};
    udpAddr.sin_family      = AF_INET;
    udpAddr.sin_addr.s_addr = htonl(INADDR_ANY);
    udpAddr.sin_port        = htons(static_cast<u_short>(discoveryPort));

    if (bind(udpSock, (sockaddr*)&udpAddr, sizeof(udpAddr)) == SOCKET_ERROR) {
        Logger::Log(Logger::Level::Warning,
            "UDP discovery: bind failed on port " + std::to_string(discoveryPort) +
            " (Error: " + std::to_string(WSAGetLastError()) + ")");
        closesocket(udpSock);
        return;
    }

    Logger::Log(Logger::Level::Info,
        "UDP discovery listener active on port " + std::to_string(discoveryPort));

    // Get local IP addresses to include in the response
    auto adapters = NetworkUtils::GetLocalIPv4Addresses();
    std::string localIp = "127.0.0.1";
    for (const auto& a : adapters) {
        if (a.ipAddress.substr(0, 3) != "127") {
            localIp = a.ipAddress;
            break;
        }
    }
    std::string responsePayload =
        "ZPC_RESPONSE:ws://" + localIp + ":" + std::to_string(m_port);

    char recvBuf[256];
    while (m_running) {
        sockaddr_in senderAddr = {0};
        int senderLen = sizeof(senderAddr);

        // Set a timeout so we can check m_running periodically
        DWORD timeout = 1000; // ms
        setsockopt(udpSock, SOL_SOCKET, SO_RCVTIMEO, (const char*)&timeout, sizeof(timeout));

        int bytes = recvfrom(udpSock, recvBuf, sizeof(recvBuf) - 1, 0,
                             (sockaddr*)&senderAddr, &senderLen);
        if (bytes <= 0) continue;

        recvBuf[bytes] = '\0';
        std::string msg(recvBuf);
        if (msg == "ZPC_DISCOVER") {
            sendto(udpSock, responsePayload.c_str(), (int)responsePayload.size(), 0,
                   (sockaddr*)&senderAddr, senderLen);
            char senderIpBuf[INET_ADDRSTRLEN] = {0};
            inet_ntop(AF_INET, &senderAddr.sin_addr, senderIpBuf, INET_ADDRSTRLEN);
            Logger::Log(Logger::Level::Info,
                "UDP discovery: replied to " + std::string(senderIpBuf));
        }
    }

    closesocket(udpSock);
}
