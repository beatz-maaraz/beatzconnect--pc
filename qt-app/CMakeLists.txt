cmake_minimum_required(VERSION 3.16)

project(BeatzControl VERSION 1.23.50 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(CMAKE_AUTOMOC ON)
set(CMAKE_AUTORCC ON)
set(CMAKE_AUTOUIC ON)

find_package(QT NAMES Qt6 Qt5 REQUIRED COMPONENTS Core Gui Widgets Qml Quick QuickControls2 Network)
find_package(Qt${QT_VERSION_MAJOR} REQUIRED COMPONENTS Core Gui Widgets Qml Quick QuickControls2 Network)

set(PROJECT_SOURCES
    src/main.cpp
    src/RelayManager.h
    src/RelayManager.cpp
    src/HardwareController.h
    src/HardwareController.cpp
    src/AutoStartManager.h
    src/AutoStartManager.cpp
    resources.qrc
)

if(${QT_VERSION_MAJOR} GREATER_EQUAL 6)
    qt_add_executable(BeatzControl
        MANUAL_FINALIZATION
        ${PROJECT_SOURCES}
    )
else()
    add_executable(BeatzControl WIN32
        ${PROJECT_SOURCES}
    )
endif()

target_include_directories(BeatzControl PRIVATE src)

target_link_libraries(BeatzControl PRIVATE
    Qt${QT_VERSION_MAJOR}::Core
    Qt${QT_VERSION_MAJOR}::Gui
    Qt${QT_VERSION_MAJOR}::Widgets
    Qt${QT_VERSION_MAJOR}::Qml
    Qt${QT_VERSION_MAJOR}::Quick
    Qt${QT_VERSION_MAJOR}::QuickControls2
    Qt${QT_VERSION_MAJOR}::Network
)

if(WIN32)
    target_link_libraries(BeatzControl PRIVATE user32 shell32 advapi32)
    set_target_properties(BeatzControl PROPERTIES
        WIN32_EXECUTABLE TRUE
    )
endif()

if(${QT_VERSION_MAJOR} GREATER_EQUAL 6)
    qt_finalize_executable(BeatzControl)
endif()

# Installation rules for Qt Installer Framework packaging
install(TARGETS BeatzControl DESTINATION .)

