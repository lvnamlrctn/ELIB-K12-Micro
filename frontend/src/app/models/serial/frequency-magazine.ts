// Tần suất xuất bản — nguồn: ELIB Entities.PrintBook.Magazine.FrequencyMagazine
export interface FrequencyMagazine {
  id:           number;
  name:         string;
  dv?:          number;   // đơn vị thời gian (1=ngày, 2=tuần, 3=tháng, 4=năm…)
  soTrenDV?:    number;   // số kỳ trên mỗi đơn vị
  dvTrenSo?:    number;   // số đơn vị trên mỗi kỳ
  ngayPhatHanh?: string;  // mô tả lịch phát hành
  order?:       number;
  publicId?:    string;
  tenantName?:  string;
}
