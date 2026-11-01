#ifndef HARDWARECONTROLLER_H
#define HARDWARECONTROLLER_H

#include <QObject>
#include <QStringList>
#include <QVariantList>

class HardwareController : public QObject
{
    Q_OBJECT

public:
    explicit HardwareController(QObject *parent = nullptr);

public slots:
    bool lockWorkstation();
    bool addFirewallRule();
    bool toggleHotspot();
    void setVolume(int percent);
    int getVolume();
    void moveMouse(int dx, int dy);
    void mouseClick(const QString &button = QStringLiteral("left"));
    QVariantList getActiveWindows();
    bool killProcess(int pid);

signals:
    void volumeChanged(int percent);
    void statusNotice(const QString &message);
};

#endif // HARDWARECONTROLLER_H

