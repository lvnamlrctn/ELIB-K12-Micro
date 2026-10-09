// Lưu thông / mượn sách — nguồn: Bussiness/PrintBook/Circulation/Loan/BorrowBook.cs
export interface Borrow {
  id:          number;
  publicId?:   string;   // Đợt 16 — PublicId của BookOut gốc, dùng cho Lịch sử thay đổi theo hồ sơ
  readerId?:   number;
  cardNo?:     string;
  readerName?: string;
  barcode?:    string;
  bibTitle?:   string;
  callNumber?: string;
  borrowDate?: string;
  dueDate?:    string;
  returnDate?: string;
  renewCount?: number;
  location?:   string;     // vị trí kho (BookOut.Store → Store.Name)
  status?:     number;     // 1=Đang mượn, 2=Đã trả, 3=Quá hạn, 4=Mất
  statusName?: string;
  staffName?:  string;     // người thực hiện: cán bộ nhận trả, chưa trả thì cán bộ cho mượn
  fineValue?:  number;
  ngayQH?:     number;     // số ngày quá hạn
  circPlaceId?: number;
  note?:       string;
}

/** Thông tin độc giả + sách đang mượn cho bàn lưu thông. */
export interface BorrowReaderSnapshot {
  readerId?:    number;
  readerPublicId?: string;
  cardNo?:      string;
  fullName?:    string;
  readerType?:  string;
  className?:   string;
  courseName?:  string;
  orgName?:     string;
  citizenId?:   string;   // số căn cước công dân
  expireDate?:  string;
  issueDate?:   string;
  photo?:       string;
  balance?:     number;
  status?:      number;     // trạng thái bạn đọc (khóa/thường) từ Reader.Status
  avatar?:      string;
  currentLoans: Borrow[];
  maxItems?:    number;
  borrowedCount?: number;
  isLocked?:    boolean;
  isExpired?:   boolean;
  hasOverdue?:  boolean;
  canBorrow?:   boolean;
  blockReason?: string;
}
