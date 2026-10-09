// Loại tài liệu Marc — nguồn: PrintBook.MarcType
export interface MarcType {
  id:             number;
  code?:          string;
  vnDescription?: string;
  description?:   string;
  publicId?:      string;
}
