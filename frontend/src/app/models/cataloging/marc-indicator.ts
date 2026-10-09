// Từ điển chỉ thị MARC — nguồn: Entities/PrintBook/MarcIndicator.cs
export interface MarcIndicatorDic {
  id:             number;
  indicator?:     string; // vị trí chỉ thị, "1" hoặc "2"
  value?:         string; // giá trị chỉ thị, vd "0", "1", "#"
  description?:   string;
  vndescription?: string;
  field_Id?:      string; // mã trường MARC, vd "082" (không phải khoá số)
  publicId?:      string;
}
