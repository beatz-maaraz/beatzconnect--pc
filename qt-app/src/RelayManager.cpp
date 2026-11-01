#include "RelayManager.h"
#include <QDateTime>
#include <QDebug>
#include <QJsonArray>
#include <QRandomGenerator>
#include <QMutexLocker>

// ═══════════════════════════════════════════════════════════════════════════════
// NetworkWorker Implementation: Background Thread for ALL Network Operations
// ═══════════════════════════════════════════════════════════════════════════════

NetworkWorker::NetworkWorker(QObject *parent)
    : QObject(parent)
    , m_socket(nullptr)
    , m_reconnectTimer(nullptr)
    , m_pingTimer(nullptr)
    , m_port(8080)
    , m_retryIntervalMs(3000)
    , m_isConnected(false)
{
}

NetworkWorker::~NetworkWorker()
{
    if (m_socket && m_socket->state() != QAbstractSocket::UnconnectedState) {
        m_socket->disconnectFromHost();
        m_socket->waitForDisconnected(1000);
    }
}

void NetworkWorker::initialize()
{
    // Initialize socket and timers ON THE WORKER THREAD
    m_socket = new QTcpSocket(this);
    
    // ✓ Enable TCP_NODELAY for ultra-low latency (disables Nagle's algorithm)
    m_socket->setSocketOption(QAbstractSocket::LowDelayOption, 1);
    m_socket->setSocketOption(QAbstractSocket::KeepAliveOption, 1);
    
    connect(m_socket, &QTcpSocket::connected, this, &NetworkWorker::onSocketConnected);
    connect(m_socket, &QTcpSocket::disconnected, this, &NetworkWorker::onSocketDisconnected);
    connect(m_socket, &QTcpSocket::readyRead, this, &NetworkWorker::onSocketReadyRead);
#if QT_VERSION >= QT_VERSION_CHECK(5, 15, 0)
    connect(m_socket, &QTcpSocket::errorOccurred, this, &NetworkWorker::onSocketError);
#else
    connect(m_socket, QOverload<QAbstractSocket::SocketError>::of(&QAbstractSocket::error),
            this, &NetworkWorker::onSocketError);
#endif

    m_reconnectTimer = new QTimer(this);
    m_reconnectTimer->setSingleShot(true);
    connect(m_reconnectTimer, &QTimer::timeout, this, &NetworkWorker::attemptReconnect);

    m_pingTimer = new QTimer(this);
    m_pingTimer->setInterval(4000);
    connect(m_pingTimer, &QTimer::timeout, this, &NetworkWorker::sendPing);

    qDebug() << "[NetworkWorker] Initialized on thread:" << QThread::currentThread();
}

void NetworkWorker::connectToServer(const QString &host, int port)
{
    m_host = host;
    m_port = port;
    m_reconnectTimer->stop();

    if (m_socket->state() == QAbstractSocket::ConnectedState) {
        m_socket->disconnectFromHost();
    }

    emit statusUpdate(QStringLiteral("Connecting…"), 
                     QString("Connecting to %1:%2 via background thread").arg(m_host).arg(m_port));
    
    qDebug() << "[NetworkWorker] Connecting to" << m_host << ":" << m_port 
             << "on thread:" << QThread::currentThread();
    
    m_socket->connectToHost(m_host, static_cast<quint16>(m_port));
}

void NetworkWorker::disconnectFromServer()
{
    m_reconnectTimer->stop();
    m_pingTimer->stop();

    if (m_socket->state() != QAbstractSocket::UnconnectedState) {
        m_socket->disconnectFromHost();
    }
    emit statusUpdate(QStringLiteral("Disconnected"), QStringLiteral("Disconnected by user"));
}

void NetworkWorker::sendCommand(const QString &cmd)
{
    if (!m_isConnected || m_socket->state() != QAbstractSocket::ConnectedState) {
        qWarning() << "[NetworkWorker] Cannot send command: socket not connected";
        return;
    }

    QByteArray bytes = cmd.toUtf8();
    if (!bytes.endsWith('\n')) {
        bytes.append('\n');
    }
    m_socket->write(bytes);
    m_socket->flush();
}

void NetworkWorker::sendPing()
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

void NetworkWorker::onSocketConnected()
{
    m_isConnected = true;
    m_retryIntervalMs = 3000;
    
    qDebug() << "[NetworkWorker] Socket connected on thread:" << QThread::currentThread();
    
    emit connected();
    emit statusUpdate(QStringLiteral("Connected to Relay"), 
                     QString("Established on port %1").arg(m_port));
    
    m_pingTimer->start();
    sendPing();
}

void NetworkWorker::onSocketDisconnected()
{
    m_isConnected = false;
    m_pingTimer->stop();
    
    qDebug() << "[NetworkWorker] Socket disconnected. Reconnecting in" << (m_retryIntervalMs / 1000) << "seconds...";
    
    emit disconnected();
    emit statusUpdate(QStringLiteral("Disconnected"), QStringLiteral("Socket connection lost"));
    
    // Exponential backoff automatic reconnect
    m_reconnectTimer->start(m_retryIntervalMs);
    m_retryIntervalMs = qMin(m_retryIntervalMs * 2, 30000);
}

void NetworkWorker::onSocketReadyRead()
{
    // Process ALL incoming data on background thread (no UI blocking!)
    while (m_socket->canReadLine()) {
        QByteArray line = m_socket->readLine().trimmed();
        if (!line.isEmpty()) {
            // Emit raw data to main thread for processing
            emit dataReceived(line);
        }
    }
}

void NetworkWorker::onSocketError(QAbstractSocket::SocketError socketError)
{
    Q_UNUSED(socketError)
    QString errStr = m_socket->errorString();
    
    qWarning() << "[NetworkWorker] Socket error:" << errStr;
    
    emit errorOccurred(errStr);
    emit statusUpdate(QStringLiteral("Connecting…"), QString("Waiting for server (%1)").arg(errStr));
    
    if (!m_reconnectTimer->isActive()) {
        m_reconnectTimer->start(m_retryIntervalMs);
        m_retryIntervalMs = qMin(m_retryIntervalMs * 2, 30000);
    }
}

void NetworkWorker::attemptReconnect()
{
    if (!m_isConnected && m_socket->state() == QAbstractSocket::UnconnectedState) {
        connectToServer(m_host, m_port);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// RelayManager Implementation: Main Thread Facade
// ═══════════════════════════════════════════════════════════════════════════════

RelayManager::RelayManager(QObject *parent)
    : QObject(parent)
    , m_workerThread(nullptr)
    , m_worker(nullptr)
    , m_isConnected(false)
    , m_statusText(QStringLiteral("Initialising…"))
    , m_statusDetail(QStringLiteral("Ready to connect"))
    , m_roomId(QStringLiteral("—"))
    , m_host(QStringLiteral("127.0.0.1"))
    , m_port(8080)
    , m_packetCount(0)
    , m_latency(0)
{
    m_pcName = QHostInfo::localHostName();
    m_pcId = m_pcName.toLower().replace(QLatin1Char(' '), QLatin1Char('_'));

    // ✓✓✓ Create dedicated worker thread for network operations ✓✓✓
    m_workerThread = new QThread(this);
    m_worker = new NetworkWorker();
    m_worker->moveToThread(m_workerThread);

    // Connect worker signals to main thread slots (Qt::QueuedConnection by default)
    connect(m_worker, &NetworkWorker::connected, this, &RelayManager::onWorkerConnected);
    connect(m_worker, &NetworkWorker::disconnected, this, &RelayManager::onWorkerDisconnected);
    connect(m_worker, &NetworkWorker::errorOccurred, this, &RelayManager::onWorkerError);
    connect(m_worker, &NetworkWorker::dataReceived, this, &RelayManager::onWorkerDataReceived);
    connect(m_worker, &NetworkWorker::latencyMeasured, this, &RelayManager::onWorkerLatency);
    connect(m_worker, &NetworkWorker::statusUpdate, this, &RelayManager::onWorkerStatus);

    // Connect main thread signals to worker slots (cross-thread via Qt::QueuedConnection)
    connect(this, &RelayManager::requestConnect, m_worker, &NetworkWorker::connectToServer);
    connect(this, &RelayManager::requestDisconnect, m_worker, &NetworkWorker::disconnectFromServer);
    connect(this, &RelayManager::requestSendCommand, m_worker, &NetworkWorker::sendCommand);

    // Initialize worker when thread starts
    connect(m_workerThread, &QThread::started, m_worker, &NetworkWorker::initialize);
    
    // Cleanup when thread finishes
    connect(m_workerThread, &QThread::finished, m_worker, &QObject::deleteLater);

    // Start the worker thread!
    m_workerThread->start();
    
    qDebug() << "[RelayManager] Worker thread started. Main thread:" << QThread::currentThread();

    // Automatic server connection upon application launch!
    QTimer::singleShot(500, this, [this]() {
        connectToServer();
    });
}

RelayManager::~RelayManager()
{
    qDebug() << "[RelayManager] Shutting down worker thread...";
    disconnectFromServer();
    
    if (m_workerThread) {
        m_workerThread->quit();
        if (!m_workerThread->wait(3000)) {
            qWarning() << "[RelayManager] Worker thread did not terminate gracefully, forcing...";
            m_workerThread->terminate();
            m_workerThread->wait();
        }
    }
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
    if (!host.isEmpty()) {
        m_host = host;
        emit hostChanged(m_host);
    }
    if (port > 0) {
        m_port = port;
        emit portChanged(m_port);
    }

    setStatus(QStringLiteral("Connecting…"), QString("Initiating connection to %1:%2").arg(m_host).arg(m_port));
    emit logMessage(QString("[%1] Requesting connection to %2:%3 via background thread")
                        .arg(QDateTime::currentDateTime().toString("HH:mm:ss.zzz"))
                        .arg(m_host)
                        .arg(m_port));

    // Signal worker thread to connect (non-blocking!)
    emit requestConnect(m_host, m_port);
}

void RelayManager::disconnectFromServer()
{
    emit requestDisconnect();
    setStatus(QStringLiteral("Disconnected"), QStringLiteral("Disconnected by user"));
}

void RelayManager::sendCommand(const QString &cmd)
{
    if (!m_isConnected) {
        emit logMessage(QStringLiteral("[Warning] Cannot send command: not connected"));
        return;
    }

    // Send command to worker thread (non-blocking!)
    emit requestSendCommand(cmd);
}

// ───────────────────────────────────────────────────────────────────────────────
// Worker Thread Signal Handlers (called on main thread via Qt::QueuedConnection)
// ───────────────────────────────────────────────────────────────────────────────

void RelayManager::onWorkerConnected()
{
    QMutexLocker locker(&m_stateMutex);
    m_isConnected = true;
    emit connectionChanged(true);
    
    emit logMessage(QString("[%1] Background thread established connection!")
                        .arg(QDateTime::currentDateTime().toString("HH:mm:ss.zzz")));

    // Perform immediate registration handshake
    QJsonObject regObj;
    regObj[QStringLiteral("type")] = QStringLiteral("register_pc");
    regObj[QStringLiteral("pcId")] = m_pcId;
    regObj[QStringLiteral("pcName")] = m_pcName;
    regObj[QStringLiteral("version")] = QStringLiteral("1.23.50");

    QJsonDocument doc(regObj);
    emit requestSendCommand(QString::fromUtf8(doc.toJson(QJsonDocument::Compact)));
}

void RelayManager::onWorkerDisconnected()
{
    QMutexLocker locker(&m_stateMutex);
    m_isConnected = false;
    emit connectionChanged(false);
    
    emit logMessage(QString("[%1] Background thread disconnected. Auto-reconnecting...")
                        .arg(QDateTime::currentDateTime().toString("HH:mm:ss.zzz")));
}

void RelayManager::onWorkerError(const QString &errorString)
{
    emit logMessage(QString("[%1] Worker thread error: %2")
                        .arg(QDateTime::currentDateTime().toString("HH:mm:ss.zzz"))
                        .arg(errorString));
}

void RelayManager::onWorkerDataReceived(const QByteArray &data)
{
    // Process incoming packet on main thread (data already read from socket on worker thread)
    handleIncomingPacket(data);
}

void RelayManager::onWorkerLatency(int ms)
{
    m_latency = ms;
    emit latencyChanged(m_latency);
}

void RelayManager::onWorkerStatus(const QString &text, const QString &detail)
{
    setStatus(text, detail);
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
        return;
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

