import QtQuick 2.15

QtObject {
    id: theme

    // Mode can be: "dark", "light", "system"
    property string mode: "dark"

    readonly property bool isDark: mode === "dark" || (mode === "system" && true)

    // ── Application & Container Backgrounds (Phone Link WinUI 3) ──
    readonly property color bgBase: isDark ? "#1C1C1C" : "#F3F3F3"
    readonly property color bgSidebar: isDark ? "#202020" : "#EBEBEB"
    readonly property color surfaceDefault: isDark ? "#2B2B2B" : "#FFFFFF"
    readonly property color surfaceSecondary: isDark ? "#323232" : "#F8F8F8"
    readonly property color surfaceTertiary: isDark ? "#383838" : "#EAEAEA"
    readonly property color surfaceFlyout: isDark ? "#2C2C2C" : "#FFFFFF"

    // ── Accent Colors (Microsoft Phone Link Signature Blue & Cyan) ──
    readonly property color accentPrimary: isDark ? "#0078D4" : "#005FB8"
    readonly property color accentHover: isDark ? "#1084D9" : "#0066CC"
    readonly property color accentPressed: isDark ? "#006CC1" : "#004D99"
    readonly property color accentLight: isDark ? "#60CDFF" : "#0078D4"
    readonly property color accentSubtle: isDark ? "#1F60CDFF" : "#14005FB8"
    readonly property color accentSubtleHover: isDark ? "#3360CDFF" : "#24005FB8"

    // ── Typography Colors ──
    readonly property color textPrimary: isDark ? "#FFFFFF" : "#1F1F1F"
    readonly property color textSecondary: isDark ? "#D1D1D1" : "#5C5C5C"
    readonly property color textTertiary: isDark ? "#A0A0A0" : "#737373"
    readonly property color textDisabled: isDark ? "#6E6E6E" : "#A1A1AA"
    readonly property color textOnAccent: "#FFFFFF"

    // ── Borders & Outlines ──
    readonly property color strokeSubtle: isDark ? "#12FFFFFF" : "#0F000000"
    readonly property color strokeDefault: isDark ? "#1CFFFFFF" : "#1A000000"
    readonly property color strokeStrong: isDark ? "#2EFFFFFF" : "#30000000"
    readonly property color strokeFocus: accentLight

    // ── Feedback & Semantic States ──
    readonly property color success: isDark ? "#10B981" : "#0F766E"
    readonly property color successBg: isDark ? "#2010B981" : "#1510B981"
    readonly property color warning: isDark ? "#F59E0B" : "#B45309"
    readonly property color warningBg: isDark ? "#20F59E0B" : "#15F59E0B"
    readonly property color danger: isDark ? "#F87171" : "#DC2626"
    readonly property color dangerBg: isDark ? "#20EF4444" : "#15EF4444"

    // ── Component & Chrome Colors ──
    readonly property color logBg: isDark ? "#121212" : "#1E1E1E"
    readonly property color logColor: "#38BDF8"
    readonly property color chromeHover: isDark ? "#15FFFFFF" : "#15000000"
    readonly property color chromeCloseHover: "#C42B1C"

    // ── Radius Standards ──
    readonly property int radiusSmall: 4
    readonly property int radiusMedium: 8
    readonly property int radiusLarge: 12
    readonly property int radiusPill: 9999
}
