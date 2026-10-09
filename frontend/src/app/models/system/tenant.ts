// Đơn vị/thương hiệu (multi-tenant) — xem backend Tenant entity. Field portalId/language tồn tại ở
// backend nhưng không hiển thị trên form (không dùng trong thực tế, theo đúng UI gốc).
export interface Tenant {
  id?:        string;
  publicId?:  string;
  code:       string;
  name:       string;
  portalId?:  string | null;
  language?:  string | null;
  host?:      string | null;
  logoText?:  string | null;
  logoUrl?:   string | null;
}
