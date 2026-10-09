// Loại biểu ghi — nguồn: Entities/PrintBook/BibType.cs (tên cột giữ nguyên theo entity: Type, Bib_Level, Materal_Type, Record_Type_Code)
export interface BibType {
  id:                 number;
  name?:              string;
  code?:              string;
  type?:              string;
  bib_Level?:         string;
  materal_Type?:      string;
  record_Type_Code?:  string;
  publicId?:          string;
  tenantName?:        string;
}
