// Liên kết 1-1 giữa Tài liệu in (PrintBook.Bib) và Tài liệu số (Ebook.Item) — nguồn: PrintBook.PrintBookandDigital
export interface LinkedEbookInfo {
  id:           number;
  publicId?:    string;
  title?:       string;
  author?:      string;
  publisher?:   string;
  publishDate?: string;
}

export interface LinkedBibInfo {
  bibid:        number;
  publicId?:    string;
  mfn?:         number;
  title?:       string;
  author?:      string;
  publisher?:   string;
  publishDate?: string;
}
