/** File đính kèm tin tức (cms.AttachFile). url = tên object trong kho; "Upload/..." = file cũ chưa đồng bộ. */
export interface AttachFile {
  id:           number;
  publicId:     string;
  name:         string | null;
  url:          string | null;
  /** KB */
  fileSize:     number | null;
  newsId:       number | null;
  createdDate:  string | null;
}

export interface AttachFileSyncResult {
  synced: number;
  failed: number;
  total:  number;
  errors: string[];
}

/** Định dạng được phép đính kèm — khớp AttachFileService.AllowedExtensions ở backend. */
export const ATTACH_ALLOWED_EXTENSIONS = ['pdf', 'doc', 'docx', 'xls', 'xlsx', 'ppt', 'pptx', 'odt', 'ods', 'odp', 'txt', 'csv', 'rtf',
  'zip', 'rar', '7z', 'jpg', 'jpeg', 'png', 'gif', 'webp', 'mp3', 'mp4'];
export const ATTACH_MAX_BYTES = 50 * 1024 * 1024;
