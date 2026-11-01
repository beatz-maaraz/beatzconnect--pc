#include "HardwareController.h"

#include <QProcess>
#include <QVariantMap>
#include <QDebug>

#ifdef Q_OS_WIN
#include <windows.h>
#endif

HardwareController::HardwareController(QObject *parent)
    : QObject(parent)
{
}

bool HardwareController::lockWorkstation()
{
#ifdef Q_OS_WIN
    BOOL ok = LockWorkStation();
    if (ok) {
        emit statusNotice(QStringLiteral("Workstation locked successfully"));
        return true;
    }
#endif
    QProcess::startDetached(QStringLiteral("rundll32.exe"), {QStringLiteral("user32.dll,LockWorkStation")});
    emit statusNotice(QStringLiteral("Workstation lock executed"));
    return true;
}

bool HardwareController::addFirewallRule()
{
#ifdef Q_OS_WIN
    QStringList args;
    args << QStringLiteral("advfirewall") << QStringLiteral("firewall") << QStringLiteral("add") << QStringLiteral("rule")
         << QStringLiteral("name=Beatz Control") << QStringLiteral("dir=in") << QStringLiteral("action=allow")
         << QStringLiteral("protocol=TCP") << QStringLiteral("localport=8080");

    QProcess proc;
    proc.start(QStringLiteral("netsh"), args);
    proc.waitForFinished(4000);
    emit statusNotice(QStringLiteral("Firewall rule configured for TCP 8080"));
    return (proc.exitCode() == 0);
#else
    return false;
#endif
}

bool HardwareController::toggleHotspot()
{
#ifdef Q_OS_WIN
    QProcess proc;
    proc.start(QStringLiteral("netsh"), {QStringLiteral("wlan"), QStringLiteral("start"), QStringLiteral("hostednetwork")});
    proc.waitForFinished(3000);
    emit statusNotice(QStringLiteral("Hotspot start command executed"));
    return true;
#else
    return false;
#endif
}

void HardwareController::setVolume(int percent)
{
    int clamped = qBound(0, percent, 100);

#ifdef Q_OS_WIN
    // Step volume using WScript media key simulation
    QString script = QString(
        "$wsh = New-Object -ComObject WScript.Shell; "
        "1..50 | ForEach-Object { $wsh.SendKeys([char]174) }; "
        "$steps = [math]::Round(%1 / 2); "
        "1..$steps | ForEach-Object { $wsh.SendKeys([char]175) };"
    ).arg(clamped);

    QProcess::startDetached(QStringLiteral("powershell"), {QStringLiteral("-NoProfile"), QStringLiteral("-Command"), script});
#endif

    emit volumeChanged(clamped);
}

int HardwareController::getVolume()
{
    return 50; // default safe baseline
}

void HardwareController::moveMouse(int dx, int dy)
{
#ifdef Q_OS_WIN
    POINT p;
    if (GetCursorPos(&p)) {
        SetCursorPos(p.x + dx, p.y + dy);
    }
#else
    Q_UNUSED(dx)
    Q_UNUSED(dy)
#endif
}

void HardwareController::mouseClick(const QString &button)
{
#ifdef Q_OS_WIN
    DWORD down = MOUSEEVENTF_LEFTDOWN;
    DWORD up   = MOUSEEVENTF_LEFTUP;

    if (button.compare(QLatin1String("right"), Qt::CaseInsensitive) == 0) {
        down = MOUSEEVENTF_RIGHTDOWN;
        up   = MOUSEEVENTF_RIGHTUP;
    } else if (button.compare(QLatin1String("middle"), Qt::CaseInsensitive) == 0) {
        down = MOUSEEVENTF_MIDDLEDOWN;
        up   = MOUSEEVENTF_MIDDLEUP;
    }

    mouse_event(down, 0, 0, 0, 0);
    mouse_event(up, 0, 0, 0, 0);
#else
    Q_UNUSED(button)
#endif
}

QVariantList HardwareController::getActiveWindows()
{
    QVariantList list;

#ifdef Q_OS_WIN
    // Populate active visible top-level windows using EnumWindows
    struct EnumData {
        QVariantList *pList;
    };

    EnumData data = { &list };

    auto enumCallback = [](HWND hwnd, LPARAM lParam) -> BOOL {
        if (!IsWindowVisible(hwnd) || IsIconic(hwnd)) return TRUE;

        int len = GetWindowTextLengthW(hwnd);
        if (len <= 0) return TRUE;

        // Skip windows without taskbar presentation
        LONG style = GetWindowLongW(hwnd, GWL_STYLE);
        LONG exStyle = GetWindowLongW(hwnd, GWL_EXSTYLE);
        if (!(style & WS_VISIBLE) || (exStyle & WS_EX_TOOLWINDOW)) return TRUE;

        wchar_t titleBuf[512];
        GetWindowTextW(hwnd, titleBuf, 512);
        QString title = QString::fromWCharArray(titleBuf);
        if (title.isEmpty() || title == QLatin1String("Default IME") || title == QLatin1String("MSCTFIME UI"))
            return TRUE;

        DWORD pid = 0;
        GetWindowThreadProcessId(hwnd, &pid);

        QVariantMap item;
        item[QStringLiteral("title")] = title;
        item[QStringLiteral("pid")] = static_cast<int>(pid);
        item[QStringLiteral("hwnd")] = QString::number(reinterpret_cast<quintptr>(hwnd));

        auto *pData = reinterpret_cast<EnumData*>(lParam);
        pData->pList->append(item);

        return TRUE;
    };

    EnumWindows(enumCallback, reinterpret_cast<LPARAM>(&data));
#endif

    return list;
}

bool HardwareController::killProcess(int pid)
{
#ifdef Q_OS_WIN
    HANDLE hProcess = OpenProcess(PROCESS_TERMINATE, FALSE, static_cast<DWORD>(pid));
    if (hProcess) {
        BOOL ok = TerminateProcess(hProcess, 0);
        CloseHandle(hProcess);
        return ok;
    }
#endif
    return false;
}
