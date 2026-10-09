export interface EbookCollection {
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
  children?: EbookCollection[];
  hasChildren?: boolean;
  childCount?: number;
  expanded?: boolean;
  depth?: number;
}

export interface EbookCollectionFlatNode extends EbookCollection {
  visible: boolean;
  depth: number;
}
