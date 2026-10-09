// Đóng tập báo tạp chí — nguồn cẩm nang mục VII.5: tìm các số tạp chí đã nhận
// về thư viện, đóng thành tập và đăng ký cá biệt cho tập được đóng.
export interface SerialBindingItem {
  issueId:        number;   // liên kết tới SerialIssue.id
  serialSeq?:      string;   // nhãn kỳ (cache hiển thị, khỏi phải load lại)
  publishedDate?: string;
}

export interface SerialBinding {
  id:               number;
  accessionNo?:     string;   // Số ĐKCB của tập
  storeId?:         number;   storeName?: string;
  subscriptionId?:  number;   subscriptionTitle?: string;
  volumeTitle?:     string;   // nhan đề đóng tập (nếu khác nhan đề tạp chí)
  bindingDate?:     string;
  callNumber?:      string;
  note?:            string;
  items?:           SerialBindingItem[];
  itemCount?:       number;   // chỉ đọc, backend trả kèm
  publicId?:        string;
  tenantName?:      string;
}
