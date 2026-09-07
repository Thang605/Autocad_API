@echo off
chcp 65001 >nul
title DONG BO GIT LEN GITHUB: Autocad_API
cd /d "c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API"

echo ================================================================
echo  🚀 ĐANG ĐẨY CODE LÊN GITHUB: https://github.com/Thang605/Autocad_API.git
echo ================================================================
echo.
echo  📌 HƯỚNG DẪN XÁC THỰC:
echo  1. Trình duyệt Web hoặc cửa sổ GitHub Login sẽ tự động bật lên.
echo  2. Chọn "Sign in with your browser" -> Đăng nhập tài khoản Thang605.
echo  3. Bấm "Authorize Git-Credential-Manager".
echo  (Chỉ cần đăng nhập 1 lần duy nhất, Windows sẽ tự động ghi nhớ).
echo ================================================================
echo.

git push -u origin master

echo.
if %errorlevel% equ 0 (
    echo ================================================================
    echo  🎉 CHÚC MỪNG: ĐÃ ĐẨY CODE THÀNH CÔNG LÊN GITHUB!
    echo ================================================================
) else (
    echo ================================================================
    echo  ❌ CÓ LỖI XẢY RA TRONG QUÁ TRÌNH PUSH. Vui lòng kiểm tra lại.
    echo ================================================================
)
echo.
pause
