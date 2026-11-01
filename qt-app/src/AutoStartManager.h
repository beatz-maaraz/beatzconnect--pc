#ifndef AUTOSTARTMANAGER_H
#define AUTOSTARTMANAGER_H

#include <QObject>
#include <QSettings>
#include <QCoreApplication>

class AutoStartManager : public QObject
{
    Q_OBJECT

    Q_PROPERTY(bool isAutoStartEnabled READ isAutoStartEnabled NOTIFY autoStartChanged)

public:
    explicit AutoStartManager(QObject *parent = nullptr);

    bool isAutoStartEnabled() const;

public slots:
    void setAutoStartEnabled(bool enabled);

signals:
    void autoStartChanged(bool enabled);

private:
    static const QString RunRegistryKey;
    static const QString AppName;
};

#endif // AUTOSTARTMANAGER_H

