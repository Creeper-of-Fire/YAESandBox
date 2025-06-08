@echo off
setlocal enabledelayedexpansion

echo ===========================================
echo   Vue文件转TXT工具 (By ChatGPT)
echo ===========================================
echo.

set "SCAN_DIR="
set "OUTPUT_DIR=.\output"

rem 提示用户输入要扫描的后端项目根目录
echo 请输入要扫描的后端项目根目录 (例如: D:\my_backend_project):
echo (此目录下的所有子文件夹都将被递归扫描)
set /p SCAN_DIR="请输入: "

rem 检查用户是否输入了目录
if "%SCAN_DIR%"=="" (
    echo.
    echo 错误：未输入扫描目录。
    goto :end
)

rem 检查扫描目录是否存在
if not exist "%SCAN_DIR%" (
    echo.
    echo 错误：指定的扫描目录 "%SCAN_DIR%" 不存在。
    goto :end
)

rem 创建输出目录，如果不存在
if not exist "%OUTPUT_DIR%" (
    echo.
    echo 创建输出目录: %OUTPUT_DIR%
    mkdir "%OUTPUT_DIR%"
) else (
    echo.
    echo 输出目录 "%OUTPUT_DIR%" 已存在。
)

echo.
echo 开始扫描 "%SCAN_DIR%" 下的.vue文件并转换为.txt...
echo 请稍候...
echo.

set /a VUE_COUNT=0
set /a CONVERTED_COUNT=0

rem 使用for /r 递归查找所有.vue文件
rem %%~dpnxF 是文件的完整路径和名称
rem %%~nF 是文件名（不带后缀）
rem %%~xF 是文件后缀名
for /r "%SCAN_DIR%" %%F in (*.vue) do (
    set /a VUE_COUNT+=1
    set "SOURCE_FILE=%%F"
    set "FILE_NAME=%%~nF"
    set "DEST_FILE=!OUTPUT_DIR!\!FILE_NAME!.txt"
    
    echo 找到并处理: "!SOURCE_FILE!"
    
    rem 复制并改名
    copy "!SOURCE_FILE!" "!DEST_FILE!" >nul
    if exist "!DEST_FILE!" (
        set /a CONVERTED_COUNT+=1
    ) else (
        echo 警告: 复制文件 "!SOURCE_FILE!" 到 "!DEST_FILE!" 失败。
    )
)

echo.
echo ===========================================
echo 扫描和转换完成！
echo ===========================================
echo 总共找到 %VUE_COUNT% 个 .vue 文件。
echo 成功转换为 .txt 并复制了 %CONVERTED_COUNT% 个文件到 "%OUTPUT_DIR%"。
echo.

:end
pause
endlocal