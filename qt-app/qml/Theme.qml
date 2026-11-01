pragma Singleton
import QtQuick 2.15

QtObject {
    // Microsoft Phone Link / WinUI 3 Color Tokens
    readonly property color bgBase: "#1C1C1C"
    readonly property color bgSidebar: "#202020"
    readonly property color surfaceDefault: "#2B2B2B"
    readonly property color surfaceSecondary: "#323232"
    readonly property color surfaceTertiary: "#383838"

    readonly property color accentPrimary: "#0078D4"
    readonly property color accentHover: "#1084D9"
    readonly property color accentLight: "#60CDFF"
    readonly property color accentSubtle: "#1F0078D4"

    readonly property color textPrimary: "#FFFFFF"
    readonly property color textSecondary: "#D1D1D1"
    readonly property color textTertiary: "#A0A0A0"

    readonly property color strokeSubtle: "#12FFFFFF"
    readonly property color strokeDefault: "#1CFFFFFF"
    readonly property color strokeStrong: "#2AFFFFFF"

    readonly property color success: "#10B981"
    readonly property color successBg: "#1F10B981"
    readonly property color warning: "#F59E0B"
    readonly property color danger: "#F87171"
    readonly property color dangerBg: "#1FEF4444"

    readonly property int radiusSmall: 4
    readonly property int radiusMedium: 8
    readonly property int radiusLarge: 12
    readonly property int radiusPill: 9999
}

