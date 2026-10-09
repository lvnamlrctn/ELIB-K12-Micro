// Chatbot AI (RAG) — nguồn: POST /api/public/chat/ask
export interface ChatSource {
  ebookId:      string;
  ebookFileId?: string;
  title:        string;
  author?:      string;
  pageNumber?:  number;
  excerpt?:     string;
}

/** 1 tài liệu do trợ lý TÌM TÀI LIỆU trả về (POST /api/public/chat/find-documents, DocumentFinderItem). */
export interface FoundDocument {
  docType:         'print' | 'digital' | string;
  publicId?:       string;
  title?:          string;
  author?:         string;
  publisher?:      string;
  publishYear?:    number;
  images?:         string;
  free?:           boolean;
  /** Tài liệu in: số bản / số bản sẵn sàng (đếm thời gian thực). */
  copyCount?:      number;
  availableCount?: number;
}

export interface ChatMessage {
  id:         string;
  role:       'user' | 'assistant';
  text:       string;
  sources?:   ChatSource[];
  /** Kết quả trợ lý tìm tài liệu: thẻ tài liệu bấm được + tổng số khớp. */
  documents?: FoundDocument[];
  total?:     number;
  isError?:   boolean;
  timestamp:  number;
}
