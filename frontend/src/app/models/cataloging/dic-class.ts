// Khung phân loại (DDC...) — nguồn: Entities/PrintBook/Catalogue/Dic/DClass.cs
export interface DicClass {
  id:             number;
  parentId?:      number;
  code?:          string;
  description?:   string;
  vnDescription?: string;
  type?:          string;
  // tree helpers
  children?:      DicClass[];
  depth?:         number;
  expanded?:      boolean;
  publicId?:      string;
  tenantName?:    string;
}
