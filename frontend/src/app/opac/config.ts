// LƯU Ý: object mutable — TenantId được TenantService gán lại lúc khởi động (resolve theo subdomain).
// Giá trị dưới đây chỉ là FALLBACK khi chạy localhost/IP/SSR hoặc resolve lỗi.
export const APP_CONFIG = {
  TenantId: '552E1DBE-DA4E-42D5-9067-EF51E47A98A6', // Tenant (đơn vị/thư viện) — value for the target APIs
  BackendBase: 'http://localhost:5101',
  BackendDataBase: 'http://103.97.134.58:8089'
};

// Chatbot AI (RAG: embedding + kNN + Gemini) — chạy chung backend. Trình duyệt gọi đường dẫn tương đối (qua proxy /api
// như mọi API khác); trước đây để cứng 'http://localhost:5101' nên trên máy bạn đọc chat luôn lỗi (port ELIB-LRC 09-29).
export const CHATBOT_API_BASE = '';
