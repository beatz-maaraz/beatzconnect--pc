#include "AutoStartManager.h"
#include <QDir>

const QString AutoStartManager::RunRegistryKey = QStringLiteral("HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Run");
const QString AutoStartManager::AppName = QStringLiteral("BeatzControl");

AutoStartManager::AutoStartManager(QObject *parent)
    : QObject(parent)
{
}

bool AutoStartManager::isAutoStartEnabled() const
{
#ifdef Q_OS_WIN
    QSettings settings(RunRegistryKey, QSettings::NativeFormat);
    return settings.contains(AppName);
#else
    return false;
#endif
}

void AutoStartManager::setAutoStartEnabled(bool enabled)
{
#ifdef Q_OS_WIN
    QSettings settings(RunRegistryKey, QSettings::NativeFormat);
    if (enabled) {
        QString appPath = QDir::toNativeSeparators(QCoreApplication::applicationFilePath());
        QString value = QString("\"%1\" --autostart --minimized").arg(appPath);
        settings.setValue(AppName, value);
    } else {
        settings.remove(AppName);
    }
    emit autoStartChanged(enabled);
#else
    Q_UNUSED(enabled)
#endif
}
