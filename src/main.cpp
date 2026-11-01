#include "../include/Common.h"
#include "../include/NetworkUtils.h"
#include "../include/InputController.h"
#include "../include/CommandDispatcher.h"
#include "../include/NetworkServer.h"

// Global pointer for graceful shutdown via console control handler
static NetworkServer* g_pServer = nullptr;

BOOL WINAPI ConsoleCtrlHandler(DWORD signal) {
    if (signal == CTRL_C_EVENT || signal == CTRL_CLOSE_EVENT || signal == CTRL_BREAK_EVENT) {
        std::cout << "\n" << ConsoleColor::Yellow << "[INFO] Termination signal received. Shutting down gracefully..." << ConsoleColor::Reset << std::endl;
        if (g_pServer) {
            g_pServer->Stop();
        }
        return TRUE;
    }
    return FALSE;
}

void EnableVirtualTerminalProcessing() {
    HANDLE hOut = GetStdHandle(STD_OUTPUT_HANDLE);
    if (hOut == INVALID_HANDLE_VALUE) return;

    DWORD dwMode = 0;
    if (GetConsoleMode(hOut, &dwMode)) {
        dwMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING;
        SetConsoleMode(hOut, dwMode);
    }
}

void PrintBanner(int port) {
    std::cout << ConsoleColor::Cyan << ConsoleColor::Bold << R"(
=============================================================================
   __  __       _     _ _         ____                  _             
  |  \/  | ___ | |__ (_) | ___   |  _ \ ___ _ __ ___   ___ | |_ ___       
  | |\/| |/ _ \| '_ \| | |/ _ \  | |_) / _ \ '_ ` _ \ / _ \| __/ _ \      
  | |  | | (_) | |_) | | |  __/  |  _ <  __/ | | | | | (_) | ||  __/      
  |_|  |_|\___/|_.__/|_|_|\___|  |_| \_\___|_| |_| |_|\___/ \__\___|      
                   C++ Windows Remote Control Server
=============================================================================
)" << ConsoleColor::Reset << "\n";

    std::cout << ConsoleColor::White << "  Architecture: " << ConsoleColor::Green << "Windows API (SendInput) + Winsock2 (TCP)" << ConsoleColor::Reset << "\n";
    std::cout << ConsoleColor::White << "  Server Port : " << ConsoleColor::Yellow << port << ConsoleColor::Reset << "\n\n";

    std::cout << ConsoleColor::Bold << "  Available Network Interfaces for Mobile Connection:" << ConsoleColor::Reset << "\n";
    auto adapters = NetworkUtils::GetLocalIPv4Addresses();
    if (adapters.empty()) {
        std::cout << ConsoleColor::Yellow << "    [!] No active network adapter found. (Listening on localhost: " << port << ")" << ConsoleColor::Reset << "\n";
    } else {
        for (const auto& adapter : adapters) {
            std::cout << "    * " << ConsoleColor::Cyan << adapter.ipAddress << ConsoleColor::Reset 
                      << "  (" << ConsoleColor::Dim << adapter.adapterName << ConsoleColor::Reset << ")\n";
        }
    }

    std::cout << "\n" << ConsoleColor::Bold << "  Quick Protocol Cheat-Sheet:" << ConsoleColor::Reset << "\n";
    std::cout << "    - Mouse Move : " << ConsoleColor::Yellow << "MOVE <dx> <dy>" << ConsoleColor::Reset << "        (e.g., MOVE 15 -10)\n";
    std::cout << "    - Mouse Click: " << ConsoleColor::Yellow << "CLICK [LEFT|RIGHT|MIDDLE]" << ConsoleColor::Reset << "\n";
    std::cout << "    - Mouse Drag : " << ConsoleColor::Yellow << "DOWN LEFT" << ConsoleColor::Reset << " / " << ConsoleColor::Yellow << "UP LEFT" << ConsoleColor::Reset << "\n";
    std::cout << "    - Scrolling  : " << ConsoleColor::Yellow << "SCROLL <delta>" << ConsoleColor::Reset << "           (e.g., SCROLL 120 / -120)\n";
    std::cout << "    - Type Text  : " << ConsoleColor::Yellow << "TYPE <any text>" << ConsoleColor::Reset << "          (e.g., TYPE Hello World!)\n";
    std::cout << "    - Hotkeys    : " << ConsoleColor::Yellow << "HOTKEY CTRL+C" << ConsoleColor::Reset << "            (e.g., WIN+D, ALT+TAB)\n";
    std::cout << "    - Media Keys : " << ConsoleColor::Yellow << "MEDIA VOL_UP" << ConsoleColor::Reset << "             (PLAY, NEXT, VOL_UP, MUTE)\n";
    std::cout << "    - Status/Help: " << ConsoleColor::Yellow << "RESOLUTION, PING, HELP, QUIT" << ConsoleColor::Reset << "\n";
    std::cout << ConsoleColor::Dim << "=============================================================================\n" << ConsoleColor::Reset << std::endl;
}

int main(int argc, char* argv[]) {
    // Enable UTF-8 console output
    SetConsoleOutputCP(CP_UTF8);
    EnableVirtualTerminalProcessing();
    SetConsoleTitleA("Remote Control Server - C++ Winsock & Windows API");

    int port = 8080;
    if (argc >= 2) {
        try {
            port = std::stoi(argv[1]);
            if (port <= 0 || port > 65535) {
                std::cerr << "Invalid port number. Defaulting to 8080.\n";
                port = 8080;
            }
        } catch (...) {
            std::cerr << "Invalid port argument. Defaulting to 8080.\n";
            port = 8080;
        }
    }

    PrintBanner(port);

    // Initialize subsystems
    auto inputController = std::make_shared<InputController>();
    auto dispatcher = std::make_shared<CommandDispatcher>(inputController);
    NetworkServer server(port, dispatcher);
    g_pServer = &server;

    // Register Ctrl+C handler
    if (!SetConsoleCtrlHandler(ConsoleCtrlHandler, TRUE)) {
        Logger::Log(Logger::Level::Warning, "Could not set console control handler.");
    }

    Logger::Log(Logger::Level::Info, "Starting Remote Control Server on port " + std::to_string(port) + "...");
    bool success = server.Start();

    if (!success) {
        Logger::Log(Logger::Level::Error, "Server failed to start or encountered a fatal error.");
        return 1;
    }

    return 0;
}
