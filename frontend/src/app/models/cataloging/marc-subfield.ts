// Từ điển trường con MARC — nguồn: Entities/PrintBook/MarcSubField.cs
export interface MarcSubFieldDic {
  id:             number;
  field?:         string; // mã trường MARC, vd "082"
  subfield?:      string; // mã trường con, vd "a"
  description?:   string;
  vndescription?: string;
  repeatable?:    number;
  mandatory?:     number;
  publicId?:      string;
}
