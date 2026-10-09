export interface EbookSubject {
  id: number;
  publicId?: string;
  tenantName?: string;
  parentId: number | null;
  name: string;
  status: number | boolean;
  order?: number;
  link?: string;
  description?: string;
  pageTitle?: string;
  metaDescription?: string;
  keyword?: string;
  language?: string;
  children?: EbookSubject[];
  hasChildren?: boolean;
  childCount?: number;
  expanded?: boolean;
  depth?: number;
}

export interface EbookSubjectFlatNode extends EbookSubject {
  visible: boolean;
  depth: number;
}
