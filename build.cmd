@ECHO OFF

SETLOCAL

SET SOLUTION_PATH="%~dp0src\CodeFormatter.sln"

msbuild %SOLUTION_PATH% /t:Restore /t:Build /p:Configuration=Release /p:OutDir="%~dp0bin" /nologo /m /v:m /flp:verbosity=normal %*
