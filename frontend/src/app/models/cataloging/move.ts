// Điều chuyển kho — nguồn: Entities/PrintBook/Catalogue/Move.cs
export interface AbMove {
  id:               number;
  code?:            number;
  // Đặt tên khớp chính xác với key JSON thực tế trả về từ backend (JsonNamingPolicy.CamelCase chỉ hạ
  // chữ cái đầu của "StoreDeliver_Id"/"StoreReceipt_Id", không đụng tới dấu "_" hay phần còn lại).
  storeDeliver_Id?: number;
  storeReceipt_Id?: number;
  delivererName?:   string;
  delivererAddress?: string;
  receiptName?:     string;
  receiptAddress?:  string;
  delivererDate?:   string;
  status?:          number;   // 2 = đã hoàn thành (ĐKCB đã chuyển sang kho nhận, phiếu khoá) — AbMoveStatus
  receiptDate?:     string;
  note?:            string;
  createdBy?:       number;
  publicId?:        string;
  tenantName?:      string;
}

export const AbMoveStatus = { Completed: 2 } as const;

// Kết quả "Hoàn thành điều chuyển": số ĐKCB đã chuyển + các bản bị bỏ qua kèm lý do
export interface AbMoveCompleteResult { moved: number; skipped: { barcode?: string; reason: string }[]; }
