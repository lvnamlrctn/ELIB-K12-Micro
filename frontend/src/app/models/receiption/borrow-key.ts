// Mượn chìa khóa / tủ đựng đồ — nguồn ELIB: BorrowKey (BorrowingKey/ReturnKey/SearchKeyBorrowByCardNo)
export interface BorrowKey {
  id:            number;
  readerId?:     number;
  cardNo?:       string;
  fullName?:     string;
  readerType?:   string;
  cabinetId?:    number;
  cabinetName?:  string;
  cabinetBarcode?: string;
  circPlaceId?:  number;
  borrowDate?:   string;
  returnDate?:   string;
  status?:       number;   // 1 đang mượn · 2 đã trả
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

// Thông tin bạn đọc + danh sách khóa đang mượn (SearchKeyBorrowByCardNo)
export interface KeyHolderSnapshot {
  readerId:     number;
  cardNo:       string;
  fullName:     string;
  readerType?:  string;
  className?:   string;
  avatar?:      string;
  balance?:     number;
  status?:      number;     // trạng thái bạn đọc (khóa/thường) từ Reader.Status
  issueDate?:   string;
  expireDate?:  string;
  currentKeys:  BorrowKey[];
  currentLoans: CurrentLoanItem[];
}
