// Sách mất — nguồn ELIB: Book.ProcessLossBook / SearchLostBook
// Các field khớp trực tiếp với Entity LostBook.cs (GenericController trả entity thô, không enrich)
export interface LostBook {
  id?:          number;
  barcode:      string;
  bibTitle?:    string;
  store?:       number;   // Id kho
  storeName?:   string;
  submited?:    string;   // ngày báo mất
  reason?:      string;
  createdBy?:   number;
  createdByName?: string;
  publicId?:    string;
  tenantName?:  string;
}

// Kết quả tra cứu theo Đăng ký cá biệt — dùng để tự động điền form Báo mất/Khôi phục mất
export interface LostBookLookup {
  barcode:      string;
  mfn?:         number | null;
  storeId?:     number | null;
  storeName?:   string | null;
  status?:      string | null;
  statusName?:  string | null;
  isbd?:        string | null;
}
