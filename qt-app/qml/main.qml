import QtQuick 2.15
import QtQuick.Controls 2.15
import QtQuick.Layouts 1.15
import QtQuick.Window 2.15
import QtQuick.Shapes 1.15

Window {
    id: rootWindow
    width: 1080
    height: 720
    minimumWidth: 880
    minimumHeight: 580
    visible: false
    title: "Beatz Control"
    flags: Qt.Window | Qt.FramelessWindowHint
    color: theme.bgBase

    // ── Application Theme Instance ───────────────────────────────────────────
    Theme {
        id: theme
        mode: "dark" // "dark", "light", "system"
    }

    // ── Navigation & Window State ────────────────────────────────────────────
    property int currentTab: 0 // 0: Dashboard, 1: Apps, 2: Settings, 3: Logs
    property bool minimizeToTray: true
    property int uptimeSeconds: 0

    // Smooth window entrance animation
    opacity: 0.0
    Component.onCompleted: {
        entranceAnim.start();
        uptimeTimer.start();
    }

    NumberAnimation {
        id: entranceAnim
        target: rootWindow
        property: "opacity"
        from: 0.0
        to: 1.0
        duration: 250
        easing.type: Easing.OutCubic
    }

    Timer {
        id: uptimeTimer
        interval: 1000
        repeat: true
        onTriggered: rootWindow.uptimeSeconds++
    }

    function formatUptime(seconds) {
        var h = Math.floor(seconds / 3600);
        var m = Math.floor((seconds % 3600) / 60);
        var s = seconds % 60;
        if (h > 0) return h + "h " + m + "m " + s + "s";
        if (m > 0) return m + "m " + s + "s";
        return s + "s";
    }

    // Model for activity logs
    ListModel {
        id: logModel
    }

    Connections {
        target: relayManager
        function onLogMessage(message) {
            var now = new Date();
            var timeStr = Qt.formatTime(now, "hh:mm:ss.zzz");
            logModel.append({ "time": timeStr, "entry": message });
            if (logModel.count > 500) {
                logModel.remove(0);
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // ROOT LAYOUT
    // ═════════════════════════════════════════════════════════════════════════
    ColumnLayout {
        anchors.fill: parent
        spacing: 0

        // ═════════════════════════════════════════════════════════════════════
        // 1. CUSTOM TITLEBAR (Microsoft Phone Link Fluent Style)
        // ═════════════════════════════════════════════════════════════════════
        Rectangle {
            id: titleBar
            Layout.fillWidth: true
            Layout.preferredHeight: 40
            color: theme.bgSidebar

            // Draggable region
            MouseArea {
                anchors.fill: parent
                acceptedButtons: Qt.LeftButton
                onPressed: rootWindow.startSystemMove()
                onDoubleClicked: {
                    if (rootWindow.visibility === Window.Maximized) {
                        rootWindow.showNormal();
                    } else {
                        rootWindow.showMaximized();
                    }
                }
            }

            RowLayout {
                anchors.fill: parent
                anchors.leftMargin: 14
                anchors.rightMargin: 0
                spacing: 10

                // App Brand Wave Logo
                Rectangle {
                    width: 22
                    height: 22
                    radius: 5
                    color: theme.accentPrimary

                    Shape {
                        anchors.centerIn: parent
                        width: 14
                        height: 10

                        ShapePath {
                            strokeColor: "#FFFFFF"
                            strokeWidth: 1.8
                            fillColor: "transparent"
                            capStyle: ShapePath.RoundCap
                            joinStyle: ShapePath.RoundJoin
                            startX: 0; startY: 8
                            PathLine { x: 3.5; y: 2 }
                            PathLine { x: 7; y: 7 }
                            PathLine { x: 10.5; y: 3 }
                            PathLine { x: 14; y: 8 }
                        }
                    }
                }

                // Application Title
                Text {
                    text: "Beatz Control"
                    font.family: "Segoe UI"
                    font.pixelSize: 13
                    font.weight: Font.DemiBold
                    color: theme.textPrimary
                }

                // Version Badge (Cyan Fluent Style)
                Rectangle {
                    radius: theme.radiusPill
                    color: theme.accentSubtle
                    border.color: theme.accentLight
                    border.width: 1
                    implicitWidth: lblVersion.implicitWidth + 12
                    implicitHeight: 18

                    Text {
                        id: lblVersion
                        anchors.centerIn: parent
                        text: "v1.23.50"
                        font.family: "Segoe UI"
                        font.pixelSize: 10
                        font.weight: Font.Medium
                        color: theme.accentLight
                    }
                }

                // Auto-Start Pill Badge (Visible when autostart registered)
                Rectangle {
                    visible: autoStartManager.isAutoStartEnabled || isSilentStartup
                    radius: theme.radiusPill
                    color: theme.successBg
                    border.color: theme.success
                    border.width: 1
                    implicitWidth: lblAuto.implicitWidth + 12
                    implicitHeight: 18

                    Text {
                        id: lblAuto
                        anchors.centerIn: parent
                        text: "AUTO"
                        font.family: "Segoe UI"
                        font.pixelSize: 10
                        font.weight: Font.Bold
                        color: theme.success
                    }
                }

                Item { Layout.fillWidth: true } // Spacer

                // Window Control Buttons (Chrome)
                Row {
                    Layout.alignment: Qt.AlignRight | Qt.AlignVCenter
                    spacing: 0

                    // Minimize Button
                    Rectangle {
                        width: 46
                        height: 40
                        color: btnMinMouse.containsMouse ? theme.chromeHover : "transparent"
                        Behavior on color { ColorAnimation { duration: 120 } }

                        Rectangle {
                            anchors.centerIn: parent
                            width: 10
                            height: 1
                            radius: 0.5
                            color: theme.textPrimary
                        }

                        MouseArea {
                            id: btnMinMouse
                            anchors.fill: parent
                            hoverEnabled: true
                            onClicked: rootWindow.showMinimized()
                        }
                    }

                    // Maximize / Restore Button
                    Rectangle {
                        width: 46
                        height: 40
                        color: btnMaxMouse.containsMouse ? theme.chromeHover : "transparent"
                        Behavior on color { ColorAnimation { duration: 120 } }

                        // Square icon (Single or Double)
                        Item {
                            anchors.centerIn: parent
                            width: 10
                            height: 10

                            Rectangle {
                                anchors.fill: parent
                                visible: rootWindow.visibility !== Window.Maximized
                                color: "transparent"
                                border.color: theme.textPrimary
                                border.width: 1
                                radius: 1
                            }

                            // Restore double-square
                            Item {
                                anchors.fill: parent
                                visible: rootWindow.visibility === Window.Maximized
                                Rectangle {
                                    x: 2; y: 0; width: 8; height: 8
                                    color: "transparent"; border.color: theme.textPrimary; border.width: 1; radius: 1
                                }
                                Rectangle {
                                    x: 0; y: 2; width: 8; height: 8
                                    color: theme.bgSidebar; border.color: theme.textPrimary; border.width: 1; radius: 1
                                }
                            }
                        }

                        MouseArea {
                            id: btnMaxMouse
                            anchors.fill: parent
                            hoverEnabled: true
                            onClicked: {
                                if (rootWindow.visibility === Window.Maximized) {
                                    rootWindow.showNormal();
                                } else {
                                    rootWindow.showMaximized();
                                }
                            }
                        }
                    }

                    // Close Button (Smooth Red Hover)
                    Rectangle {
                        width: 46
                        height: 40
                        color: btnCloseMouse.containsMouse ? theme.chromeCloseHover : "transparent"
                        Behavior on color { ColorAnimation { duration: 120 } }

                        Shape {
                            anchors.centerIn: parent
                            width: 10
                            height: 10

                            ShapePath {
                                strokeColor: btnCloseMouse.containsMouse ? "#FFFFFF" : theme.textPrimary
                                strokeWidth: 1.1
                                capStyle: ShapePath.RoundCap
                                startX: 0; startY: 0
                                PathLine { x: 10; y: 10 }
                            }
                            ShapePath {
                                strokeColor: btnCloseMouse.containsMouse ? "#FFFFFF" : theme.textPrimary
                                strokeWidth: 1.1
                                capStyle: ShapePath.RoundCap
                                startX: 10; startY: 0
                                PathLine { x: 0; y: 10 }
                            }
                        }

                        MouseArea {
                            id: btnCloseMouse
                            anchors.fill: parent
                            hoverEnabled: true
                            onClicked: {
                                if (rootWindow.minimizeToTray) {
                                    rootWindow.hide();
                                } else {
                                    Qt.quit();
                                }
                            }
                        }
                    }
                }
            }

            // Bottom titlebar divider
            Rectangle {
                anchors.bottom: parent.bottom
                width: parent.width
                height: 1
                color: theme.strokeSubtle
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // 2. MAIN APP SHELL (Sidebar Navigation Rail + Dynamic Views)
        // ═════════════════════════════════════════════════════════════════════
        RowLayout {
            Layout.fillWidth: true
            Layout.fillHeight: true
            spacing: 0

            // ── Sidebar Navigation Rail (Width: 200px) ──────────────────────
            Rectangle {
                Layout.preferredWidth: 200
                Layout.fillHeight: true
                color: theme.bgSidebar

                Rectangle {
                    anchors.right: parent.right
                    width: 1
                    height: parent.height
                    color: theme.strokeSubtle
                }

                ColumnLayout {
                    anchors.fill: parent
                    anchors.margins: 10
                    spacing: 4

                    // Component for Nav Item
                    Component {
                        id: navButtonComp
                        Rectangle {
                            id: navBtn
                            property int tabIndex: 0
                            property string navTitle: ""
                            property string navIconType: "grid"

                            Layout.fillWidth: true
                            Layout.preferredHeight: 40
                            radius: theme.radiusSmall
                            color: {
                                if (rootWindow.currentTab === tabIndex) return theme.accentSubtle;
                                if (navMouse.containsMouse) return theme.isDark ? "#12FFFFFF" : "#0D000000";
                                return "transparent";
                            }
                            Behavior on color { ColorAnimation { duration: 120 } }

                            // Phone Link Active Indicator (Blue left pill bar)
                            Rectangle {
                                anchors.left: parent.left
                                anchors.verticalCenter: parent.verticalCenter
                                width: 3
                                height: 16
                                radius: 1.5
                                color: theme.accentLight
                                visible: rootWindow.currentTab === tabIndex
                            }

                            RowLayout {
                                anchors.fill: parent
                                anchors.leftMargin: 14
                                anchors.rightMargin: 12
                                spacing: 12

                                // Icon representation
                                Item {
                                    width: 18
                                    height: 18

                                    // Dashboard Grid Icon
                                    Grid {
                                        visible: navIconType === "dashboard"
                                        anchors.centerIn: parent
                                        rows: 2; columns: 2; spacing: 2
                                        Rectangle { width: 6; height: 6; radius: 1; color: rootWindow.currentTab === tabIndex ? theme.accentLight : theme.textSecondary }
                                        Rectangle { width: 6; height: 6; radius: 1; color: rootWindow.currentTab === tabIndex ? theme.accentLight : theme.textTertiary }
                                        Rectangle { width: 6; height: 6; radius: 1; color: rootWindow.currentTab === tabIndex ? theme.accentLight : theme.textTertiary }
                                        Rectangle { width: 6; height: 6; radius: 1; color: rootWindow.currentTab === tabIndex ? theme.accentLight : theme.textSecondary }
                                    }

                                    // Active Apps List Icon
                                    Column {
                                        visible: navIconType === "apps"
                                        anchors.centerIn: parent
                                        spacing: 3
                                        Rectangle { width: 14; height: 2; radius: 1; color: rootWindow.currentTab === tabIndex ? theme.accentLight : theme.textSecondary }
                                        Rectangle { width: 14; height: 2; radius: 1; color: rootWindow.currentTab === tabIndex ? theme.accentLight : theme.textSecondary }
                                        Rectangle { width: 8; height: 2; radius: 1; color: rootWindow.currentTab === tabIndex ? theme.accentLight : theme.textTertiary }
                                    }

                                    // Settings Gear Icon
                                    Item {
                                        visible: navIconType === "settings"
                                        anchors.centerIn: parent
                                        width: 16; height: 16
                                        Rectangle {
                                            anchors.centerIn: parent
                                            width: 12; height: 12; radius: 6
                                            color: "transparent"
                                            border.color: rootWindow.currentTab === tabIndex ? theme.accentLight : theme.textSecondary
                                            border.width: 2
                                        }
                                        Rectangle {
                                            anchors.centerIn: parent
                                            width: 4; height: 4; radius: 2
                                            color: rootWindow.currentTab === tabIndex ? theme.accentLight : theme.textSecondary
                                        }
                                    }

                                    // Logs Document Icon
                                    Column {
                                        visible: navIconType === "logs"
                                        anchors.centerIn: parent
                                        spacing: 2.5
                                        Rectangle { width: 13; height: 1.5; radius: 0.5; color: rootWindow.currentTab === tabIndex ? theme.accentLight : theme.textSecondary }
                                        Rectangle { width: 9; height: 1.5; radius: 0.5; color: rootWindow.currentTab === tabIndex ? theme.accentLight : theme.textTertiary }
                                        Rectangle { width: 12; height: 1.5; radius: 0.5; color: rootWindow.currentTab === tabIndex ? theme.accentLight : theme.textSecondary }
                                        Rectangle { width: 6; height: 1.5; radius: 0.5; color: rootWindow.currentTab === tabIndex ? theme.accentLight : theme.textTertiary }
                                    }
                                }

                                Text {
                                    Layout.fillWidth: true
                                    text: navTitle
                                    font.family: "Segoe UI"
                                    font.pixelSize: 13
                                    font.weight: rootWindow.currentTab === tabIndex ? Font.DemiBold : Font.Normal
                                    color: rootWindow.currentTab === tabIndex ? theme.textPrimary : theme.textSecondary
                                }
                            }

                            MouseArea {
                                id: navMouse
                                anchors.fill: parent
                                hoverEnabled: true
                                cursorShape: Qt.PointingHandCursor
                                onClicked: rootWindow.currentTab = tabIndex
                            }
                        }
                    }

                    // 4 Main Navigation Rail Buttons
                    Loader {
                        Layout.fillWidth: true
                        sourceComponent: navButtonComp
                        onLoaded: { item.tabIndex = 0; item.navTitle = "Dashboard"; item.navIconType = "dashboard"; }
                    }
                    Loader {
                        Layout.fillWidth: true
                        sourceComponent: navButtonComp
                        onLoaded: { item.tabIndex = 1; item.navTitle = "Active Apps"; item.navIconType = "apps"; }
                    }
                    Loader {
                        Layout.fillWidth: true
                        sourceComponent: navButtonComp
                        onLoaded: { item.tabIndex = 2; item.navTitle = "Settings"; item.navIconType = "settings"; }
                    }
                    Loader {
                        Layout.fillWidth: true
                        sourceComponent: navButtonComp
                        onLoaded: { item.tabIndex = 3; item.navTitle = "Activity Log"; item.navIconType = "logs"; }
                    }

                    Item { Layout.fillHeight: true } // Spacer

                    // Sidebar Status Bottom Card
                    Rectangle {
                        Layout.fillWidth: true
                        Layout.preferredHeight: 38
                        radius: theme.radiusSmall
                        color: theme.surfaceSecondary
                        border.color: theme.strokeSubtle
                        border.width: 1

                        RowLayout {
                            anchors.fill: parent
                            anchors.margins: 10
                            spacing: 8

                            Rectangle {
                                width: 8
                                height: 8
                                radius: 4
                                color: relayManager.isConnected ? theme.success : theme.warning
                            }

                            Text {
                                Layout.fillWidth: true
                                elide: Text.ElideRight
                                text: relayManager.isConnected ? "QTcpSocket Active" : "Connecting…"
                                font.family: "Segoe UI"
                                font.pixelSize: 11
                                color: theme.textTertiary
                            }
                        }
                    }
                }
            }

            // ── Dynamic Content Views ────────────────────────────────────────
            StackLayout {
                id: mainStack
                Layout.fillWidth: true
                Layout.fillHeight: true
                currentIndex: rootWindow.currentTab

                // ═════════════════════════════════════════════════════════════
                // TAB 0: DASHBOARD
                // ═════════════════════════════════════════════════════════════
                ScrollView {
                    contentWidth: -1
                    clip: true

                    ColumnLayout {
                        anchors.fill: parent
                        anchors.margins: 24
                        spacing: 16

                        // Silent Autostart Notification Banner
                        Rectangle {
                            visible: isSilentStartup
                            Layout.fillWidth: true
                            Layout.preferredHeight: 36
                            radius: theme.radiusSmall
                            color: theme.successBg
                            border.color: theme.success
                            border.width: 1

                            RowLayout {
                                anchors.fill: parent
                                anchors.leftMargin: 12
                                spacing: 8
                                Rectangle { width: 6; height: 6; radius: 3; color: theme.success }
                                Text {
                                    text: "Silent Startup Mode: Beatz Control launched minimized to system tray."
                                    color: theme.success
                                    font.family: "Segoe UI"
                                    font.pixelSize: 12
                                    font.weight: Font.Medium
                                }
                            }
                        }

                        // ── 1. Hero Connection Status Card ───────────────────
                        Rectangle {
                            Layout.fillWidth: true
                            Layout.preferredHeight: 180
                            radius: theme.radiusMedium
                            color: theme.surfaceDefault
                            border.color: theme.strokeDefault
                            border.width: 1

                            ColumnLayout {
                                anchors.fill: parent
                                anchors.margins: 20
                                spacing: 14

                                RowLayout {
                                    Layout.fillWidth: true
                                    spacing: 14

                                    // Pulsing Connection Indicator
                                    Rectangle {
                                        width: 16
                                        height: 16
                                        radius: 8
                                        color: relayManager.isConnected ? theme.success : theme.warning

                                        SequentialAnimation on opacity {
                                            running: true
                                            loops: Animation.Infinite
                                            NumberAnimation { from: 1.0; to: 0.35; duration: 900; easing.type: Easing.InOutQuad }
                                            NumberAnimation { from: 0.35; to: 1.0; duration: 900; easing.type: Easing.InOutQuad }
                                        }
                                    }

                                    ColumnLayout {
                                        Layout.fillWidth: true
                                        spacing: 2

                                        Text {
                                            text: relayManager.isConnected ? "CONNECTED & READY" : "CONNECTING TO RELAY SERVER..."
                                            font.family: "Segoe UI"
                                            font.pixelSize: 16
                                            font.weight: Font.Bold
                                            color: theme.textPrimary
                                        }

                                        Text {
                                            text: relayManager.statusDetail + " (" + relayManager.host + ":" + relayManager.port + ")"
                                            font.family: "Segoe UI"
                                            font.pixelSize: 12
                                            color: theme.textSecondary
                                        }
                                    }

                                    // Metadata Chips
                                    Row {
                                        spacing: 8

                                        Rectangle {
                                            radius: theme.radiusPill
                                            color: theme.surfaceSecondary
                                            border.color: theme.strokeSubtle
                                            implicitWidth: txtPcName.implicitWidth + 16
                                            implicitHeight: 26

                                            Text {
                                                id: txtPcName
                                                anchors.centerIn: parent
                                                text: "PC: " + relayManager.pcName
                                                font.family: "Segoe UI"
                                                font.pixelSize: 11
                                                font.weight: Font.Medium
                                                color: theme.textSecondary
                                            }
                                        }

                                        Rectangle {
                                            radius: theme.radiusPill
                                            color: theme.surfaceSecondary
                                            border.color: theme.strokeSubtle
                                            implicitWidth: txtUptime.implicitWidth + 16
                                            implicitHeight: 26

                                            Text {
                                                id: txtUptime
                                                anchors.centerIn: parent
                                                text: "Uptime: " + formatUptime(rootWindow.uptimeSeconds)
                                                font.family: "Segoe UI"
                                                font.pixelSize: 11
                                                font.weight: Font.Medium
                                                color: theme.textTertiary
                                            }
                                        }
                                    }
                                }

                                Rectangle {
                                    Layout.fillWidth: true
                                    height: 1
                                    color: theme.strokeSubtle
                                }

                                // Metrics 3-Column Sub-Grid
                                RowLayout {
                                    Layout.fillWidth: true
                                    spacing: 14

                                    // Packets Metric
                                    Rectangle {
                                        Layout.fillWidth: true
                                        Layout.preferredHeight: 52
                                        radius: theme.radiusSmall
                                        color: theme.surfaceSecondary
                                        border.color: theme.strokeSubtle

                                        RowLayout {
                                            anchors.fill: parent
                                            anchors.margins: 10
                                            spacing: 10
                                            Text { text: "📦"; font.pixelSize: 16 }
                                            ColumnLayout {
                                                spacing: 1
                                                Text { text: "PACKETS PROCESSED"; font.pixelSize: 9; font.weight: Font.Bold; color: theme.textTertiary }
                                                Text { text: String(relayManager.packetCount); font.pixelSize: 14; font.weight: Font.DemiBold; color: theme.textPrimary }
                                            }
                                        }
                                    }

                                    // Latency Metric
                                    Rectangle {
                                        Layout.fillWidth: true
                                        Layout.preferredHeight: 52
                                        radius: theme.radiusSmall
                                        color: theme.surfaceSecondary
                                        border.color: theme.strokeSubtle

                                        RowLayout {
                                            anchors.fill: parent
                                            anchors.margins: 10
                                            spacing: 10
                                            Text { text: "⚡"; font.pixelSize: 16 }
                                            ColumnLayout {
                                                spacing: 1
                                                Text { text: "ROUND-TRIP LATENCY"; font.pixelSize: 9; font.weight: Font.Bold; color: theme.textTertiary }
                                                Text {
                                                    text: relayManager.latency > 0 ? (relayManager.latency + " ms") : "-- ms"
                                                    font.pixelSize: 14
                                                    font.weight: Font.DemiBold
                                                    color: relayManager.latency < 50 ? theme.success : (relayManager.latency < 150 ? theme.warning : theme.danger)
                                                }
                                            }
                                        }
                                    }

                                    // Protocol Metric
                                    Rectangle {
                                        Layout.fillWidth: true
                                        Layout.preferredHeight: 52
                                        radius: theme.radiusSmall
                                        color: theme.surfaceSecondary
                                        border.color: theme.strokeSubtle

                                        RowLayout {
                                            anchors.fill: parent
                                            anchors.margins: 10
                                            spacing: 10
                                            Text { text: "🔒"; font.pixelSize: 16 }
                                            ColumnLayout {
                                                spacing: 1
                                                Text { text: "CONNECTION PROTOCOL"; font.pixelSize: 9; font.weight: Font.Bold; color: theme.textTertiary }
                                                Text { text: "QTcpSocket / Binary-JSON"; font.pixelSize: 12; font.weight: Font.DemiBold; color: theme.accentLight }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        // Section Label
                        Text {
                            text: "QUICK HARDWARE CONTROLS"
                            font.family: "Segoe UI"
                            font.pixelSize: 11
                            font.weight: Font.Bold
                            color: theme.textTertiary
                        }

                        // ── 2. Hardware Controls Grid ────────────────────────
                        GridLayout {
                            Layout.fillWidth: true
                            columns: 2
                            rowSpacing: 14
                            columnSpacing: 14

                            // Card 1: Lock Workstation
                            Rectangle {
                                Layout.fillWidth: true
                                Layout.preferredHeight: 110
                                radius: theme.radiusMedium
                                color: theme.surfaceDefault
                                border.color: theme.strokeDefault

                                RowLayout {
                                    anchors.fill: parent
                                    anchors.margins: 16
                                    spacing: 16

                                    Rectangle {
                                        width: 44; height: 44; radius: theme.radiusSmall
                                        color: theme.surfaceSecondary
                                        Text { anchors.centerIn: parent; text: "🔒"; font.pixelSize: 22 }
                                    }

                                    ColumnLayout {
                                        Layout.fillWidth: true
                                        spacing: 2
                                        Text { text: "Lock Workstation"; font.pixelSize: 14; font.weight: Font.DemiBold; color: theme.textPrimary }
                                        Text { text: "Instantly locks the Windows session (Win + L)"; font.pixelSize: 11; color: theme.textSecondary }
                                    }

                                    Button {
                                        text: "Lock Now"
                                        onClicked: hardwareController.lockWorkstation()
                                    }
                                }
                            }

                            // Card 2: Reconnect Relay
                            Rectangle {
                                Layout.fillWidth: true
                                Layout.preferredHeight: 110
                                radius: theme.radiusMedium
                                color: theme.surfaceDefault
                                border.color: theme.strokeDefault

                                RowLayout {
                                    anchors.fill: parent
                                    anchors.margins: 16
                                    spacing: 16

                                    Rectangle {
                                        width: 44; height: 44; radius: theme.radiusSmall
                                        color: theme.surfaceSecondary
                                        Text { anchors.centerIn: parent; text: "🔄"; font.pixelSize: 22 }
                                    }

                                    ColumnLayout {
                                        Layout.fillWidth: true
                                        spacing: 2
                                        Text { text: "Relay Socket Reconnect"; font.pixelSize: 14; font.weight: Font.DemiBold; color: theme.textPrimary }
                                        Text { text: "Force reset and re-handshake with server"; font.pixelSize: 11; color: theme.textSecondary }
                                    }

                                    Button {
                                        text: "Reconnect"
                                        onClicked: relayManager.connectToServer()
                                    }
                                }
                            }

                            // Card 3: Master Audio Volume
                            Rectangle {
                                Layout.fillWidth: true
                                Layout.preferredHeight: 125
                                radius: theme.radiusMedium
                                color: theme.surfaceDefault
                                border.color: theme.strokeDefault

                                ColumnLayout {
                                    anchors.fill: parent
                                    anchors.margins: 16
                                    spacing: 8

                                    RowLayout {
                                        Layout.fillWidth: true
                                        Text { text: "🔊  Master Volume"; font.pixelSize: 13; font.weight: Font.DemiBold; color: theme.textPrimary }
                                        Item { Layout.fillWidth: true }
                                        Text {
                                            text: hardwareController.isMuted ? "MUTED" : Math.round(sliderVolume.value) + "%"
                                            font.pixelSize: 12
                                            font.weight: Font.Bold
                                            color: hardwareController.isMuted ? theme.danger : theme.accentLight
                                        }
                                    }

                                    RowLayout {
                                        Layout.fillWidth: true
                                        spacing: 12

                                        Slider {
                                            id: sliderVolume
                                            Layout.fillWidth: true
                                            from: 0
                                            to: 100
                                            value: hardwareController.volume
                                            onMoved: hardwareController.setMasterVolume(Math.round(value))
                                        }

                                        Button {
                                            text: hardwareController.isMuted ? "Unmute" : "Mute"
                                            onClicked: hardwareController.toggleMute()
                                        }
                                    }
                                }
                            }

                            // Card 4: Mobile Hotspot & Firewall
                            Rectangle {
                                Layout.fillWidth: true
                                Layout.preferredHeight: 125
                                radius: theme.radiusMedium
                                color: theme.surfaceDefault
                                border.color: theme.strokeDefault

                                ColumnLayout {
                                    anchors.fill: parent
                                    anchors.margins: 16
                                    spacing: 8

                                    RowLayout {
                                        Layout.fillWidth: true
                                        Text { text: "📡  Network & Firewall"; font.pixelSize: 13; font.weight: Font.DemiBold; color: theme.textPrimary }
                                        Item { Layout.fillWidth: true }
                                        Rectangle {
                                            radius: theme.radiusPill
                                            color: theme.successBg
                                            implicitWidth: 60
                                            implicitHeight: 20
                                            Text { anchors.centerIn: parent; text: "ACTIVE"; font.pixelSize: 9; font.bold: true; color: theme.success }
                                        }
                                    }

                                    RowLayout {
                                        Layout.fillWidth: true
                                        spacing: 10

                                        Button {
                                            Layout.fillWidth: true
                                            text: "Toggle Hotspot"
                                            onClicked: hardwareController.toggleHotspot()
                                        }

                                        Button {
                                            Layout.fillWidth: true
                                            text: "Add Firewall Rule"
                                            onClicked: hardwareController.addFirewallRule()
                                        }
                                    }
                                }
                            }
                        }

                        Item { Layout.fillHeight: true }
                    }
                }

                // ═════════════════════════════════════════════════════════════
                // TAB 1: ACTIVE APPS VIEW
                // ═════════════════════════════════════════════════════════════
                ScrollView {
                    contentWidth: -1
                    clip: true

                    ColumnLayout {
                        anchors.fill: parent
                        anchors.margins: 24
                        spacing: 14

                        RowLayout {
                            Layout.fillWidth: true
                            Text {
                                text: "ACTIVE WINDOWS & PROCESSES"
                                font.family: "Segoe UI"
                                font.pixelSize: 11
                                font.weight: Font.Bold
                                color: theme.textTertiary
                            }

                            Item { Layout.fillWidth: true }

                            Button {
                                text: "🔄 Refresh List"
                                onClicked: appListModel.reload()
                            }
                        }

                        ListModel {
                            id: appListModel
                            Component.onCompleted: reload()

                            function reload() {
                                clear();
                                var list = hardwareController.getActiveWindows();
                                for (var i = 0; i < list.length; i++) {
                                    append(list[i]);
                                }
                            }
                        }

                        // App List Panel
                        Rectangle {
                            Layout.fillWidth: true
                            Layout.preferredHeight: 460
                            radius: theme.radiusMedium
                            color: theme.surfaceDefault
                            border.color: theme.strokeDefault
                            border.width: 1

                            ListView {
                                anchors.fill: parent
                                anchors.margins: 10
                                clip: true
                                model: appListModel
                                spacing: 6

                                delegate: Rectangle {
                                    width: ListView.view.width
                                    height: 48
                                    radius: theme.radiusSmall
                                    color: theme.surfaceSecondary
                                    border.color: theme.strokeSubtle

                                    RowLayout {
                                        anchors.fill: parent
                                        anchors.margins: 12
                                        spacing: 12

                                        Rectangle {
                                            width: 8
                                            height: 8
                                            radius: 4
                                            color: theme.accentLight
                                        }

                                        Text {
                                            Layout.fillWidth: true
                                            elide: Text.ElideRight
                                            text: model.title
                                            font.family: "Segoe UI"
                                            font.pixelSize: 13
                                            font.weight: Font.Medium
                                            color: theme.textPrimary
                                        }

                                        Rectangle {
                                            radius: theme.radiusSmall
                                            color: theme.surfaceTertiary
                                            implicitWidth: txtPid.implicitWidth + 12
                                            implicitHeight: 22

                                            Text {
                                                id: txtPid
                                                anchors.centerIn: parent
                                                text: "PID: " + model.pid
                                                font.pixelSize: 10
                                                font.family: "Consolas"
                                                color: theme.textTertiary
                                            }
                                        }
                                    }
                                }

                                Text {
                                    anchors.centerIn: parent
                                    visible: appListModel.count === 0
                                    text: "No active client windows found. Click Refresh to scan."
                                    font.pixelSize: 12
                                    color: theme.textTertiary
                                }
                            }
                        }
                    }
                }

                // ═════════════════════════════════════════════════════════════
                // TAB 2: SETTINGS (Theme Selection & Configuration)
                // ═════════════════════════════════════════════════════════════
                ScrollView {
                    contentWidth: -1
                    clip: true

                    ColumnLayout {
                        anchors.fill: parent
                        anchors.margins: 24
                        spacing: 16

                        // ── 1. Appearance & Theme Setting Panel ──────────────
                        Text {
                            text: "APPEARANCE & THEME"
                            font.family: "Segoe UI"
                            font.pixelSize: 11
                            font.weight: Font.Bold
                            color: theme.textTertiary
                        }

                        Rectangle {
                            Layout.fillWidth: true
                            Layout.preferredHeight: 140
                            radius: theme.radiusMedium
                            color: theme.surfaceDefault
                            border.color: theme.strokeDefault
                            border.width: 1

                            ColumnLayout {
                                anchors.fill: parent
                                anchors.margins: 16
                                spacing: 12

                                Text {
                                    text: "Select application color scheme (Microsoft Phone Link style)"
                                    font.family: "Segoe UI"
                                    font.pixelSize: 12
                                    color: theme.textSecondary
                                }

                                RowLayout {
                                    Layout.fillWidth: true
                                    spacing: 14

                                    // Option 1: Dark Mode
                                    Rectangle {
                                        Layout.fillWidth: true
                                        Layout.preferredHeight: 64
                                        radius: theme.radiusSmall
                                        color: theme.mode === "dark" ? theme.accentSubtle : theme.surfaceSecondary
                                        border.color: theme.mode === "dark" ? theme.accentLight : theme.strokeDefault
                                        border.width: theme.mode === "dark" ? 2 : 1

                                        RowLayout {
                                            anchors.fill: parent
                                            anchors.margins: 12
                                            spacing: 12

                                            // Dark Mini Preview Box
                                            Rectangle {
                                                width: 38; height: 38; radius: 4; color: "#1C1C1C"; border.color: "#333333"
                                                Rectangle { anchors.left: parent.left; width: 10; height: parent.height; color: "#202020" }
                                                Rectangle { x: 14; y: 8; width: 18; height: 6; radius: 2; color: "#0078D4" }
                                            }

                                            ColumnLayout {
                                                spacing: 1
                                                Text { text: "Dark Mode"; font.pixelSize: 13; font.weight: Font.DemiBold; color: theme.textPrimary }
                                                Text { text: "Signature Phone Link dark aesthetic"; font.pixelSize: 10; color: theme.textTertiary }
                                            }
                                        }

                                        MouseArea {
                                            anchors.fill: parent
                                            cursorShape: Qt.PointingHandCursor
                                            onClicked: theme.mode = "dark"
                                        }
                                    }

                                    // Option 2: Light Mode
                                    Rectangle {
                                        Layout.fillWidth: true
                                        Layout.preferredHeight: 64
                                        radius: theme.radiusSmall
                                        color: theme.mode === "light" ? theme.accentSubtle : theme.surfaceSecondary
                                        border.color: theme.mode === "light" ? theme.accentLight : theme.strokeDefault
                                        border.width: theme.mode === "light" ? 2 : 1

                                        RowLayout {
                                            anchors.fill: parent
                                            anchors.margins: 12
                                            spacing: 12

                                            // Light Mini Preview Box
                                            Rectangle {
                                                width: 38; height: 38; radius: 4; color: "#FFFFFF"; border.color: "#CCCCCC"
                                                Rectangle { anchors.left: parent.left; width: 10; height: parent.height; color: "#EBEBEB" }
                                                Rectangle { x: 14; y: 8; width: 18; height: 6; radius: 2; color: "#005FB8" }
                                            }

                                            ColumnLayout {
                                                spacing: 1
                                                Text { text: "Light Mode"; font.pixelSize: 13; font.weight: Font.DemiBold; color: theme.textPrimary }
                                                Text { text: "Clean Windows 11 light palette"; font.pixelSize: 10; color: theme.textTertiary }
                                            }
                                        }

                                        MouseArea {
                                            anchors.fill: parent
                                            cursorShape: Qt.PointingHandCursor
                                            onClicked: theme.mode = "light"
                                        }
                                    }

                                    // Option 3: System Default
                                    Rectangle {
                                        Layout.fillWidth: true
                                        Layout.preferredHeight: 64
                                        radius: theme.radiusSmall
                                        color: theme.mode === "system" ? theme.accentSubtle : theme.surfaceSecondary
                                        border.color: theme.mode === "system" ? theme.accentLight : theme.strokeDefault
                                        border.width: theme.mode === "system" ? 2 : 1

                                        RowLayout {
                                            anchors.fill: parent
                                            anchors.margins: 12
                                            spacing: 12

                                            // Split Mini Preview Box
                                            Rectangle {
                                                width: 38; height: 38; radius: 4; clip: true; border.color: "#666666"
                                                Rectangle { anchors.left: parent.left; width: 19; height: parent.height; color: "#202020" }
                                                Rectangle { anchors.right: parent.right; width: 19; height: parent.height; color: "#EBEBEB" }
                                            }

                                            ColumnLayout {
                                                spacing: 1
                                                Text { text: "System Default"; font.pixelSize: 13; font.weight: Font.DemiBold; color: theme.textPrimary }
                                                Text { text: "Match Windows OS preference"; font.pixelSize: 10; color: theme.textTertiary }
                                            }
                                        }

                                        MouseArea {
                                            anchors.fill: parent
                                            cursorShape: Qt.PointingHandCursor
                                            onClicked: theme.mode = "system"
                                        }
                                    }
                                }
                            }
                        }

                        // ── 2. Relay Configuration Panel ─────────────────────
                        Text {
                            text: "RELAY CONFIGURATION"
                            font.family: "Segoe UI"
                            font.pixelSize: 11
                            font.weight: Font.Bold
                            color: theme.textTertiary
                            Layout.topMargin: 8
                        }

                        Rectangle {
                            Layout.fillWidth: true
                            Layout.preferredHeight: 120
                            radius: theme.radiusMedium
                            color: theme.surfaceDefault
                            border.color: theme.strokeDefault
                            border.width: 1

                            ColumnLayout {
                                anchors.fill: parent
                                anchors.margins: 16
                                spacing: 8

                                Text { text: "Relay Server Address & Port"; font.pixelSize: 12; color: theme.textSecondary }

                                RowLayout {
                                    Layout.fillWidth: true
                                    spacing: 10

                                    TextField {
                                        id: txtHostInput
                                        Layout.fillWidth: true
                                        text: relayManager.host
                                        placeholderText: "127.0.0.1"
                                    }

                                    TextField {
                                        id: txtPortInput
                                        Layout.preferredWidth: 100
                                        text: String(relayManager.port)
                                        placeholderText: "8080"
                                    }

                                    Button {
                                        text: "Connect"
                                        onClicked: relayManager.connectToServer(txtHostInput.text.trim(), parseInt(txtPortInput.text.trim()))
                                    }
                                }

                                Text {
                                    text: "Service: " + relayManager.statusText + " (" + relayManager.statusDetail + ")"
                                    font.pixelSize: 11
                                    color: theme.textTertiary
                                }
                            }
                        }

                        // ── 3. Startup & Background Behavior Panel ───────────
                        Text {
                            text: "STARTUP & BACKGROUND BEHAVIOR"
                            font.family: "Segoe UI"
                            font.pixelSize: 11
                            font.weight: Font.Bold
                            color: theme.textTertiary
                            Layout.topMargin: 8
                        }

                        Rectangle {
                            Layout.fillWidth: true
                            Layout.preferredHeight: 160
                            radius: theme.radiusMedium
                            color: theme.surfaceDefault
                            border.color: theme.strokeDefault
                            border.width: 1

                            ColumnLayout {
                                anchors.fill: parent
                                anchors.margins: 16
                                spacing: 14

                                // Toggle 1: Auto Start
                                RowLayout {
                                    Layout.fillWidth: true
                                    spacing: 12

                                    Rectangle {
                                        width: 38; height: 20; radius: 10
                                        color: autoStartManager.isAutoStartEnabled ? theme.accentPrimary : theme.surfaceTertiary
                                        border.color: autoStartManager.isAutoStartEnabled ? theme.accentPrimary : theme.strokeStrong
                                        Behavior on color { ColorAnimation { duration: 150 } }

                                        Rectangle {
                                            width: 14; height: 14; radius: 7
                                            x: autoStartManager.isAutoStartEnabled ? 21 : 3
                                            anchors.verticalCenter: parent.verticalCenter
                                            color: "#FFFFFF"
                                            Behavior on x { NumberAnimation { duration: 150; easing.type: Easing.OutCubic } }
                                        }

                                        MouseArea {
                                            anchors.fill: parent
                                            cursorShape: Qt.PointingHandCursor
                                            onClicked: autoStartManager.setAutoStartEnabled(!autoStartManager.isAutoStartEnabled)
                                        }
                                    }

                                    Text {
                                        text: "Launch automatically on Windows logon (silent background / tray mode)"
                                        font.pixelSize: 13
                                        color: theme.textPrimary
                                    }
                                }

                                // Toggle 2: Minimize to Tray
                                RowLayout {
                                    Layout.fillWidth: true
                                    spacing: 12

                                    Rectangle {
                                        width: 38; height: 20; radius: 10
                                        color: rootWindow.minimizeToTray ? theme.accentPrimary : theme.surfaceTertiary
                                        border.color: rootWindow.minimizeToTray ? theme.accentPrimary : theme.strokeStrong
                                        Behavior on color { ColorAnimation { duration: 150 } }

                                        Rectangle {
                                            width: 14; height: 14; radius: 7
                                            x: rootWindow.minimizeToTray ? 21 : 3
                                            anchors.verticalCenter: parent.verticalCenter
                                            color: "#FFFFFF"
                                            Behavior on x { NumberAnimation { duration: 150; easing.type: Easing.OutCubic } }
                                        }

                                        MouseArea {
                                            anchors.fill: parent
                                            cursorShape: Qt.PointingHandCursor
                                            onClicked: rootWindow.minimizeToTray = !rootWindow.minimizeToTray
                                        }
                                    }

                                    Text {
                                        text: "Minimize to tray instead of closing (keeps QTcpSocket relay alive in background)"
                                        font.pixelSize: 13
                                        color: theme.textPrimary
                                    }
                                }

                                // Startup Status Pill
                                Rectangle {
                                    Layout.fillWidth: true
                                    Layout.preferredHeight: 32
                                    radius: theme.radiusSmall
                                    color: theme.surfaceSecondary
                                    border.color: theme.strokeSubtle

                                    RowLayout {
                                        anchors.fill: parent
                                        anchors.margins: 8
                                        spacing: 8

                                        Rectangle {
                                            width: 6; height: 6; radius: 3
                                            color: autoStartManager.isAutoStartEnabled ? theme.success : theme.warning
                                        }

                                        Text {
                                            text: autoStartManager.isAutoStartEnabled
                                                  ? "Windows startup: Configured in HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run"
                                                  : "Windows startup: Disabled"
                                            font.pixelSize: 11
                                            color: theme.textSecondary
                                        }
                                    }
                                }
                            }
                        }

                        // ── 4. Auto-Connection Information Card ──────────────
                        Text {
                            text: "AUTO-CONNECTION STATUS"
                            font.family: "Segoe UI"
                            font.pixelSize: 11
                            font.weight: Font.Bold
                            color: theme.textTertiary
                            Layout.topMargin: 8
                        }

                        Rectangle {
                            Layout.fillWidth: true
                            Layout.preferredHeight: 90
                            radius: theme.radiusMedium
                            color: theme.surfaceDefault
                            border.color: theme.strokeDefault
                            border.width: 1

                            ColumnLayout {
                                anchors.fill: parent
                                anchors.margins: 16
                                spacing: 4

                                RowLayout {
                                    Layout.fillWidth: true
                                    Text {
                                        text: "Zero-Touch Automatic Relay Connection"
                                        font.pixelSize: 13
                                        font.weight: Font.DemiBold
                                        color: theme.textPrimary
                                    }
                                    Item { Layout.fillWidth: true }
                                    Rectangle {
                                        radius: theme.radiusPill
                                        color: theme.accentSubtle
                                        border.color: theme.accentLight
                                        implicitWidth: 80
                                        implicitHeight: 20
                                        Text { anchors.centerIn: parent; text: "ALWAYS ON"; font.pixelSize: 9; font.bold: true; color: theme.accentLight }
                                    }
                                }

                                Text {
                                    text: "The application initializes QTcpSocket instantly on startup with exponential backoff retry (3s to 60s) to guarantee high availability."
                                    font.pixelSize: 11
                                    color: theme.textTertiary
                                    wrapMode: Text.WordWrap
                                    Layout.fillWidth: true
                                }
                            }
                        }

                        Item { Layout.fillHeight: true }
                    }
                }

                // ═════════════════════════════════════════════════════════════
                // TAB 3: ACTIVITY LOG VIEW
                // ═════════════════════════════════════════════════════════════
                Rectangle {
                    color: "transparent"

                    ColumnLayout {
                        anchors.fill: parent
                        anchors.margins: 24
                        spacing: 12

                        RowLayout {
                            Layout.fillWidth: true

                            Text {
                                text: "ACTIVITY LOG"
                                font.family: "Segoe UI"
                                font.pixelSize: 11
                                font.weight: Font.Bold
                                color: theme.textTertiary
                            }

                            Rectangle {
                                radius: theme.radiusPill
                                color: theme.surfaceSecondary
                                implicitWidth: txtLogCount.implicitWidth + 12
                                implicitHeight: 20

                                Text {
                                    id: txtLogCount
                                    anchors.centerIn: parent
                                    text: logModel.count + " entries"
                                    font.pixelSize: 10
                                    color: theme.textTertiary
                                }
                            }

                            Item { Layout.fillWidth: true }

                            Button {
                                text: "Clear Logs"
                                onClicked: logModel.clear()
                            }
                        }

                        // Terminal Log Box
                        Rectangle {
                            Layout.fillWidth: true
                            Layout.fillHeight: true
                            radius: theme.radiusSmall
                            color: theme.logBg
                            border.color: theme.strokeDefault
                            border.width: 1

                            ListView {
                                id: logListView
                                anchors.fill: parent
                                anchors.margins: 12
                                clip: true
                                model: logModel
                                spacing: 4

                                onCountChanged: {
                                    Qt.callLater(logListView.positionViewAtEnd);
                                }

                                delegate: RowLayout {
                                    width: ListView.view.width
                                    spacing: 8

                                    Text {
                                        text: model.time
                                        font.pixelSize: 11
                                        font.family: "Consolas"
                                        color: theme.textTertiary
                                    }

                                    Text {
                                        Layout.fillWidth: true
                                        text: model.entry
                                        font.pixelSize: 11
                                        font.family: "Consolas"
                                        color: theme.logColor
                                        wrapMode: Text.WrapAnywhere
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // 3. STATUS BAR (Microsoft Phone Link Footer)
        // ═════════════════════════════════════════════════════════════════════
        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: 28
            color: theme.bgSidebar

            Rectangle {
                anchors.top: parent.top
                width: parent.width
                height: 1
                color: theme.strokeSubtle
            }

            RowLayout {
                anchors.fill: parent
                anchors.leftMargin: 16
                anchors.rightMargin: 16

                Rectangle {
                    width: 7
                    height: 7
                    radius: 3.5
                    color: relayManager.isConnected ? theme.success : theme.warning
                }

                Text {
                    text: relayManager.statusText + " (" + relayManager.host + ":" + relayManager.port + ")"
                    font.family: "Segoe UI"
                    font.pixelSize: 11
                    color: theme.textSecondary
                }

                Item { Layout.fillWidth: true }

                Text {
                    text: isSilentStartup ? "Auto-Started (Silent)" : "Manual Launch"
                    font.family: "Segoe UI"
                    font.pixelSize: 11
                    color: isSilentStartup ? theme.success : theme.textTertiary
                }

                Text { text: "·"; font.pixelSize: 11; color: theme.textTertiary }

                Text {
                    text: "Beatz Control v1.23.50"
                    font.family: "Segoe UI"
                    font.pixelSize: 11
                    color: theme.textTertiary
                }

                Text { text: "·"; font.pixelSize: 11; color: theme.textTertiary }

                Text {
                    text: "© 2026"
                    font.family: "Segoe UI"
                    font.pixelSize: 11
                    color: theme.textTertiary
                }
            }
        }
    }
}
