import QtQuick 2.15
import QtQuick.Controls 2.15
import QtQuick.Layouts 1.15
import QtQuick.Window 2.15

Window {
    id: rootWindow
    width: 1080
    height: 720
    minimumWidth: 860
    minimumHeight: 560
    visible: false
    title: "Beatz Control"
    flags: Qt.Window | Qt.FramelessWindowHint
    color: "#1C1C1C"

    // Property state
    property int currentTab: 0 // 0: Dashboard, 1: Apps, 2: Settings, 3: Logs
    property bool minimizeToTray: true

    // ─── Theme Colors (Microsoft Phone Link Dark Palette) ────────────────────
    readonly property color colBgBase: "#1C1C1C"
    readonly property color colBgSidebar: "#202020"
    readonly property color colSurface: "#2B2B2B"
    readonly property color colSurfaceSec: "#323232"
    readonly property color colSurfaceTert: "#383838"
    readonly property color colAccent: "#0078D4"
    readonly property color colAccentHover: "#1084D9"
    readonly property color colAccentLight: "#60CDFF"
    readonly property color colTextPrimary: "#FFFFFF"
    readonly property color colTextSecondary: "#D1D1D1"
    readonly property color colTextTertiary: "#A0A0A0"
    readonly property color colStrokeSubtle: "#12FFFFFF"
    readonly property color colStrokeDefault: "#1CFFFFFF"
    readonly property color colSuccess: "#10B981"
    readonly property color colWarning: "#F59E0B"
    readonly property color colDanger: "#F87171"

    // Model for activity logs
    ListModel {
        id: logModel
    }

    Connections {
        target: relayManager
        function onLogMessage(message) {
            logModel.append({ "entry": message });
            if (logModel.count > 400) {
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

        // ── 1. CUSTOM TITLE BAR (Phone Link Style) ───────────────────────────
        Rectangle {
            id: titleBar
            Layout.fillWidth: true
            Layout.preferredHeight: 40
            color: rootWindow.colBgSidebar

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
                anchors.leftMargin: 16
                anchors.rightMargin: 0
                spacing: 10

                // App Brand Glyph
                Rectangle {
                    width: 20
                    height: 20
                    radius: 5
                    color: rootWindow.colAccent

                    Text {
                        anchors.centerIn: parent
                        text: "♪"
                        color: "#FFFFFF"
                        font.pixelSize: 13
                        font.bold: true
                    }
                }

                Text {
                    text: "Beatz Control"
                    font.pixelSize: 12
                    font.weight: Font.DemiBold
                    color: rootWindow.colTextSecondary
                }

                // Version Badge
                Rectangle {
                    height: 18
                    width: txtVer.width + 12
                    radius: 4
                    color: "#182438"
                    border.color: "#300078D4"

                    Text {
                        id: txtVer
                        anchors.centerIn: parent
                        text: "v1.23.50"
                        font.pixelSize: 10
                        font.bold: true
                        color: rootWindow.colAccentLight
                    }
                }

                // Autostart Badge
                Rectangle {
                    visible: isSilentStartup
                    height: 18
                    width: txtAuto.width + 12
                    radius: 4
                    color: "#0D2010"
                    border.color: rootWindow.colSuccess

                    Text {
                        id: txtAuto
                        anchors.centerIn: parent
                        text: "AUTO"
                        font.pixelSize: 9
                        font.bold: true
                        color: rootWindow.colSuccess
                    }
                }

                Item { Layout.fillWidth: true }

                // Window Chrome Control Buttons
                Row {
                    spacing: 0
                    Layout.fillHeight: true

                    // Minimize
                    Rectangle {
                        width: 46
                        height: 40
                        color: btnMinMouse.containsMouse ? "#20FFFFFF" : "transparent"

                        Text {
                            anchors.centerIn: parent
                            text: "—"
                            font.pixelSize: 10
                            color: rootWindow.colTextSecondary
                        }

                        MouseArea {
                            id: btnMinMouse
                            anchors.fill: parent
                            hoverEnabled: true
                            onClicked: rootWindow.showMinimized()
                        }
                    }

                    // Maximize / Restore
                    Rectangle {
                        width: 46
                        height: 40
                        color: btnMaxMouse.containsMouse ? "#20FFFFFF" : "transparent"

                        Text {
                            anchors.centerIn: parent
                            text: rootWindow.visibility === Window.Maximized ? "❐" : "☐"
                            font.pixelSize: 12
                            color: rootWindow.colTextSecondary
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

                    // Close to Tray
                    Rectangle {
                        width: 46
                        height: 40
                        color: btnCloseMouse.containsMouse ? "#C42B1C" : "transparent"

                        Text {
                            anchors.centerIn: parent
                            text: "✕"
                            font.pixelSize: 11
                            color: btnCloseMouse.containsMouse ? "#FFFFFF" : rootWindow.colTextSecondary
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

            Rectangle {
                anchors.bottom: parent.bottom
                width: parent.width
                height: 1
                color: rootWindow.colStrokeSubtle
            }
        }

        // ── 2. APP SHELL (Sidebar Navigation + Content Views) ────────────────
        RowLayout {
            Layout.fillWidth: true
            Layout.fillHeight: true
            spacing: 0

            // Left Navigation Rail
            Rectangle {
                Layout.preferredWidth: 210
                Layout.fillHeight: true
                color: rootWindow.colBgSidebar

                Rectangle {
                    anchors.right: parent.right
                    width: 1
                    height: parent.height
                    color: rootWindow.colStrokeSubtle
                }

                ColumnLayout {
                    anchors.fill: parent
                    anchors.margins: 10
                    spacing: 4

                    // Nav item 0: Dashboard
                    Rectangle {
                        Layout.fillWidth: true
                        Layout.preferredHeight: 38
                        radius: 4
                        color: rootWindow.currentTab === 0 ? "#1CFFFFFF" : (m0.containsMouse ? "#0DFFFFFF" : "transparent")

                        Rectangle {
                            visible: rootWindow.currentTab === 0
                            width: 3
                            height: 18
                            radius: 2
                            color: rootWindow.colAccentLight
                            anchors.left: parent.left
                            anchors.verticalCenter: parent.verticalCenter
                        }

                        RowLayout {
                            anchors.fill: parent
                            anchors.leftMargin: 14
                            spacing: 12
                            Text { text: "⊞"; font.pixelSize: 14; color: rootWindow.colTextPrimary }
                            Text { text: "Dashboard"; font.pixelSize: 13; font.weight: rootWindow.currentTab === 0 ? Font.DemiBold : Font.Normal; color: rootWindow.colTextPrimary }
                        }

                        MouseArea { id: m0; anchors.fill: parent; hoverEnabled: true; onClicked: rootWindow.currentTab = 0 }
                    }

                    // Nav item 1: Active Apps
                    Rectangle {
                        Layout.fillWidth: true
                        Layout.preferredHeight: 38
                        radius: 4
                        color: rootWindow.currentTab === 1 ? "#1CFFFFFF" : (m1.containsMouse ? "#0DFFFFFF" : "transparent")

                        Rectangle {
                            visible: rootWindow.currentTab === 1
                            width: 3
                            height: 18
                            radius: 2
                            color: rootWindow.colAccentLight
                            anchors.left: parent.left
                            anchors.verticalCenter: parent.verticalCenter
                        }

                        RowLayout {
                            anchors.fill: parent
                            anchors.leftMargin: 14
                            spacing: 12
                            Text { text: "☰"; font.pixelSize: 14; color: rootWindow.colTextPrimary }
                            Text { text: "Active Apps"; font.pixelSize: 13; font.weight: rootWindow.currentTab === 1 ? Font.DemiBold : Font.Normal; color: rootWindow.colTextPrimary }
                        }

                        MouseArea { id: m1; anchors.fill: parent; hoverEnabled: true; onClicked: rootWindow.currentTab = 1 }
                    }

                    // Nav item 2: Settings
                    Rectangle {
                        Layout.fillWidth: true
                        Layout.preferredHeight: 38
                        radius: 4
                        color: rootWindow.currentTab === 2 ? "#1CFFFFFF" : (m2.containsMouse ? "#0DFFFFFF" : "transparent")

                        Rectangle {
                            visible: rootWindow.currentTab === 2
                            width: 3
                            height: 18
                            radius: 2
                            color: rootWindow.colAccentLight
                            anchors.left: parent.left
                            anchors.verticalCenter: parent.verticalCenter
                        }

                        RowLayout {
                            anchors.fill: parent
                            anchors.leftMargin: 14
                            spacing: 12
                            Text { text: "⚙"; font.pixelSize: 14; color: rootWindow.colTextPrimary }
                            Text { text: "Settings"; font.pixelSize: 13; font.weight: rootWindow.currentTab === 2 ? Font.DemiBold : Font.Normal; color: rootWindow.colTextPrimary }
                        }

                        MouseArea { id: m2; anchors.fill: parent; hoverEnabled: true; onClicked: rootWindow.currentTab = 2 }
                    }

                    // Nav item 3: Activity Log
                    Rectangle {
                        Layout.fillWidth: true
                        Layout.preferredHeight: 38
                        radius: 4
                        color: rootWindow.currentTab === 3 ? "#1CFFFFFF" : (m3.containsMouse ? "#0DFFFFFF" : "transparent")

                        Rectangle {
                            visible: rootWindow.currentTab === 3
                            width: 3
                            height: 18
                            radius: 2
                            color: rootWindow.colAccentLight
                            anchors.left: parent.left
                            anchors.verticalCenter: parent.verticalCenter
                        }

                        RowLayout {
                            anchors.fill: parent
                            anchors.leftMargin: 14
                            spacing: 12
                            Text { text: "≡"; font.pixelSize: 14; color: rootWindow.colTextPrimary }
                            Text { text: "Activity Log"; font.pixelSize: 13; font.weight: rootWindow.currentTab === 3 ? Font.DemiBold : Font.Normal; color: rootWindow.colTextPrimary }
                        }

                        MouseArea { id: m3; anchors.fill: parent; hoverEnabled: true; onClicked: rootWindow.currentTab = 3 }
                    }

                    Item { Layout.fillHeight: true }

                    // Sidebar Status Bottom Card
                    Rectangle {
                        Layout.fillWidth: true
                        Layout.preferredHeight: 36
                        radius: 6
                        color: rootWindow.colSurfaceSec
                        border.color: rootWindow.colStrokeSubtle

                        RowLayout {
                            anchors.fill: parent
                            anchors.margins: 8
                            spacing: 8

                            Rectangle {
                                width: 8
                                height: 8
                                radius: 4
                                color: relayManager.isConnected ? rootWindow.colSuccess : rootWindow.colWarning
                            }

                            Text {
                                Layout.fillWidth: true
                                elide: Text.ElideRight
                                text: relayManager.isConnected ? "QTcpSocket Active" : "Connecting…"
                                font.pixelSize: 11
                                color: rootWindow.colTextTertiary
                            }
                        }
                    }
                }
            }

            // Right Main Content Views
            StackLayout {
                id: mainStack
                Layout.fillWidth: true
                Layout.fillHeight: true
                currentIndex: rootWindow.currentTab

                // ── TAB 0: DASHBOARD ─────────────────────────────────────────
                ScrollView {
                    contentWidth: -1
                    clip: true

                    ColumnLayout {
                        anchors.fill: parent
                        anchors.margins: 24
                        spacing: 16

                        // Silent Autostart Alert Banner
                        Rectangle {
                            visible: isSilentStartup
                            Layout.fillWidth: true
                            Layout.preferredHeight: 36
                            radius: 6
                            color: "#1A10B981"
                            border.color: "#3010B981"

                            RowLayout {
                                anchors.fill: parent
                                anchors.leftMargin: 12
                                spacing: 8

                                Rectangle { width: 6; height: 6; radius: 3; color: rootWindow.colSuccess }
                                Text {
                                    text: "Running silently in background mode — automatically connected via QTcpSocket."
                                    color: "#6EE7B7"
                                    font.pixelSize: 12
                                }
                            }
                        }

                        // Grid: Cloud Relay Card & Telemetry Card
                        GridLayout {
                            Layout.fillWidth: true
                            columns: 2
                            columnSpacing: 16
                            rowSpacing: 16

                            // Cloud Relay Card
                            Rectangle {
                                Layout.fillWidth: true
                                Layout.preferredHeight: 280
                                radius: 8
                                color: rootWindow.colSurface
                                border.color: rootWindow.colStrokeDefault

                                ColumnLayout {
                                    anchors.fill: parent
                                    anchors.margins: 16
                                    spacing: 12

                                    Text {
                                        text: "CLOUD RELAY (QTcpSocket)"
                                        font.pixelSize: 11
                                        font.bold: true
                                        color: rootWindow.colTextTertiary
                                    }

                                    Rectangle {
                                        Layout.fillWidth: true
                                        Layout.preferredHeight: 60
                                        radius: 6
                                        color: rootWindow.colSurfaceSec

                                        RowLayout {
                                            anchors.fill: parent
                                            anchors.margins: 12
                                            spacing: 12

                                            Rectangle {
                                                width: 12
                                                height: 12
                                                radius: 6
                                                color: relayManager.isConnected ? rootWindow.colSuccess : rootWindow.colWarning
                                            }

                                            ColumnLayout {
                                                Layout.fillWidth: true
                                                spacing: 2
                                                Text {
                                                    text: relayManager.statusText
                                                    font.pixelSize: 13
                                                    font.weight: Font.DemiBold
                                                    color: rootWindow.colTextPrimary
                                                }
                                                Text {
                                                    text: relayManager.statusDetail
                                                    font.pixelSize: 11
                                                    color: rootWindow.colTextTertiary
                                                }
                                            }

                                            // Room ID Badge
                                            Rectangle {
                                                Layout.preferredHeight: 24
                                                Layout.preferredWidth: txtRoom.width + 16
                                                radius: 12
                                                color: rootWindow.colSurfaceTert

                                                Text {
                                                    id: txtRoom
                                                    anchors.centerIn: parent
                                                    text: relayManager.roomId
                                                    font.pixelSize: 11
                                                    font.family: "Consolas"
                                                    color: rootWindow.colAccentLight
                                                }
                                            }
                                        }
                                    }

                                    // Info Grid (PC Name, Resolution, Port)
                                    GridLayout {
                                        Layout.fillWidth: true
                                        columns: 2
                                        rowSpacing: 8
                                        columnSpacing: 12

                                        ColumnLayout {
                                            Text { text: "PC Name"; font.pixelSize: 10; color: rootWindow.colTextTertiary }
                                            Text { text: relayManager.pcName; font.pixelSize: 12; font.bold: true; color: rootWindow.colTextPrimary }
                                        }

                                        ColumnLayout {
                                            Text { text: "Socket Server"; font.pixelSize: 10; color: rootWindow.colTextTertiary }
                                            Text { text: relayManager.host + ":" + relayManager.port; font.pixelSize: 11; font.family: "Consolas"; color: rootWindow.colTextPrimary }
                                        }
                                    }

                                    Item { Layout.fillHeight: true }

                                    // Volume Slider
                                    ColumnLayout {
                                        Layout.fillWidth: true
                                        spacing: 4

                                        RowLayout {
                                            Layout.fillWidth: true
                                            Text { text: "Master Volume"; font.pixelSize: 11; color: rootWindow.colTextTertiary }
                                            Item { Layout.fillWidth: true }
                                            Text { text: Math.round(volSlider.value) + "%"; font.pixelSize: 11; font.bold: true; color: rootWindow.colTextPrimary }
                                        }

                                        Slider {
                                            id: volSlider
                                            Layout.fillWidth: true
                                            from: 0
                                            to: 100
                                            value: 50
                                            onMoved: hardwareController.setVolume(Math.round(value))
                                        }
                                    }
                                }
                            }

                            // Telemetry & Quick Actions Card
                            Rectangle {
                                Layout.fillWidth: true
                                Layout.preferredHeight: 280
                                radius: 8
                                color: rootWindow.colSurface
                                border.color: rootWindow.colStrokeDefault

                                ColumnLayout {
                                    anchors.fill: parent
                                    anchors.margins: 16
                                    spacing: 12

                                    Text {
                                        text: "TELEMETRY & ACTIONS"
                                        font.pixelSize: 11
                                        font.bold: true
                                        color: rootWindow.colTextTertiary
                                    }

                                    RowLayout {
                                        Layout.fillWidth: true
                                        Rectangle {
                                            Layout.fillWidth: true
                                            Layout.preferredHeight: 52
                                            radius: 6
                                            color: rootWindow.colSurfaceSec
                                            ColumnLayout {
                                                anchors.centerIn: parent
                                                Text { text: "Packets Received"; font.pixelSize: 10; color: rootWindow.colTextTertiary }
                                                Text { text: String(relayManager.packetCount); font.pixelSize: 16; font.bold: true; color: rootWindow.colTextPrimary }
                                            }
                                        }
                                        Rectangle {
                                            Layout.fillWidth: true
                                            Layout.preferredHeight: 52
                                            radius: 6
                                            color: rootWindow.colSurfaceSec
                                            ColumnLayout {
                                                anchors.centerIn: parent
                                                Text { text: "Roundtrip Latency"; font.pixelSize: 10; color: rootWindow.colTextTertiary }
                                                Text { text: relayManager.latency > 0 ? relayManager.latency + " ms" : "—"; font.pixelSize: 16; font.bold: true; color: rootWindow.colAccentLight }
                                            }
                                        }
                                    }

                                    Text {
                                        text: "QUICK ACTIONS"
                                        font.pixelSize: 11
                                        font.bold: true
                                        color: rootWindow.colTextTertiary
                                        Layout.topMargin: 4
                                    }

                                    GridLayout {
                                        Layout.fillWidth: true
                                        columns: 2
                                        columnSpacing: 8
                                        rowSpacing: 8

                                        Button {
                                            Layout.fillWidth: true
                                            text: "🔒 Lock Screen"
                                            onClicked: hardwareController.lockWorkstation()
                                        }
                                        Button {
                                            Layout.fillWidth: true
                                            text: "📡 Toggle Hotspot"
                                            onClicked: hardwareController.toggleHotspot()
                                        }
                                        Button {
                                            Layout.fillWidth: true
                                            text: "🛡 Add Firewall Rule"
                                            onClicked: hardwareController.addFirewallRule()
                                        }
                                        Button {
                                            Layout.fillWidth: true
                                            text: "🔄 Reconnect Relay"
                                            onClicked: relayManager.connectToServer()
                                        }
                                    }
                                }
                            }
                        }

                        Item { Layout.fillHeight: true }
                    }
                }

                // ── TAB 1: ACTIVE APPS ───────────────────────────────────────
                Rectangle {
                    color: "transparent"

                    ColumnLayout {
                        anchors.fill: parent
                        anchors.margins: 24
                        spacing: 12

                        RowLayout {
                            Layout.fillWidth: true
                            Text { text: "ACTIVE WINDOW PROCESSES"; font.pixelSize: 11; font.bold: true; color: rootWindow.colTextTertiary }
                            Item { Layout.fillWidth: true }
                            Button {
                                text: "Refresh List"
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

                        ListView {
                            Layout.fillWidth: true
                            Layout.fillHeight: true
                            clip: true
                            model: appListModel
                            spacing: 6

                            delegate: Rectangle {
                                width: ListView.view.width
                                height: 44
                                radius: 6
                                color: rootWindow.colSurface
                                border.color: rootWindow.colStrokeDefault

                                RowLayout {
                                    anchors.fill: parent
                                    anchors.margins: 10
                                    spacing: 12

                                    Rectangle {
                                        width: 8
                                        height: 8
                                        radius: 4
                                        color: rootWindow.colAccentLight
                                    }

                                    Text {
                                        Layout.fillWidth: true
                                        elide: Text.ElideRight
                                        text: model.title
                                        font.pixelSize: 12
                                        color: rootWindow.colTextPrimary
                                    }

                                    Text {
                                        text: "PID: " + model.pid
                                        font.pixelSize: 10
                                        font.family: "Consolas"
                                        color: rootWindow.colTextTertiary
                                    }
                                }
                            }
                        }
                    }
                }

                // ── TAB 2: SETTINGS ──────────────────────────────────────────
                ScrollView {
                    contentWidth: -1
                    clip: true

                    ColumnLayout {
                        anchors.fill: parent
                        anchors.margins: 24
                        spacing: 16

                        Text { text: "SERVER CONFIGURATION"; font.pixelSize: 11; font.bold: true; color: rootWindow.colTextTertiary }

                        Rectangle {
                            Layout.fillWidth: true
                            Layout.preferredHeight: 110
                            radius: 8
                            color: rootWindow.colSurface
                            border.color: rootWindow.colStrokeDefault

                            ColumnLayout {
                                anchors.fill: parent
                                anchors.margins: 14
                                spacing: 8

                                Text { text: "QTcpSocket Server Address"; font.pixelSize: 12; color: rootWindow.colTextSecondary }

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
                            }
                        }

                        Text { text: "STARTUP & BACKGROUND"; font.pixelSize: 11; font.bold: true; color: rootWindow.colTextTertiary; Layout.topMargin: 8 }

                        Rectangle {
                            Layout.fillWidth: true
                            Layout.preferredHeight: 120
                            radius: 8
                            color: rootWindow.colSurface
                            border.color: rootWindow.colStrokeDefault

                            ColumnLayout {
                                anchors.fill: parent
                                anchors.margins: 14
                                spacing: 10

                                CheckBox {
                                    text: "Launch automatically on Windows logon (silent background mode)"
                                    checked: autoStartManager.isAutoStartEnabled
                                    onToggled: autoStartManager.setAutoStartEnabled(checked)
                                }

                                CheckBox {
                                    text: "Minimize to tray instead of closing (keep QTcpSocket alive)"
                                    checked: rootWindow.minimizeToTray
                                    onToggled: rootWindow.minimizeToTray = checked
                                }
                            }
                        }

                        Item { Layout.fillHeight: true }
                    }
                }

                // ── TAB 3: ACTIVITY LOG ──────────────────────────────────────
                Rectangle {
                    color: "transparent"

                    ColumnLayout {
                        anchors.fill: parent
                        anchors.margins: 24
                        spacing: 10

                        RowLayout {
                            Layout.fillWidth: true
                            Text { text: "ACTIVITY LOG"; font.pixelSize: 11; font.bold: true; color: rootWindow.colTextTertiary }
                            Item { Layout.fillWidth: true }
                            Button {
                                text: "Clear Logs"
                                onClicked: logModel.clear()
                            }
                        }

                        Rectangle {
                            Layout.fillWidth: true
                            Layout.fillHeight: true
                            radius: 6
                            color: "#121212"
                            border.color: rootWindow.colStrokeDefault

                            ListView {
                                anchors.fill: parent
                                anchors.margins: 10
                                clip: true
                                model: logModel
                                delegate: Text {
                                    text: model.entry
                                    font.pixelSize: 11
                                    font.family: "Consolas"
                                    color: "#38BDF8"
                                    wrapMode: Text.WrapAnywhere
                                    width: ListView.view.width
                                }
                            }
                        }
                    }
                }
            }
        }

        // ── 3. STATUS BAR ────────────────────────────────────────────────────
        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: 28
            color: rootWindow.colBgSidebar

            Rectangle {
                anchors.top: parent.top
                width: parent.width
                height: 1
                color: rootWindow.colStrokeSubtle
            }

            RowLayout {
                anchors.fill: parent
                anchors.leftMargin: 16
                anchors.rightMargin: 16

                Rectangle {
                    width: 7
                    height: 7
                    radius: 3.5
                    color: relayManager.isConnected ? rootWindow.colSuccess : rootWindow.colWarning
                }

                Text {
                    text: relayManager.statusText
                    font.pixelSize: 10
                    color: rootWindow.colTextSecondary
                }

                Item { Layout.fillWidth: true }

                Text {
                    text: isSilentStartup ? "Auto-started (silent)" : "Manual Launch"
                    font.pixelSize: 10
                    color: isSilentStartup ? rootWindow.colSuccess : rootWindow.colTextTertiary
                }

                Text { text: " · "; font.pixelSize: 10; color: rootWindow.colTextTertiary }

                Text {
                    text: "Beatz Control v1.23.50 · © 2026"
                    font.pixelSize: 10
                    color: rootWindow.colTextTertiary
                }
            }
        }
    }
}
