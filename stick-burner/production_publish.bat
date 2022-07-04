@ECHO OFF
:CHOICE
SET /P c="Haluatko vied‰ nykyisen version tuotantoon? (K/E)"
IF /I "%c%" EQU "K" GOTO :PROCEED
IF /I "%c%" EQU "E" GOTO :CANCEL
GOTO :CHOICE

:PROCEED
ECHO Vied‰‰n tuotantoon

CALL publish_common.bat %1 %2 Release
GOTO :EOF

:CANCEL
ECHO Perutaan tuotantoonvienti
GOTO :EOF
