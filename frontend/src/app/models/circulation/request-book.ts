// Yêu cầu mượn (đăng ký mượn trước) — nguồn: Bussiness BorrowBook (SearchRequestBook/BorrowByRequest)
export interface RequestBook {
  id:          number;
  readerId?:   number;
  cardNo?:     string;
  readerName?: string;
  barcode?:    string;
  bibTitle?:   string;
  author?:     string;
  publisher?:  string;
  requestDate?: string;
  status?:     number;     // 0=Chờ duyệt, 1=Đã cho mượn, 2=Từ chối
  statusName?: string;
  tenantName?: string;
}
