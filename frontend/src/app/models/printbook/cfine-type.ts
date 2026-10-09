export interface CFineType {
  id:           number;
  code?:        string;
  name?:        string;
  /** Mã trạng thái ĐKCB (Barcode_Status.Id) gán cho tài liệu khi phạt với lý do này — API: `status_Reg_Id`. */
  statusRegId?: string;
  publicId?:    string;
  tenantName?:  string;
}

// API PrintBook/BarcodeStatus: Id là mã trạng thái ("R", "L"…), tên ở commentStatus.
export interface BarcodeStatus {
  id?:            string;
  publicId?:      string;
  commentStatus?: string;
  name?:          string;
  code?:          string;
}
