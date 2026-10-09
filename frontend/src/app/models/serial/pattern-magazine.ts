// Mẫu đánh số kỳ — nguồn: ELIB Entities.PrintBook.Magazine.PatternMagazine(+Detail)
export interface PatternMagazineDetail {
  id?:       number;
  patternId?: number;
  x?:        string;   // nhãn cấp X (vd: "Tập")
  y?:        string;   // nhãn cấp Y (vd: "Số")
  z?:        string;   // nhãn cấp Z
  stepX?:    number;   stepY?: number;   stepZ?: number;   // bước tăng
  repeatX?:  number;   repeatY?: number; repeatZ?: number; // số lần lặp
  maxX?:     number;   maxY?: number;    maxZ?: number;    // giá trị tối đa
  resetX?:   number;   resetY?: number;  resetZ?: number;  // mốc reset
}

export interface PatternMagazine {
  id:          number;
  name:        string;
  description?: string;
  function?:   string;   // biểu thức sinh số kỳ
  order?:      number;
  detail?:     PatternMagazineDetail;
  publicId?:   string;
  tenantName?: string;
}
