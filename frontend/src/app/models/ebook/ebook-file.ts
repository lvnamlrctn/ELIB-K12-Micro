export interface EbookFile {
  id:                 number;
  publicId?:          string;
  ebookId?:           number;
  url?:               string | null;
  type?:              string | null;
  isConvert?:         number | null;
  createdDate?:       string | null;
  fileType?:          string | null;
  fileSize?:          number | null;
  fileExt?:           string | null;
  description?:       string | null;
  source?:            string | null;
  formatId?:          number | null;
  checkSumAlgorithm?: string | null;
  sortOrder?:         number | null;
  isDelete?:          number | null;
  createdRowBy?:      number | null;
  updateRowBy?:       number | null;
  createdRowDate?:    string | null;
  updatedRowDate?:    string | null;
  tenantId?:          string | null;
}
