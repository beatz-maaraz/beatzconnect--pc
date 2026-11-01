#include <QApplication>
#include <QQmlApplicationEngine>
#include <QQmlContext>
#include <QQuickWindow>
#include <QSystemTrayIcon>
#include <QMenu>
#include <QAction>
#include <QPalette>
#include <QColor>
#include <QIcon>
#include <QCommandLineParser>
#include <QDebug>

#include "RelayManager.h"
#include "HardwareController.h"
#include "AutoStartManager.h"

int main(int argc, char *argv[])
{
    // High-DPI scaling attributes for modern Windows displays
#if QT_VERSION < QT_VERSION_CHECK(6, 0, 0)
    QCoreApplication::setAttribute(Qt::AA_EnableHighDpiScaling);
    QCoreApplication::setAttribute(Qt::AA_UseHighDpiPixmaps);
#endif

    QApplication app(argc, argv);
    app.setApplicationName(QStringLiteral("Beatz Control"));
    app.setOrganizationName(QStringLiteral("Beatz Control"));
    app.setApplicationVersion(QStringLiteral("1.23.50"));
    app.setQuitOnLastWindowClosed(false);

    // ─── 1. Custom QPalette for Microsoft Phone Link Dark Aesthetic ──────────
    QPalette phoneLinkDarkPalette;
    const QColor bgBase(0x1C, 0x1C, 0x1C);       // Phone Link Dark base: #1C1C1C
    const QColor bgSidebar(0x20, 0x20, 0x20);    // Phone Link Nav rail:  #202020
    const QColor surfaceCard(0x2B, 0x2B, 0x2B);  // Card surface:         #2B2B2B
    const QColor surfaceNested(0x32, 0x32, 0x32);// Nested surface:       #323232
    const QColor surfaceHover(0x38, 0x38, 0x38); // Hover surface:        #383838
    const QColor accentBlue(0x00, 0x78, 0xD4);   // Phone Link Accent:    #0078D4
    const QColor accentCyan(0x60, 0xCD, 0xFF);   // Fluent Light Accent:  #60CDFF
    const QColor textWhite(0xFF, 0xFF, 0xFF);    // Primary text:         #FFFFFF
    const QColor textDim(0xA0, 0xA0, 0xA0);      // Secondary text:       #A0A0A0

    phoneLinkDarkPalette.setColor(QPalette::Window, bgBase);
    phoneLinkDarkPalette.setColor(QPalette::WindowText, textWhite);
    phoneLinkDarkPalette.setColor(QPalette::Base, surfaceCard);
    phoneLinkDarkPalette.setColor(QPalette::AlternateBase, bgSidebar);
    phoneLinkDarkPalette.setColor(QPalette::Button, surfaceCard);
    phoneLinkDarkPalette.setColor(QPalette::ButtonText, textWhite);
    phoneLinkDarkPalette.setColor(QPalette::Text, textWhite);
    phoneLinkDarkPalette.setColor(QPalette::BrightText, accentCyan);
    phoneLinkDarkPalette.setColor(QPalette::Highlight, accentBlue);
    phoneLinkDarkPalette.setColor(QPalette::HighlightedText, textWhite);
    phoneLinkDarkPalette.setColor(QPalette::ToolTipBase, surfaceHover);
    phoneLinkDarkPalette.setColor(QPalette::ToolTipText, textWhite);
    phoneLinkDarkPalette.setColor(QPalette::PlaceholderText, textDim);
    app.setPalette(phoneLinkDarkPalette);

    // ─── 2. Command Line Arguments: Silent Startup Detection ─────────────────
    QCommandLineParser parser;
    parser.setApplicationDescription(QStringLiteral("Beatz Control Desktop Remote Service"));
    parser.addHelpOption();
    parser.addVersionOption();

    QCommandLineOption autostartOption(QStringList() << QStringLiteral("autostart"),
                                       QStringLiteral("Run silently in background system tray mode on startup"));
    QCommandLineOption minimizedOption(QStringList() << QStringLiteral("minimized"),
                                       QStringLiteral("Start application minimized"));
    QCommandLineOption silentOption(QStringList() << QStringLiteral("silent"),
                                    QStringLiteral("Start in silent mode"));

    parser.addOption(autostartOption);
    parser.addOption(minimizedOption);
    parser.addOption(silentOption);
    parser.process(app);

    const bool isSilentStartup = parser.isSet(autostartOption) ||
                                parser.isSet(minimizedOption) ||
                                parser.isSet(silentOption);

    // ─── 3. Instantiate Core Services ────────────────────────────────────────
    RelayManager relayManager;
    HardwareController hardwareController;
    AutoStartManager autoStartManager;

    // ─── 4. QML Engine Setup ─────────────────────────────────────────────────
    QQmlApplicationEngine engine;
    engine.rootContext()->setContextProperty(QStringLiteral("relayManager"), &relayManager);
    engine.rootContext()->setContextProperty(QStringLiteral("hardwareController"), &hardwareController);
    engine.rootContext()->setContextProperty(QStringLiteral("autoStartManager"), &autoStartManager);
    engine.rootContext()->setContextProperty(QStringLiteral("isSilentStartup"), isSilentStartup);

    engine.load(QUrl(QStringLiteral("qrc:/qml/main.qml")));
    if (engine.rootObjects().isEmpty()) {
        return -1;
    }

    QQuickWindow *mainWindow = qobject_cast<QQuickWindow*>(engine.rootObjects().first());

    // ─── 5. System Tray Icon & Context Menu ──────────────────────────────────
    QSystemTrayIcon trayIcon;
    QIcon appIcon = QIcon(QStringLiteral(":/assets/icon.ico"));
    if (appIcon.isNull()) {
        appIcon = app.style()->standardIcon(QStyle::SP_ComputerIcon);
    }
    trayIcon.setIcon(appIcon);
    trayIcon.setToolTip(QStringLiteral("Beatz Control — Relay Active"));

    QMenu trayMenu;
    QAction *openAction = trayMenu.addAction(QStringLiteral("Open Beatz Control"));
    trayMenu.addSeparator();
    QAction *lockAction = trayMenu.addAction(QStringLiteral("Lock Workstation"));
    QAction *reconnectAction = trayMenu.addAction(QStringLiteral("Reconnect Relay"));
    trayMenu.addSeparator();
    QAction *quitAction = trayMenu.addAction(QStringLiteral("Exit"));

    QObject::connect(openAction, &QAction::triggered, [mainWindow]() {
        if (mainWindow) {
            mainWindow->show();
            mainWindow->raise();
            mainWindow->requestActivate();
        }
    });

    QObject::connect(lockAction, &QAction::triggered, &hardwareController, &HardwareController::lockWorkstation);
    QObject::connect(reconnectAction, &QAction::triggered, [&relayManager]() {
        relayManager.connectToServer();
    });

    QObject::connect(quitAction, &QAction::triggered, &app, &QCoreApplication::quit);

    QObject::connect(&trayIcon, &QSystemTrayIcon::activated, [mainWindow](QSystemTrayIcon::ActivationReason reason) {
        if (reason == QSystemTrayIcon::DoubleClick || reason == QSystemTrayIcon::Trigger) {
            if (mainWindow) {
                if (mainWindow->isVisible() && !mainWindow->isMinimized()) {
                    mainWindow->hide();
                } else {
                    mainWindow->show();
                    mainWindow->raise();
                    mainWindow->requestActivate();
                }
            }
        }
    });

    trayIcon.setContextMenu(&trayMenu);
    trayIcon.show();

    // ─── 6. Silent Startup vs Interactive Launch ─────────────────────────────
    if (isSilentStartup) {
        // Run silently in background on startup! Window stays hidden.
        if (mainWindow) {
            mainWindow->hide();
        }
        trayIcon.showMessage(
            QStringLiteral("Beatz Control"),
            QStringLiteral("Started silently in background — connected via QTcpSocket."),
            QSystemTrayIcon::Information,
            3000
        );
    } else {
        if (mainWindow) {
            mainWindow->show();
            mainWindow->raise();
            mainWindow->requestActivate();
        }
    }

    return app.exec();
}

