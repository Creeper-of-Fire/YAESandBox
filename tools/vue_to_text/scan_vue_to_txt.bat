@echo off
setlocal enabledelayedexpansion

echo ===========================================
echo   Vue�ļ�תTXT���� (By ChatGPT)
echo ===========================================
echo.

set "SCAN_DIR="
set "OUTPUT_DIR=.\output"

rem ��ʾ�û�����Ҫɨ��ĺ����Ŀ��Ŀ¼
echo ������Ҫɨ��ĺ����Ŀ��Ŀ¼ (����: D:\my_backend_project):
echo (��Ŀ¼�µ��������ļ��ж������ݹ�ɨ��)
set /p SCAN_DIR="������: "

rem ����û��Ƿ�������Ŀ¼
if "%SCAN_DIR%"=="" (
    echo.
    echo ����δ����ɨ��Ŀ¼��
    goto :end
)

rem ���ɨ��Ŀ¼�Ƿ����
if not exist "%SCAN_DIR%" (
    echo.
    echo ����ָ����ɨ��Ŀ¼ "%SCAN_DIR%" �����ڡ�
    goto :end
)

rem �������Ŀ¼�����������
if not exist "%OUTPUT_DIR%" (
    echo.
    echo �������Ŀ¼: %OUTPUT_DIR%
    mkdir "%OUTPUT_DIR%"
) else (
    echo.
    echo ���Ŀ¼ "%OUTPUT_DIR%" �Ѵ��ڡ�
)

echo.
echo ��ʼɨ�� "%SCAN_DIR%" �µ�.vue�ļ���ת��Ϊ.txt...
echo ���Ժ�...
echo.

set /a VUE_COUNT=0
set /a CONVERTED_COUNT=0

rem ʹ��for /r �ݹ��������.vue�ļ�
rem %%~dpnxF ���ļ�������·��������
rem %%~nF ���ļ�����������׺��
rem %%~xF ���ļ���׺��
for /r "%SCAN_DIR%" %%F in (*.vue) do (
    set /a VUE_COUNT+=1
    set "SOURCE_FILE=%%F"
    set "FILE_NAME=%%~nF"
    set "DEST_FILE=!OUTPUT_DIR!\!FILE_NAME!.txt"
    
    echo �ҵ�������: "!SOURCE_FILE!"
    
    rem ���Ʋ�����
    copy "!SOURCE_FILE!" "!DEST_FILE!" >nul
    if exist "!DEST_FILE!" (
        set /a CONVERTED_COUNT+=1
    ) else (
        echo ����: �����ļ� "!SOURCE_FILE!" �� "!DEST_FILE!" ʧ�ܡ�
    )
)

echo.
echo ===========================================
echo ɨ���ת����ɣ�
echo ===========================================
echo �ܹ��ҵ� %VUE_COUNT% �� .vue �ļ���
echo �ɹ�ת��Ϊ .txt �������� %CONVERTED_COUNT% ���ļ��� "%OUTPUT_DIR%"��
echo.

:end
pause
endlocal