#pragma once
#include "Common.h"
#include "InputController.h"
#include <string>

class CommandDispatcher {
public:
    explicit CommandDispatcher(std::shared_ptr<InputController> inputController);
    ~CommandDispatcher() = default;

    // Dispatches a single command line and returns response string (with trailing \n)
    std::string Dispatch(const std::string& rawLine, const std::string& clientIdentifier);

private:
    std::shared_ptr<InputController> m_input;

    // Split string into tokens
    static std::vector<std::string> Tokenize(const std::string& str);
    static std::string Trim(const std::string& str);
};
