import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from './services/auth.service';

// JWT của BẠN ĐỌC (khác token nhân viên gắn bởi services/auth.interceptor.ts ở gốc app) — chỉ gắn
// cho các endpoint OPAC yêu cầu đăng nhập bạn đọc, tránh gửi thừa token cho mọi request công khai khác.
// Lưu ý: '/api/public/RoomBooking/' bị thiếu ở đây từ Đợt 6 (RoomBookingOpacService tự nhận chú thích
// "gọi kèm Bearer token bạn đọc" nhưng interceptor này chưa từng gắn — mọi lượt Book/MyBookings/CheckIn/
// Cancel qua UI thật đều thiếu Authorization header) — vá cùng lúc với Đợt 9 vì cùng 1 danh sách, cùng
// loại lỗi (thêm domain OPAC mới mà quên cập nhật interceptor).
// SỬA 2026-09-20 (phát hiện qua test UI trực tiếp): '/api/public/ReaderWorkspace/' (có dấu / cuối) chỉ
// khớp các endpoint con như .../Searches, KHÔNG khớp chính endpoint gốc `GET/PUT /api/public/ReaderWorkspace`
// (ResearchWorkspaceOpacService.get()/save() gọi thẳng baseUrl, không có path con) — khiến tab "Không gian
// nghiên cứu" luôn nhận 401 một cách âm thầm (get() có catchError nuốt lỗi, trả về snapshot rỗng mặc định).
// Bỏ dấu / cuối để so khớp cả endpoint gốc lẫn các endpoint con (RoomBooking/MyLibrary/DocumentSubmission
// không bị ảnh hưởng vì các service đó luôn gọi kèm path con, không bao giờ gọi bare baseUrl).
const READER_AUTH_PATHS = [
  '/api/public/MyLibrary/',
  '/api/public/DocumentSubmission/',
  '/api/public/RoomBooking/',
  '/api/public/ReaderWorkspace',
  // Đợt 22.6 — thẻ bạn đọc điện tử (reader-card.ts gọi PublicReaderController.GetProfile, [Authorize]).
  '/api/public/PublicReader/Profile',
  // Thanh toán phí/phạt tự phục vụ (port ELIB-LRC 09-25) — PaymentReaderController [Authorize].
  '/api/public/PaymentReader/',
  // Trợ lý tìm tài liệu: có token bạn đọc thì gợi ý theo hồ sơ chủ đề/môn học (backend vẫn chạy khi không có token).
  '/api/public/chat/find-documents',
];

export const opacAuthInterceptor: HttpInterceptorFn = (req, next) => {
  if (!READER_AUTH_PATHS.some(p => req.url.includes(p))) return next(req);

  const token = inject(AuthService).currentUser()?.token;
  if (!token) return next(req);

  return next(req.clone({ headers: req.headers.set('Authorization', `Bearer ${token}`) }));
};
