#include "RelayManager.h"
#include <QDateTime>
#include <QDebug>
#include <QJsonArray>
#include <QRandomGenerator>

RelayManager::RelayManager(QObject *parent)
    : QObject(parent)
    , m_socket(new QTcpSocket(this))
    , m_reconnectTimer(new QTimer(this))
    , m_pingTimer(new QTimer(this))
    , m_isConnected(false)
    , m_statusText(QStringLiteral("Initialising…"))
    , m_statusDetail(QStringLiteral("Ready to connect"))
    , m_roomId(QStringLiteral("—"))
    , m_host(QStringLiteral("127.0.0.1"))
    , m_port(8080)
    , m_packetCount(0)
    , m_latency(0)
    , m_retryIntervalMs(3000)
{
    m_pcName = QHostInfo::localHostName();
    m_pcId = m_pcName.toLower().replace(QLatin1Char(' '), QLatin1Char('_'));

    connect(m_socket, &QTcpSocket::connected, this, &RelayManager::onConnected);
    connect(m_socket, &QTcpSocket::disconnected, this, &RelayManager::onDisconnected);
    connect(m_socket, &QTcpSocket::readyRead, this, &RelayManager::onReadyRead);
#if QT_VERSION >= QT_VERSION_CHECK(5, 15, 0)
    connect(m_socket, &QTcpSocket::errorOccurred, this, &RelayManager::onErrorOccurred);
#else
    connect(m_socket, QOverload<QAbstractSocket::SocketError>::of(&QAbstractSocket::error),
            this, &RelayManager::onErrorOccurred);
#endif

    m_reconnectTimer->setSingleShot(true);
    connect(m_reconnectTimer, &QTimer::timeout, this, &RelayManager::attemptReconnect);

    m_pingTimer->setInterval(4000);
    connect(m_pingTimer, &QTimer::timeout, this, &RelayManager::sendPing);

    // Automatic server connection upon application launch!
    QTimer::singleShot(200, this, [this]() {
        connectToServer();
    });
}

RelayManager::~RelayManager()
{
    disconnectFromServer();
}

void RelayManager::setHost(const QString &host)
{
    if (m_host != host) {
        m_host = host;
        emit hostChanged(m_host);
    }
}

void RelayManager::setPort(int port)
{
    if (m_port != port) {
        m_port = port;
        emit portChanged(m_port);
    }
}

void RelayManager::connectToServer(const QString &host, int port)
{
    if (!host.isEmpty()) m_host = host;
    if (port > 0) m_port = port;

    m_reconnectTimer->stop();

    if (m_socket->state() == QAbstractSocket::ConnectedState) {
        m_socket->disconnectFromHost();
    }

    setStatus(QStringLiteral("Connecting…"), QString("Connecting to %1:%2 via QTcpSocket").arg(m_host).arg(m_port));
    emit logMessage(QString("[%1] Attempting QTcpSocket connection to %2:%3")
                        .arg(QDateTime::currentDateTime().toString("HH:mm:ss.zzz"))
                        .arg(m_host)
                        .arg(m_port));

    m_socket->connectToHost(m_host, static_cast<quint16>(m_port));
}

void RelayManager::disconnectFromServer()
{
    m_reconnectTimer->stop();
    m_pingTimer->stop();

    if (m_socket->state() != QAbstractSocket::UnconnectedState) {
        m_socket->disconnectFromHost();
    }
    setStatus(QStringLiteral("Disconnected"), QStringLiteral("Disconnected by user"));
}

void RelayManager::sendCommand(const QString &cmd)
{
    if (!m_isConnected || m_socket->state() != QAbstractSocket::ConnectedState) {
        emit logMessage(QStringLiteral("[Warning] Cannot send command: QTcpSocket not connected"));
        return;
    }

    QByteArray bytes = cmd.toUtf8();
    if (!bytes.endsWith('\n')) {
        bytes.append('\n');
    }
    m_socket->write(bytes);
    m_socket->flush();
}

void RelayManager::onConnected()
{
    m_isConnected = true;
    m_retryIntervalMs = 3000;
    emit connectionChanged(true);

    setStatus(QStringLiteral("Connected to Relay"), QString("Established on port %1").arg(m_port));
    emit logMessage(QString("[%1] QTcpSocket established connection with server!")
                        .arg(QDateTime::currentDateTime().toString("HH:mm:ss.zzz")));

    // Perform immediate registration handshake
    QJsonObject regObj;
    regObj[QStringLiteral("type")] = QStringLiteral("register_pc");
    regObj[QStringLiteral("pcId")] = m_pcId;
    regObj[QStringLiteral("pcName")] = m_pcName;
    regObj[QStringLiteral("version")] = QStringLiteral("1.23.50");

    QJsonDocument doc(regObj);
    m_socket->write(doc.toJson(QJsonDocument::Compact) + "\n");
    m_socket->flush();

    // Start periodic round-trip ping telemetry
    m_pingTimer->start();
    sendPing();
}

void RelayManager::onDisconnected()
{
    m_isConnected = false;
    m_pingTimer->stop();
    emit connectionChanged(false);

    setStatus(QStringLiteral("Disconnected"), QStringLiteral("Socket connection lost"));
    emit logMessage(QString("[%1] QTcpSocket disconnected. Reconnecting in %2s…")
                        .arg(QDateTime::currentDateTime().toString("HH:mm:ss.zzz"))
                        .arg(m_retryIntervalMs / 1000));

    // Exponential backoff automatic reconnect
    m_reconnectTimer->start(m_retryIntervalMs);
    m_retryIntervalMs = qMin(m_retryIntervalMs * 2, 30000);
}

void RelayManager::onErrorOccurred(QAbstractSocket::SocketError socketError)
{
    Q_UNUSED(socketError)
    QString errStr = m_socket->errorString();
    setStatus(QStringLiteral("Connecting…"), QString("Waiting for server (%1)").arg(errStr));
    emit logMessage(QString("[%1] QTcpSocket error: %2")
                        .arg(QDateTime::currentDateTime().toString("HH:mm:ss.zzz"))
                        .arg(errStr));

    if (!m_reconnectTimer->isActive()) {
        m_reconnectTimer->start(m_retryIntervalMs);
        m_retryIntervalMs = qMin(m_retryIntervalMs * 2, 30000);
    }
}

void RelayManager::attemptReconnect()
{
    if (!m_isConnected && m_socket->state() == QAbstractSocket::UnconnectedState) {
        connectToServer();
    }
}

void RelayManager::sendPing()
{
    if (m_isConnected && m_socket->state() == QAbstractSocket::ConnectedState) {
        m_pingElapsed.restart();
        QJsonObject pingObj;
        pingObj[QStringLiteral("type")] = QStringLiteral("ping");
        pingObj[QStringLiteral("timestamp")] = QDateTime::currentMSecsSinceEpoch();
        QJsonDocument doc(pingObj);
        m_socket->write(doc.toJson(QJsonDocument::Compact) + "\n");
        m_socket->flush();
    }
}

void RelayManager::onReadyRead()
{
    while (m_socket->canReadLine()) {
        QByteArray line = m_socket->readLine().trimmed();
        if (!line.isEmpty()) {
            handleIncomingPacket(line);
        }
    }
}

void RelayManager::handleIncomingPacket(const QByteArray &data)
{
    m_packetCount++;
    emit packetReceived(m_packetCount);

    // Try parsing as JSON first
    QJsonParseError err;
    QJsonDocument doc = QJsonDocument::fromJson(data, &err);
    if (err.error == QJsonParseError::NoError && doc.isObject()) {
        processJsonCommand(doc.object());
        return;
    }

    // Fallback: Plaintext line protocol
    QString line = QString::fromUtf8(data);
    if (line.startsWith(QLatin1String("PONG")) || line.contains(QLatin1String("pong"), Qt::CaseInsensitive)) {
        if (m_pingElapsed.isValid()) {
            m_latency = static_cast<int>(m_pingElapsed.elapsed());
            emit latencyChanged(m_latency);
        }
    } else if (line.startsWith(QLatin1String("ROOM:"))) {
        m_roomId = line.section(':', 1).trimmed();
        emit roomChanged(m_roomId);
    } else {
        emit logMessage(QString("[RECV] %1").arg(line));
    }
}

void RelayManager::processJsonCommand(const QJsonObject &obj)
{
    QString type = obj.value(QStringLiteral("type")).toString().toLower();

    if (type == QLatin1String("pong")) {
        if (m_pingElapsed.isValid()) {
            m_latency = static_cast<int>(m_pingElapsed.elapsed());
            emit latencyChanged(m_latency);
        }
        return;
    }

    if (type == QLatin1String("registered") || type == QLatin1String("pc-registered")) {
        m_roomId = obj.value(QStringLiteral("roomId")).toString();
        if (m_roomId.isEmpty()) {
            m_roomId = obj.value(QStringLiteral("pcId")).toString(m_pcId);
        }
        emit roomChanged(m_roomId);
        setStatus(QStringLiteral("Connected to Relay"), QString("Registered ✓ Room: %1").arg(m_roomId));
        emit logMessage(QString("Successfully registered with Relay! Room: %1").arg(m_roomId));
        return;
    }

    emit commandExecuted(type, QString::fromUtf8(QJsonDocument(obj).toJson(QJsonDocument::Compact)));
}

void RelayManager::setStatus(const QString &text, const QString &detail)
{
    m_statusText = text;
    m_statusDetail = detail;
    emit statusChanged(m_statusText, m_statusDetail);
}
