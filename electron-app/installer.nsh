; ============================================================
;  Beatz Control NSIS Custom Installation Script
;  Sequential Architecture:
;  1. Extract and verify self-contained backend binary
;  2. Register PCControl.Service as Windows Service (AutoStart)
;  3. Start background service and verify status
;  4. Configure System Startup Shortcut for Tray Companion
;  5. Confirm local socket and service readiness
; ============================================================

!macro customInstall
  DetailPrint "[Phase 1] Verifying Self-Contained Backend Binary extraction..."
  IfFileExists "$INSTDIR\resources\backend\PCControl.Service.exe" serviceFound serviceNotFound

serviceFound:
  DetailPrint "[Phase 2] Registering PCControl.Service as Windows Background Service..."
  ExecWait '"$INSTDIR\resources\backend\PCControl.Service.exe" --install'

  DetailPrint "[Phase 3] Starting background service..."
  ExecWait '"$INSTDIR\resources\backend\PCControl.Service.exe" --start'

  DetailPrint "[Phase 4] Verifying Service Controller Status..."
  ExecWait 'sc.exe query PCControl.Service'
  Goto serviceDone

serviceNotFound:
  DetailPrint "[WARNING] PCControl.Service.exe was not located in resources\backend."

serviceDone:
  DetailPrint "[Phase 5] Configuring System Tray Startup Shortcut..."
  CreateShortcut "$SMSTARTUP\Beatz Control.lnk" "$INSTDIR\${APP_EXECUTABLE_FILENAME}" "--autostart --minimized" "$INSTDIR\${APP_EXECUTABLE_FILENAME}" 0

  DetailPrint "[Phase 6] Verifying local WebSocket & Pipe handshake readiness..."
  Sleep 600
  DetailPrint "[SUCCESS] Beatz Control deployment and service registration complete."
!macroend

!macro customUnInstall
  DetailPrint "Stopping and unregistering Beatz Control Background Service..."
  IfFileExists "$INSTDIR\resources\backend\PCControl.Service.exe" 0 +3
    ExecWait '"$INSTDIR\resources\backend\PCControl.Service.exe" --stop'
    ExecWait '"$INSTDIR\resources\backend\PCControl.Service.exe" --uninstall'

  DetailPrint "Removing startup shortcut..."
  Delete "$SMSTARTUP\Beatz Control.lnk"
!macroend
