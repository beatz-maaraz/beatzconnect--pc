cmake_minimum_required(VERSION 3.15)
project(RemoteControlServer LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

include_directories(${CMAKE_CURRENT_SOURCE_DIR}/include)

set(SOURCES
    src/main.cpp
    src/NetworkServer.cpp
    src/CommandDispatcher.cpp
    src/InputController.cpp
    src/NetworkUtils.cpp
)

add_executable(RemoteControlServer ${SOURCES})

# Link required Windows libraries
target_link_libraries(RemoteControlServer PRIVATE
    ws2_32
    user32
    iphlpapi
)

# Output executable directly to bin directory
set_target_properties(RemoteControlServer PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/bin"
    RUNTIME_OUTPUT_DIRECTORY_DEBUG "${CMAKE_CURRENT_SOURCE_DIR}/bin"
    RUNTIME_OUTPUT_DIRECTORY_RELEASE "${CMAKE_CURRENT_SOURCE_DIR}/bin"
)
