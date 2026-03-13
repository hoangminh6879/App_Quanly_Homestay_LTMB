// 🔧 API Configuration - CẬP NHẬT URL HÀNG NGÀY
// File: lib/config/api_config.dart

class ApiConfig {
  // ⚠️ QUAN TRỌNG: Cập nhật URL này MỖI NGÀY từ Conveyor
  // Xem file: D:\Nhom1\current_conveyor_url.txt để lấy URL mới
  
  // URL hiện tại (15/10/2025)
  static const String conveyorUrl = 'https://fastbrassbag1.conveyor.cloud';
  
  // URL local (dùng khi test trên máy tính)
  static const String localUrl = 'https://localhost:7097';
  
  // Chọn URL sử dụng
  // true = dùng Conveyor (điện thoại), false = dùng localhost (emulator/máy tính)
  static const bool useConveyor = true;
  
  // URL cuối cùng được sử dụng
  static String get baseUrl => useConveyor ? conveyorUrl : localUrl;
  
  // Thông tin debug
  static void printConfig() {
    print('=================================');
    print('API Configuration');
    print('=================================');
    print('Mode: ${useConveyor ? "CONVEYOR (Remote)" : "LOCAL"}');
    print('Base URL: $baseUrl');
    print('Conveyor URL: $conveyorUrl');
    print('Local URL: $localUrl');
    print('=================================');
  }
  
  // Endpoints
  static const String authLogin = '/api/auth/login';
  static const String authRegister = '/api/auth/register';
  static const String authLogout = '/api/auth/logout';
  static const String authRefresh = '/api/auth/refresh-token';
  
  static const String homestays = '/api/homestays';
  static String homestayDetails(int id) => '/api/homestays/$id';
  static const String myHomestays = '/api/homestays/my-homestays';
  
  static const String bookings = '/api/bookings';
  static String bookingDetails(int id) => '/api/bookings/$id';
  static const String myBookings = '/api/bookings/my-bookings';
  static const String hostBookings = '/api/bookings/host-bookings';
  static String cancelBooking(int id) => '/api/bookings/$id/cancel';
  static String reviewBooking(int id) => '/api/bookings/$id/review';
  static const String checkAvailability = '/api/bookings/check-availability';
  
  // Helper: Full URL
  static String fullUrl(String endpoint) => '$baseUrl$endpoint';
}

// =====================================
// HƯỚNG DẪN CẬP NHẬT URL MỖI NGÀY:
// =====================================
//
// 1. Chạy server trong Visual Studio với Conveyor
// 2. Copy URL từ Conveyor (ví dụ: https://xyz123.conveyor.cloud)
// 3. Mở file này
// 4. Thay đổi giá trị conveyorUrl ở dòng 9
// 5. Save file
// 6. Hot reload Flutter app (nhấn 'r' trong terminal)
// 7. Hoặc restart app (nhấn 'R' trong terminal)
//
// =====================================

/*
USAGE EXAMPLE:

import 'config/api_config.dart';

void main() {
  // Print current configuration
  ApiConfig.printConfig();
  
  runApp(MyApp());
}

// In ApiService:
class ApiService {
  final String baseUrl = ApiConfig.baseUrl;
  
  Future<void> login(String email, String password) async {
    final url = ApiConfig.fullUrl(ApiConfig.authLogin);
    // ... rest of code
  }
}

// Quick switch between local and remote:
// Change: static const bool useConveyor = true/false;
// Then hot reload!
*/
