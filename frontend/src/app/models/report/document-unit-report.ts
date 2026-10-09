// Báo cáo tổng hợp số liệu tài liệu (giấy + số) theo đơn vị (Tenant)
export interface DocumentUnitReportRow {
  tenantId:     number;
  tenantName:   string;
  bibCount:     number; // số đầu tài liệu in (PrintBook.Bib)
  barcodeCount: number; // số bản in (PrintBook.Barcode)
  ebookCount:   number; // số tài liệu số (Ebook.Item)
}
