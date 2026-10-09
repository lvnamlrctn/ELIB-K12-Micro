// Vào/ra thư viện — nguồn ELIB: Bussiness.Common.Reader (ReaderCheckIn/Out, SearchReaderCheckIn)
export interface CheckLog {
  id:            number;
  readerId?:     number;
  cardNo?:       string;
  fullName?:     string;
  readerType?:   string;
  className?:    string;
  departmentName?: string;
  checkInTime?:  string;
  checkOutTime?: string;
  checkType?:    number;   // 1 vào · 2 ra (ELIB CheckType)
  storeId?:      number;
  circPlaceId?:  number;
  publicId?:     string;
  tenantName?:   string;
}

// Tài liệu đang mượn của bạn đọc (PrintBook.BookOut), dùng chung ở các trang tra cứu theo thẻ
export interface CurrentLoanItem {
  id:          number;
  barcode?:    string;
  bibTitle?:   string;
  borrowDate?: string;
  dueDate?:    string;
  status?:     string;
  renewCount?: number;
  fineValue?:  number;
  location?:   string;
}

// Trạng thái thẻ hiện tại (CheckReaderCheckIn) — phục vụ quyết định vào hay ra
export interface CheckStatus {
  readerId?:    number;
  cardNo?:      string;
  fullName?:    string;
  readerType?:  string;
  className?:   string;
  avatar?:      string;
  balance?:     number;
  status?:      number;     // trạng thái bạn đọc (khóa/thường) từ Reader.Status
  issueDate?:   string;
  expireDate?:  string;
  checkedIn:    boolean;   // đang ở trong thư viện?
  checkInId?:   number;    // bản ghi vào hiện tại (để check-out)
  checkInTime?: string;
  checkInCount?: number;   // tổng số lượt vào-ra (PrintBook.CheckOut)
  currentLoans: CurrentLoanItem[];
  found:        boolean;   // có tồn tại thẻ này không
}
