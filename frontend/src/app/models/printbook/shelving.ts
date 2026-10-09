// Bản cá biệt (số KCB) chưa xếp giá — nguồn: Entities/PrintBook/Store/*
export interface UnshelvedBarcode {
  id:          number;
  barcode?:    string;
  bibTitle?:   string;
  receiptCode?: string;
  storeId?:    number;
  storeName?:  string;
  tenantName?: string;
}

export interface ShelvingSearchParams {
  receiptCode?: string | null;
  barcodeFrom?: string | null;
  barcodeTo?:   string | null;
  storeId?:     number | null;
  pageIndex?:   number;
  pageSize?:    number;
  tenantId?:    string | null;
}
