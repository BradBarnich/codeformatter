@ECHO OFF

SETLOCAL

SET SOLUTION_PATH="%~dp0src\CodeFormatter.sln"

nuget restore %SOLUTION_PATH%
msbuild %SOLUTION_PATH% /t:Build /p:Configuration=Release /nologo /m /v:m /flp:verbosity=normal %*
