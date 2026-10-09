// Phiếu nhập tin (worksheet MARC) — nguồn: Entities/PrintBook/BibWorksheet.cs
export interface WorkSheet {
  id:          number;
  name?:       string;
  usmarc?:     string;
  bib_Type_Id?: number;
  publicId?:   string;
  tenantName?: string;
}

// nguồn: Entities/PrintBook/WorksheetField.cs
export interface WorkSheetField {
  id:                number;
  bib_Worksheet_Id?: number;
  field?:            string;   // mã trường MARC, vd "245"
  l1?:               string;   // chỉ thị 1
  l2?:               string;   // chỉ thị 2
  publicId?:         string;
}

// nguồn: Entities/PrintBook/WorksheetSubfield.cs
export interface WorkSheetSubField {
  id:                  number;
  worksheet_Field_Id?: number;
  subfield?:           string; // vd "a"
  value?:              string; // giá trị mặc định
  publicId?:           string;
}
