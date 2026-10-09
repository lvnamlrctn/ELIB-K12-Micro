// Đơn vị nhận sách (khi xuất kho) — nguồn: Entities/PrintBook/DBookOutUnit.cs
export interface BookOutUnit {
  id:        number;
  name?:     string;
  publicId?: string;
  tenantName?: string;
}
