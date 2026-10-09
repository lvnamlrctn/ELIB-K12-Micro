// Đơn đặt bổ sung — nguồn: Entities/PrintBook/Catalogue/Order.cs
// Lưu ý casing: backend dùng System.Text.Json CamelCase mặc định — chỉ hạ chữ cái ĐẦU tiên, không bỏ
// gạch dưới/không đổi thứ tự từ. Các cột gốc DB có gạch dưới/toàn hoa (Order_Name, Date_Order,
// Supplier_Id, BUDGET_ID, CREATED_BY) serialize thành "order_Name"/"date_Order"/"supplier_Id"/
// "budgeT_ID"/"createD_BY" (đã kiểm chứng qua API thật) — KHÔNG đổi lại các tên field dưới đây theo
// trực giác (giống lưu ý tương tự ở models/cataloging/receipt.ts).
export interface AbOrder {
  id:               number;
  code?:            number;
  order_Name?:      string;
  date_Order?:      string;
  duedate?:         string;
  supplier_Id?:     number;
  fundId?:          number;
  budgeT_ID?:       number;
  source_Id?:       number;
  paymentMethodId?: number;
  payment_status?:  number;
  status?:          number;
  note?:            string;
  createD_BY?:      number;
  createdDate?:     string;
  lines?:           AbOrderLine[];
  publicId?:        string;
  tenantName?:      string;
}

export interface AbOrderLine {
  id:              number;
  order_Id:        number;
  orderCode?:      number;  // chỉ có khi lấy từ LookupReceivableLines (tra cứu đa đơn)
  bibid?:          number;
  mfn?:            number;   // MFN của biểu ghi MARC — dùng để sửa lại biểu ghi
  title?:          string;
  author?:         string;
  publisher?:      string;
  publishDate?:    string;
  amount?:         number;   // số lượng đặt
  currency?:       string;
  price?:          number;   // đơn giá
  rate?:           number;
  receivedAmount?: number;   // đã nhận qua đơn nhận (Order_Id trùng)
  remainingAmount?: number;  // còn lại chưa nhận
  publicId?:       string;
}
