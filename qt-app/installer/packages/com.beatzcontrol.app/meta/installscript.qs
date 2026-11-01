function Component()
{
    // Constructor for Qt Installer Framework component
}

Component.prototype.createOperations = function()
{
    // Execute default unpacking operations
    component.createOperations();

    if (systemInfo.productType === "windows") {
        // Create Start Menu program entry
        component.addOperation("CreateShortcut",
            "@TargetDir@/BeatzControl.exe",
            "@StartMenuDir@/Beatz Control.lnk",
            "workingDirectory=@TargetDir@",
            "iconPath=@TargetDir@/BeatzControl.exe",
            "iconId=0",
            "description=Beatz Control Desktop Remote Service"
        );

        // Create Desktop shortcut
        component.addOperation("CreateShortcut",
            "@TargetDir@/BeatzControl.exe",
            "@DesktopDir@/Beatz Control.lnk",
            "workingDirectory=@TargetDir@",
            "iconPath=@TargetDir@/BeatzControl.exe",
            "iconId=0",
            "description=Beatz Control Desktop Remote Service"
        );

        // Create uninstaller shortcut in Start Menu
        component.addOperation("CreateShortcut",
            "@TargetDir@/maintenancetool.exe",
            "@StartMenuDir@/Uninstall Beatz Control.lnk",
            "workingDirectory=@TargetDir@"
        );
    }
}
