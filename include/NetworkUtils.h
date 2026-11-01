#pragma once
#include "Common.h"
#include <vector>
#include <string>

struct NetworkAdapterInfo {
    std::string adapterName;
    std::string ipAddress;
};

class NetworkUtils {
public:
    // Returns a list of available IPv4 addresses and adapter descriptions
    static std::vector<NetworkAdapterInfo> GetLocalIPv4Addresses();
};
