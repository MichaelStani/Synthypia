@echo off
rem Baut SynthypiaVst3.dll (x64) mit dem MSVC-Compiler und dem VST3-SDK (MIT-Lizenz, third_party\vst3sdk).
rem Aufruf: build.cmd [Debug|Release]   Die DLL landet neben Synthypia.exe in Synthypia\bin\<Konfiguration>.
setlocal
set CFG=%1
if "%CFG%"=="" set CFG=Debug
set SDK=%~dp0..\third_party\vst3sdk
set OUT=%~dp0..\Synthypia\bin\%CFG%
set OBJ=%~dp0obj\%CFG%
if not exist "%OUT%" mkdir "%OUT%"
if not exist "%OBJ%" mkdir "%OBJ%"
call "C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat" >nul || exit /b 1
set FLAGS=/nologo /utf-8 /std:c++17 /EHsc /MT /O2 /W3 /DNOMINMAX /DUNICODE /D_UNICODE /D_CRT_SECURE_NO_WARNINGS /DRELEASE=1 /I"%SDK%"
cl %FLAGS% /LD /Fo"%OBJ%\\" /Fe"%OUT%\SynthypiaVst3.dll" "%~dp0vst3host.cpp" ^
  "%SDK%\pluginterfaces\base\conststringtable.cpp" ^
  "%SDK%\pluginterfaces\base\coreiids.cpp" ^
  "%SDK%\pluginterfaces\base\funknown.cpp" ^
  "%SDK%\pluginterfaces\base\ustring.cpp" ^
  "%SDK%\base\source\baseiids.cpp" ^
  "%SDK%\base\source\fbuffer.cpp" ^
  "%SDK%\base\source\fdebug.cpp" ^
  "%SDK%\base\source\fdynlib.cpp" ^
  "%SDK%\base\source\fobject.cpp" ^
  "%SDK%\base\source\fstreamer.cpp" ^
  "%SDK%\base\source\fstring.cpp" ^
  "%SDK%\base\source\updatehandler.cpp" ^
  "%SDK%\base\thread\source\fcondition.cpp" ^
  "%SDK%\base\thread\source\flock.cpp" ^
  "%SDK%\public.sdk\source\common\commoniids.cpp" ^
  "%SDK%\public.sdk\source\common\memorystream.cpp" ^
  "%SDK%\public.sdk\source\vst\utility\stringconvert.cpp" ^
  "%SDK%\public.sdk\source\common\commonstringconvert.cpp" ^
  "%SDK%\public.sdk\source\common\threadchecker_win32.cpp" ^
  "%SDK%\public.sdk\source\vst\vstinitiids.cpp" ^
  "%SDK%\public.sdk\source\vst\hosting\connectionproxy.cpp" ^
  "%SDK%\public.sdk\source\vst\hosting\eventlist.cpp" ^
  "%SDK%\public.sdk\source\vst\hosting\hostclasses.cpp" ^
  "%SDK%\public.sdk\source\vst\hosting\module.cpp" ^
  "%SDK%\public.sdk\source\vst\hosting\module_win32.cpp" ^
  "%SDK%\public.sdk\source\vst\hosting\parameterchanges.cpp" ^
  "%SDK%\public.sdk\source\vst\hosting\pluginterfacesupport.cpp" ^
  "%SDK%\public.sdk\source\vst\hosting\plugprovider.cpp" ^
  "%SDK%\public.sdk\source\vst\hosting\processdata.cpp" ^
  /link ole32.lib shell32.lib user32.lib
exit /b %ERRORLEVEL%
