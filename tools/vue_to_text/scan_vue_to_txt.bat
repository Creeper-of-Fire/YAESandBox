@echo off
setlocal enabledelayedexpansion

:start_loop
echo ===========================================
echo   Vue�ļ�תTXT���� (By ChatGPT)
echo ===========================================
echo.

set "DEFAULT_SCAN_DIR=C:\Users\Creeper10\Desktop\ProjectForFun\YAESandBox\frontend\src\app-workbench"
set "OUTPUT_DIR=.\output"

rem ��ʾ�û��Ƿ�ʹ��Ĭ��Ŀ¼
echo ��ǰĬ��ɨ��Ŀ¼Ϊ: "%DEFAULT_SCAN_DIR%"
echo �����ʹ������Ŀ¼���������µ�·��������ֱ�Ӱ��س���ʹ��Ĭ��Ŀ¼��
set /p "USER_SCAN_DIR=������ɨ��Ŀ¼ (���س�ʹ��Ĭ��Ŀ¼): "

rem ����û������ˣ���ʹ���û������Ŀ¼������ʹ��Ĭ��Ŀ¼
if defined USER_SCAN_DIR (
    set "SCAN_DIR=!USER_SCAN_DIR!"
) else (
    set "SCAN_DIR=!DEFAULT_SCAN_DIR!"
)

rem ���ɨ��Ŀ¼�Ƿ����
if not exist "%SCAN_DIR%" (
    echo.
    echo ����ָ����ɨ��Ŀ¼ "%SCAN_DIR%" �����ڡ�
    goto :ask_continue
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

rem ������Ŀ¼�е�����.txt�ļ�
echo.
echo ����������Ŀ¼ "%OUTPUT_DIR%" �еľ��ļ�...
del /q "%OUTPUT_DIR%\*.txt" >nul
if exist "%OUTPUT_DIR%\*.txt" (
    echo ����: �����ļ�����δ�ܳɹ�ɾ�������ֶ���顣
) else (
    echo ���ļ��������
)

echo.
echo ��ʼɨ�� "%SCAN_DIR%" �µ�.vue�ļ���ת��Ϊ.txt...
echo ���Ժ�...
echo.

set /a VUE_COUNT=0
set /a CONVERTED_COUNT=0

rem ʹ��for /r �ݹ��������.vue�ļ�
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

:ask_continue
echo �Ƿ�Ҫ����������һ��ת���� (���� Y ���������� N �˳�)
set /p "CHOICE=������: "

if /i "%CHOICE%"=="Y" (
    echo.
    echo ׼����ʼ��һ��ת��...
    echo.
    goto :start_loop
) else if /i "%CHOICE%"=="N" (
    echo ��лʹ�ã�
    goto :end
) else (
    echo ��Ч���룬Ĭ��Ϊ�˳���
    goto :end
)

:end
pause
endlocal