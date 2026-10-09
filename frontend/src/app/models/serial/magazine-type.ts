// Loại ấn phẩm định kỳ — thiết kế ILS (nhóm phân loại đầu báo/tạp chí)
// Nguồn ELIB: Entities.Magazine.MagaginzeType (Id, Name) — mở rộng theo chuẩn ILS
export interface MagazineType {
  id:              number;
  name:            string;
  code?:           string;   // mã loại
  magazineTypeId?: number;   // loại cha (phân cấp)
  images?:         string;   // ảnh đại diện
  publicId?:       string;
  tenantName?:     string;
}
