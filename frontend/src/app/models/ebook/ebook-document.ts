export interface ItemXml {
  id?:          number;
  title?:       string | null;
  author?:      string | null;
  publisher?:   string | null;
  publishDate?: string | null;
  keyword?:     string | null;
  page?:        string | null;
  otherTitle?:  string | null;
  oldAuthor?:   string | null;
  publicId?:    string | null;
}

export interface EbookDocumentAuthor {
  lastName?:  string | null;
  firstName?: string | null;
}

export interface EbookDocumentAdvisor {
  lastName?:  string | null;
  firstName?: string | null;
  title?:     string | null;
}

export interface EbookDocumentVolume {
  type?:   string | null;
  number?: string | null;
}

export interface EbookDocument {
  id:              number | string;
  publicId?:       string;
  tenantName?:     string | null;

  // List-view fields (from search API)
  images?:         string | null;
  totalView?:      number | null;
  totalDownload?:  number | null;
  status?:         number | null;
  show?:           number | null;
  allowDownload?:  number | boolean | null;
  free?:           number | null;
  share?:          number | null;
  collectionId?:   number | null;
  subjectId?:      number | null;
  topicId?:        number | null;
  typeId?:         number | null;
  submited?:       string | null;
  lastUpdate?:     string | null;
  collectionName?: string | null;
  subjectName?:    string | null;
  topicName?:      string | null;
  totalFile?:      number | null;
  itemXml?:        ItemXml | null;

  // Detail/edit fields (from getById API)
  title?:          string | null;
  otherTitles?:    string[] | null;
  authors?:        EbookDocumentAuthor[] | null;
  publisher?:      string | null;
  publishDay?:     number | null;
  publishMonth?:   number | null;
  publishYear?:    number | null;
  pages?:          number | null;
  coverImage?:     string | null;
  docTypeId?:      number | null;
  docTypeName?:    string | null;
  languageId?:     number | null;
  languageName?:   string | null;
  keywords?:       string[] | null;
  abstract?:       string | null;
  description?:    string | null;
  isPublished?:    boolean;
  isFree?:         boolean;
  shareType?:      number | null;
  journalName?:    string | null;
  volumes?:        EbookDocumentVolume[] | null;
  reportPages?:    string[] | null;
  advisors?:       EbookDocumentAdvisor[] | null;
  fileCount?:      number | null;
  viewCount?:      number | null;
  printCopies?:    number | null;
  offlineDays?:    number | null;
  numbers?:        { type: string; value: string }[] | null;
  metaData?:       { id?: number; metaDataFieldId: number; value: string; sortOrder?: number | null }[] | null;
}
