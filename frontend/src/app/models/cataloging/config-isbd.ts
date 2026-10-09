// Cấu hình dấu câu ISBD — nguồn: Entities/PrintBook/ConfigIsbd.cs + IsbdField.cs + IsbdSubfield.cs
export interface ConfigIsbd {
  id:          number;
  bib_Type_Id?: number;
  publicId?:   string;
}

// 1 quy tắc dấu câu cho 1 field MARC trong 1 cấu hình ISBD.
export interface IsbdFieldRule {
  id:          number;
  config_Id?:  number;
  field?:      string;
  starttp?:    string; // dấu câu trước field
  stoptp?:     string; // dấu câu sau field
  fieldindex?: number; // thứ tự field trong chuỗi ISBD
  publicId?:   string;
}

// 1 quy tắc dấu câu cho 1 subfield MARC trong 1 field, thuộc 1 cấu hình ISBD.
export interface IsbdSubfieldRule {
  id:             number;
  config_Id?:     number;
  field?:         string;
  subfield?:      string;
  starttp?:       string; // dấu câu trước subfield
  stoptp?:        string; // dấu câu sau subfield
  nexttp?:        string; // dấu câu nối giữa các lần lặp của cùng 1 subfield
  subfieldindex?: number; // thứ tự subfield trong field
  publicId?:      string;
}
