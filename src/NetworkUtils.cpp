#include "../include/NetworkUtils.h"

std::vector<NetworkAdapterInfo> NetworkUtils::GetLocalIPv4Addresses() {
    std::vector<NetworkAdapterInfo> addresses;

    ULONG bufferSize = 15000;
    PIP_ADAPTER_ADDRESSES pAddresses = nullptr;
    DWORD retVal = 0;

    // Retry loop in case buffer size changes dynamically
    for (int i = 0; i < 3; i++) {
        pAddresses = (IP_ADAPTER_ADDRESSES*)HeapAlloc(GetProcessHeap(), 0, bufferSize);
        if (pAddresses == nullptr) {
            return addresses;
        }

        retVal = GetAdaptersAddresses(
            AF_INET,
            GAA_FLAG_INCLUDE_PREFIX | GAA_FLAG_SKIP_ANYCAST | GAA_FLAG_SKIP_MULTICAST | GAA_FLAG_SKIP_DNS_SERVER,
            nullptr,
            pAddresses,
            &bufferSize
        );

        if (retVal == ERROR_BUFFER_OVERFLOW) {
            HeapFree(GetProcessHeap(), 0, pAddresses);
            pAddresses = nullptr;
        } else {
            break;
        }
    }

    if (retVal == NO_ERROR && pAddresses != nullptr) {
        PIP_ADAPTER_ADDRESSES pCurrAddresses = pAddresses;
        while (pCurrAddresses) {
            // Only examine adapters that are UP
            if (pCurrAddresses->OperStatus == IfOperStatusUp) {
                std::wstring desc = pCurrAddresses->Description ? pCurrAddresses->Description : L"Network Adapter";
                int sizeNeeded = WideCharToMultiByte(CP_UTF8, 0, desc.c_str(), (int)desc.size(), NULL, 0, NULL, NULL);
                std::string descUtf8(sizeNeeded, 0);
                WideCharToMultiByte(CP_UTF8, 0, desc.c_str(), (int)desc.size(), &descUtf8[0], sizeNeeded, NULL, NULL);

                PIP_ADAPTER_UNICAST_ADDRESS pUnicast = pCurrAddresses->FirstUnicastAddress;
                while (pUnicast) {
                    if (pUnicast->Address.lpSockaddr->sa_family == AF_INET) {
                        sockaddr_in* sa_in = (sockaddr_in*)pUnicast->Address.lpSockaddr;
                        char ipStr[INET_ADDRSTRLEN] = {0};
                        inet_ntop(AF_INET, &(sa_in->sin_addr), ipStr, INET_ADDRSTRLEN);

                        std::string ip(ipStr);
                        // Exclude 127.0.0.1 from the primary list or keep it noted
                        if (!ip.empty()) {
                            addresses.push_back({ descUtf8, ip });
                        }
                    }
                    pUnicast = pUnicast->Next;
                }
            }
            pCurrAddresses = pCurrAddresses->Next;
        }
    }

    if (pAddresses) {
        HeapFree(GetProcessHeap(), 0, pAddresses);
    }

    return addresses;
}
