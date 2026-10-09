// Phiên kiểm kê kho — nguồn ELIB: Entities.PrintBook.Store.Inventory + DataAccess Inventory
// status: 1 đang kiểm kê (open) · 2 đã kết thúc (closed)
export interface Inventory {
  id:            number;
  inventoryName: string;
  inventoryDate: string;
  status?:       number;
  userId?:       number;
  publicId?:     string;
  tenantName?:   string;
}

// Một mã vạch (ĐKCB) đã quét trong phiên kiểm kê
export interface InventoryBarcode {
  id?:               number;
  inventoryId:       number;
  barcode:           string;
  storeId?:          number;
  storeName?:        string;
  bibTitle?:         string;
  // cờ đối chiếu, mã theo dữ liệu ELIB cũ (xem InventoryFlag): 1 = bình thường · 2 = có vấn đề
  checkStoreStatus?: number;  // 1 đúng kho · 2 sai kho
  checkBorrow?:      number;  // 1 không mượn · 2 còn phiếu mượn mở
  checkStatus?:      number;  // 1 bình thường
  checkRegisteter?:  number;  // 1 có trong CSDL · 2 chưa đăng ký
  scanTime?:         string;
}

// Thống kê / báo cáo phiên kiểm kê
export const InventoryFlag = { Yes: 1, No: 2 } as const;
export interface InventorySummary {
  inventoryId:      number;
  scanned:          number;  // tổng đã quét
  notRegister:      number;  // CountBookNotRegister — mã chưa có trong CSDL
  notCorrectStore:  number;  // CountBookNotCorrecStore — quét sai kho
  lost:             number;  // ProcessLostBook — có trong kho nhưng không quét được
}
