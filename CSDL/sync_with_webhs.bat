@echo off
REM ========================================
REM SCRIPT ĐỒNG BỘ NHOM1 VỚI WEBHS
REM ========================================
echo.
echo ========================================
echo ĐỒNG BỘ NHOM1 VỚI WEBHS
echo ========================================
echo.

cd /d d:\Nhom1

REM Step 1: Đổi tên files
echo [1/5] Đổi tên files...
if exist Controllers\ChatsController.cs (
    ren Controllers\ChatsController.cs ConversationsController.cs
    echo ✅ Đã đổi tên ChatsController.cs → ConversationsController.cs
) else (
    echo ⚠️ File ChatsController.cs không tồn tại hoặc đã đổi tên
)

if exist Services\ChatService.cs (
    ren Services\ChatService.cs ConversationService.cs
    echo ✅ Đã đổi tên ChatService.cs → ConversationService.cs
) else (
    echo ⚠️ File ChatService.cs không tồn tại hoặc đã đổi tên
)

echo.
echo ========================================
echo QUAN TRỌNG: CẦN SỬA THỦ CÔNG
echo ========================================
echo.
echo Mở các files sau trong VS Code và sửa:
echo.
echo 1. Controllers\ConversationsController.cs
echo    - Đổi class name: ChatsController → ConversationsController
echo    - Đổi service: IChatService → IConversationService
echo    - Đổi: _chatService → _conversationService
echo    - Đổi: ChatDto → ConversationDto
echo    - Đổi: StartChatDto → StartConversationDto
echo.
echo 2. Services\ConversationService.cs
echo    - Đổi interface: IChatService → IConversationService
echo    - Đổi class: ChatService → ConversationService
echo    - Đổi: _context.Chats → _context.Conversations
echo    - Đổi: new Chat() → new Conversation()
echo    - Đổi: chat.HostId/GuestId → conversation.User1Id/User2Id
echo.
echo 3. Program.cs
echo    - Tìm: builder.Services.AddScoped^<IChatService, ChatService^>^(^);
echo    - Đổi: builder.Services.AddScoped^<IConversationService, ConversationService^>^(^);
echo.
echo 4. DTOs (nếu có)
echo    - ChatDto.cs → ConversationDto.cs
echo    - StartChatDto.cs → StartConversationDto.cs
echo.
pause

REM Step 2: Build
echo.
echo [2/5] Building project...
dotnet build
if errorlevel 1 (
    echo ❌ Build failed! Kiểm tra lỗi compile
    pause
    exit /b 1
)
echo ✅ Build successful

REM Step 3: Clean migrations
echo.
echo [3/5] Xóa migrations cũ...
if exist Migrations (
    rmdir /s /q Migrations
    echo ✅ Đã xóa migrations cũ
) else (
    echo ⚠️ Folder Migrations không tồn tại
)

REM Step 4: Create new migration
echo.
echo [4/5] Tạo migration mới...
dotnet ef migrations add SyncWithWebHS_Conversations
if errorlevel 1 (
    echo ❌ Migration creation failed!
    pause
    exit /b 1
)
echo ✅ Migration created

REM Step 5: Info
echo.
echo [5/5] HOÀN THÀNH PHẦN TỰ ĐỘNG
echo.
echo ========================================
echo BƯỚC TIẾP THEO
echo ========================================
echo.
echo 1. Mở SQL Server Management Studio
echo 2. Backup database WebHSDb
echo 3. Kiểm tra migration vừa tạo trong folder Migrations\
echo 4. Chạy: dotnet ef database update
echo 5. Test: dotnet run
echo.
pause
