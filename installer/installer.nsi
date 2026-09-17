; NSIS installer script for OssClientPro (Windows)
!define PRODUCT_NAME "OssClientPro"
!define PRODUCT_VERSION "1.2.0"
!define PRODUCT_PUBLISHER "Camliar"

Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
OutFile "..\bin\Release\net10.0\win-x64\OssClientPro-Setup.exe"
InstallDir "$PROGRAMFILES64\${PRODUCT_NAME}"
RequestExecutionLevel admin
SetCompressor lzma

Section "Install"
    SetOutPath "$INSTDIR"
    File /r "..\bin\Release\net10.0\win-x64\publish\*.*"
    CreateShortCut "$DESKTOP\${PRODUCT_NAME}.lnk" "$INSTDIR\OssClientPro.exe"
    CreateDirectory "$SMPROGRAMS\${PRODUCT_NAME}"
    CreateShortCut "$SMPROGRAMS\${PRODUCT_NAME}\${PRODUCT_NAME}.lnk" "$INSTDIR\OssClientPro.exe"
    WriteUninstaller "$INSTDIR\uninstall.exe"
SectionEnd

Section "Uninstall"
    Delete "$DESKTOP\${PRODUCT_NAME}.lnk"
    RMDir /r "$SMPROGRAMS\${PRODUCT_NAME}"
    RMDir /r "$INSTDIR"
SectionEnd
