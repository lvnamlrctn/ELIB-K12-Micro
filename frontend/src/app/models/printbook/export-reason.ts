// Lý do xuất kho — nguồn: Entities/PrintBook/DExportReason.cs
export interface ExportReason {
  id:        number;
  name?:     string;
  publicId?: string;
  tenantName?: string;
}
