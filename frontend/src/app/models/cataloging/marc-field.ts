// Từ điển trường MARC — nguồn: Entities/PrintBook/MarcField.cs
export interface MarcFieldDic {
  id:             number;
  field?:         string; // mã trường, vd "245"
  description?:   string;
  vndescription?: string;
  repeatable?:    number;
  mandatory?:     number;
  publicId?:      string;
}
