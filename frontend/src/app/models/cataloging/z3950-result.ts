// Kết quả tra cứu từ server Z39.50 — nguồn ELIB: Entities/PrintBook/Opac/Z3950Result + Z3950SearchImport
export interface Z3950ResultItem {
  id:           string;   // định danh tạm của kết quả (resultId/position) để xem MARC & nhập
  configId?:    number;   // server Z3950 nguồn
  configName?:  string;
  title?:       string;
  author?:      string;
  publisher?:   string;
  publishDate?: string;
  isbn?:        string;
  issn?:        string;
  language?:    string;
  pages?:       string;
  marc?:        string;   // bản MARC dạng văn bản (khi xem chi tiết)
}
