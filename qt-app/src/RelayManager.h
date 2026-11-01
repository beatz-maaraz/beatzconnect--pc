#ifndef RELAYMANAGER_H
#define RELAYMANAGER_H

#include <QObject>
#include <QTcpSocket>
#include <QTimer>
#include <QElapsedTimer>
#include <QJsonObject>
#include <QJsonDocument>
#include <QHostInfo>

class RelayManager : public QObject
{
    Q_OBJECT

    Q_PROPERTY(bool isConnected READ isConnected NOTIFY connectionChanged)
    Q_PROPERTY(QString statusText READ statusText NOTIFY statusChanged)
    Q_PROPERTY(QString statusDetail READ statusDetail NOTIFY statusChanged)
    Q_PROPERTY(QString roomId READ roomId NOTIFY roomChanged)
    Q_PROPERTY(QString pcName READ pcName CONSTANT)
    Q_PROPERTY(QString host READ host WRITE setHost NOTIFY hostChanged)
    Q_PROPERTY(int port READ port WRITE setPort NOTIFY portChanged)
    Q_PROPERTY(int packetCount READ packetCount NOTIFY packetReceived)
    Q_PROPERTY(int latency READ latency NOTIFY latencyChanged)

public:
    explicit RelayManager(QObject *parent = nullptr);
    ~RelayManager() override;

    bool isConnected() const { return m_isConnected; }
    QString statusText() const { return m_statusText; }
    QString statusDetail() const { return m_statusDetail; }
    QString roomId() const { return m_roomId; }
    QString pcName() const { return m_pcName; }
    QString host() const { return m_host; }
    int port() const { return m_port; }
    int packetCount() const { return m_packetCount; }
    int latency() const { return m_latency; }

    void setHost(const QString &host);
    void setPort(int port);

public slots:
    void connectToServer(const QString &host = QString(), int port = 0);
    void disconnectFromServer();
    void sendCommand(const QString &cmd);

signals:
    void connectionChanged(bool connected);
    void statusChanged(const QString &text, const QString &detail);
    void roomChanged(const QString &roomId);
    void hostChanged(const QString &host);
    void portChanged(int port);
    void packetReceived(int count);
    void latencyChanged(int ms);
    void logMessage(const QString &message);
    void commandExecuted(const QString &type, const QString &details);

private slots:
    void onConnected();
    void onDisconnected();
    void onReadyRead();
    void onErrorOccurred(QAbstractSocket::SocketError socketError);
    void attemptReconnect();
    void sendPing();

private:
    void setStatus(const QString &text, const QString &detail);
    void handleIncomingPacket(const QByteArray &data);
    void processJsonCommand(const QJsonObject &obj);

    QTcpSocket *m_socket;
    QTimer *m_reconnectTimer;
    QTimer *m_pingTimer;
    QElapsedTimer m_pingElapsed;

    bool m_isConnected;
    QString m_statusText;
    QString m_statusDetail;
    QString m_roomId;
    QString m_pcName;
    QString m_pcId;
    QString m_host;
    int m_port;
    int m_packetCount;
    int m_latency;
    int m_retryIntervalMs;
};

#endif // RELAYMANAGER_H

