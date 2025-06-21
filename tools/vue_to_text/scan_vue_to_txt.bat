@echo off
setlocal enabledelayedexpansion

:start_loop
echo ===========================================
echo   Vue文件转TXT工具 (By ChatGPT)
echo ===========================================
echo.

set "DEFAULT_SCAN_DIR=C:\Users\Creeper10\Desktop\ProjectForFun\YAESandBox\frontend\src\app-workbench"
set "OUTPUT_DIR=.\output"

rem 提示用户是否使用默认目录
echo 当前默认扫描目录为: "%DEFAULT_SCAN_DIR%"
echo 如果想使用其他目录，请输入新的路径；否则，直接按回车键使用默认目录。
set /p "USER_SCAN_DIR=请输入扫描目录 (按回车使用默认目录): "

rem 如果用户输入了，则使用用户输入的目录；否则使用默认目录
if defined USER_SCAN_DIR (
    set "SCAN_DIR=!USER_SCAN_DIR!"
) else (
    set "SCAN_DIR=!DEFAULT_SCAN_DIR!"
)

rem 检查扫描目录是否存在
if not exist "%SCAN_DIR%" (
    echo.
    echo 错误：指定的扫描目录 "%SCAN_DIR%" 不存在。
    goto :ask_continue
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

rem 清空输出目录中的所有.txt文件
echo.
echo 正在清除输出目录 "%OUTPUT_DIR%" 中的旧文件...
del /q "%OUTPUT_DIR%\*.txt" >nul
if exist "%OUTPUT_DIR%\*.txt" (
    echo 警告: 部分文件可能未能成功删除，请手动检查。
) else (
    echo 旧文件已清除。
)

echo.
echo 开始扫描 "%SCAN_DIR%" 下的.vue文件并转换为.txt...
echo 请稍候...
echo.

set /a VUE_COUNT=0
set /a CONVERTED_COUNT=0

rem 使用for /r 递归查找所有.vue文件
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

:ask_continue
echo 是否要继续进行下一次转换？ (输入 Y 继续，输入 N 退出)
set /p "CHOICE=请输入: "

if /i "%CHOICE%"=="Y" (
    echo.
    echo 准备开始下一次转换...
    echo.
    goto :start_loop
) else if /i "%CHOICE%"=="N" (
    echo 感谢使用！
    goto :end
) else (
    echo 无效输入，默认为退出。
    goto :end
)

:end
pause
endlocal