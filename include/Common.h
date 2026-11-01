#pragma once

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif

// Winsock2 MUST be included before Windows.h
#include <winsock2.h>
#include <ws2tcpip.h>
#include <iphlpapi.h>
#include <windows.h>
#include <powrprof.h>

#include <iostream>
#include <string>
#include <vector>
#include <array>
#include <memory>
#include <thread>
#include <mutex>
#include <atomic>
#include <sstream>
#include <algorithm>
#include <cctype>
#include <chrono>
#include <iomanip>
#include <cstdint>
#include <cstring>

#pragma comment(lib, "Ws2_32.lib")
#pragma comment(lib, "User32.lib")
#pragma comment(lib, "Iphlpapi.lib")
#pragma comment(lib, "PowrProf.lib")

// Console ANSI Color codes
namespace ConsoleColor {
    constexpr const char* Reset   = "\033[0m";
    constexpr const char* Bold    = "\033[1m";
    constexpr const char* Dim     = "\033[2m";
    constexpr const char* Red     = "\033[91m";
    constexpr const char* Green   = "\033[92m";
    constexpr const char* Yellow  = "\033[93m";
    constexpr const char* Blue    = "\033[94m";
    constexpr const char* Magenta = "\033[95m";
    constexpr const char* Cyan    = "\033[96m";
    constexpr const char* White   = "\033[97m";
}

// Logger utility
class Logger {
public:
    enum class Level {
        Info,
        Success,
        Warning,
        Error,
        Command
    };

    static void Log(Level level, const std::string& message) {
        static std::mutex logMutex;
        std::lock_guard<std::mutex> lock(logMutex);

        auto now = std::chrono::system_clock::now();
        auto in_time_t = std::chrono::system_clock::to_time_t(now);
        auto ms = std::chrono::duration_cast<std::chrono::milliseconds>(now.time_since_epoch()) % 1000;

        std::tm tmBuffer;
        localtime_s(&tmBuffer, &in_time_t);

        std::cout << ConsoleColor::Dim << "[" 
                  << std::put_time(&tmBuffer, "%H:%M:%S") << "." 
                  << std::setfill('0') << std::setw(3) << ms.count() 
                  << "] " << ConsoleColor::Reset;

        switch (level) {
            case Level::Info:
                std::cout << ConsoleColor::Cyan << "[INFO]    " << ConsoleColor::Reset;
                break;
            case Level::Success:
                std::cout << ConsoleColor::Green << "[SUCCESS] " << ConsoleColor::Reset;
                break;
            case Level::Warning:
                std::cout << ConsoleColor::Yellow << "[WARN]    " << ConsoleColor::Reset;
                break;
            case Level::Error:
                std::cout << ConsoleColor::Red << "[ERROR]   " << ConsoleColor::Reset;
                break;
            case Level::Command:
                std::cout << ConsoleColor::Magenta << "[ACTION]  " << ConsoleColor::Reset;
                break;
        }

        std::cout << message << std::endl;
    }
};

// =============================================================================
// SHA-1 implementation (RFC 3174) — needed for WebSocket handshake
// =============================================================================
namespace Sha1 {
    inline uint32_t rotl32(uint32_t value, unsigned int count) {
        return (value << count) | (value >> (32u - count));
    }

    // Produces a 20-byte SHA-1 digest from input data
    inline std::array<uint8_t, 20> Hash(const uint8_t* data, size_t length) {
        uint32_t h0 = 0x67452301u;
        uint32_t h1 = 0xEFCDAB89u;
        uint32_t h2 = 0x98BADCFEu;
        uint32_t h3 = 0x10325476u;
        uint32_t h4 = 0xC3D2E1F0u;

        // Pre-processing: build padded message
        size_t bitLen = length * 8;
        size_t paddedLen = ((length + 8) / 64 + 1) * 64;
        std::vector<uint8_t> msg(paddedLen, 0);
        std::memcpy(msg.data(), data, length);
        msg[length] = 0x80;
        // Append original length as big-endian 64-bit
        for (int i = 7; i >= 0; --i) {
            msg[paddedLen - 8 + (7 - i)] = static_cast<uint8_t>((bitLen >> (i * 8)) & 0xFF);
        }

        // Process each 512-bit (64-byte) chunk
        for (size_t chunk = 0; chunk < paddedLen; chunk += 64) {
            uint32_t w[80];
            for (int i = 0; i < 16; ++i) {
                w[i] = (static_cast<uint32_t>(msg[chunk + i * 4])     << 24) |
                       (static_cast<uint32_t>(msg[chunk + i * 4 + 1]) << 16) |
                       (static_cast<uint32_t>(msg[chunk + i * 4 + 2]) <<  8) |
                       (static_cast<uint32_t>(msg[chunk + i * 4 + 3]));
            }
            for (int i = 16; i < 80; ++i) {
                w[i] = rotl32(w[i - 3] ^ w[i - 8] ^ w[i - 14] ^ w[i - 16], 1);
            }

            uint32_t a = h0, b = h1, c = h2, d = h3, e = h4;
            for (int i = 0; i < 80; ++i) {
                uint32_t f, k;
                if (i < 20)      { f = (b & c) | ((~b) & d); k = 0x5A827999u; }
                else if (i < 40) { f = b ^ c ^ d;             k = 0x6ED9EBA1u; }
                else if (i < 60) { f = (b & c) | (b & d) | (c & d); k = 0x8F1BBCDCu; }
                else             { f = b ^ c ^ d;             k = 0xCA62C1D6u; }
                uint32_t temp = rotl32(a, 5) + f + e + k + w[i];
                e = d; d = c; c = rotl32(b, 30); b = a; a = temp;
            }
            h0 += a; h1 += b; h2 += c; h3 += d; h4 += e;
        }

        std::array<uint8_t, 20> digest{};
        auto store = [&](uint32_t val, int offset) {
            digest[offset]     = static_cast<uint8_t>((val >> 24) & 0xFF);
            digest[offset + 1] = static_cast<uint8_t>((val >> 16) & 0xFF);
            digest[offset + 2] = static_cast<uint8_t>((val >>  8) & 0xFF);
            digest[offset + 3] = static_cast<uint8_t>( val        & 0xFF);
        };
        store(h0, 0); store(h1, 4); store(h2, 8); store(h3, 12); store(h4, 16);
        return digest;
    }

    inline std::array<uint8_t, 20> Hash(const std::string& s) {
        return Hash(reinterpret_cast<const uint8_t*>(s.data()), s.size());
    }
}

// =============================================================================
// Base64 encoder — needed for WebSocket handshake Sec-WebSocket-Accept header
// =============================================================================
namespace Base64 {
    inline std::string Encode(const uint8_t* data, size_t length) {
        static constexpr char kTable[] =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
        std::string out;
        out.reserve(((length + 2) / 3) * 4);
        for (size_t i = 0; i < length; i += 3) {
            uint32_t triple = (static_cast<uint32_t>(data[i]) << 16);
            if (i + 1 < length) triple |= (static_cast<uint32_t>(data[i + 1]) << 8);
            if (i + 2 < length) triple |=  static_cast<uint32_t>(data[i + 2]);
            out += kTable[(triple >> 18) & 0x3F];
            out += kTable[(triple >> 12) & 0x3F];
            out += (i + 1 < length) ? kTable[(triple >> 6) & 0x3F] : '=';
            out += (i + 2 < length) ? kTable[(triple >> 0) & 0x3F] : '=';
        }
        return out;
    }

    inline std::string Encode(const std::array<uint8_t, 20>& arr) {
        return Encode(arr.data(), arr.size());
    }
}
