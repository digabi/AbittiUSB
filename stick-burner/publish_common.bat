SET VERSION=%1
SET REVISION=%2
SET CONFIGURATION=%3
SET SUFFIX=%4

SET APPVERSION=%VERSION:.=_%_%REVISION%

FINDSTR "/C:value=""%VERSION%""" App.config || GOTO :ERROR_VERSION
FINDSTR "/C:Revision>%REVISION%<" StickBurner.csproj || GOTO :ERROR_VERSION
FINDSTR "/C:Version>%VERSION%.%%2a<" StickBurner.csproj || GOTO :ERROR_VERSION
FINDSTR "/C:AssemblyName>AbittiUSB%SUFFIX%<" StickBurner.csproj || GOTO :ERROR_VERSION

REM SHA1 is a 40 characters long hex digest pointing to the digital signature
REM e.g. 123456789abcdef123456789abcdef123456789a
SET SHA1=123456789abcdef123456789abcdef123456789a

REM HASH is a hex digest to pointing to the digital signature
REM e.g. 12 34 56 78 9a bc df ef 12 34 56 78 9a bc cd ef 12 34 56 78
SET HASH=12 34 56 78 9a bc df ef 12 34 56 78 9a bc cd ef 12 34 56 78

msbuild /t:clean /t:publish /p:Configuration=%CONFIGURATION% || GOTO :ERROR

signtool sign /s my /sha1 %SHA1% /t http://timestamp.verisign.com/scripts/timstamp.dll /v "bin\%CONFIGURATION%\app.publish\Application Files\AbittiUSB%SUFFIX%_%APPVERSION%\AbittiUSB%SUFFIX%.exe" || GOTO :ERROR
signtool sign /s my /sha1 %SHA1% /t http://timestamp.verisign.com/scripts/timstamp.dll /v "bin\%CONFIGURATION%\app.publish\Application Files\AbittiUSB%SUFFIX%_%APPVERSION%\Uninstall.exe" || GOTO :ERROR
signtool sign /s my /sha1 %SHA1% /t http://timestamp.verisign.com/scripts/timstamp.dll /v "bin\%CONFIGURATION%\app.publish\AbittiUSB%SUFFIX%.exe" || GOTO :ERROR
signtool sign /s my /sha1 %SHA1% /t http://timestamp.verisign.com/scripts/timstamp.dll /v "bin\%CONFIGURATION%\app.publish\setup.exe" || GOTO :ERROR

mage -Update "bin\%CONFIGURATION%\app.publish\Application Files\AbittiUSB%SUFFIX%_%APPVERSION%\AbittiUSB%SUFFIX%.exe.manifest" -CertHash "%HASH%" || GOTO :ERROR
mage -Update "bin\%CONFIGURATION%\app.publish\AbittiUSB%SUFFIX%.application" -AppManifest "bin\%CONFIGURATION%\app.publish\Application Files\AbittiUSB%SUFFIX%_%APPVERSION%\AbittiUSB%SUFFIX%.exe.manifest" -CertHash "%HASH%" || GOTO :ERROR

msbuild /t:publishToAmazon /p:Configuration=%CONFIGURATION% || GOTO :ERROR

GOTO :EOF

:ERROR_VERSION
ECHO ERROR: Check assembly name, version and configuration fields in StickBurner.csproj and App.config

:ERROR
ECHO Failed with error #%ERRORLEVEL%.
EXIT /b %ERRORLEVEL%
