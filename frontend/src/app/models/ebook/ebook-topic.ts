export interface EbookTopic {
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
  children?: EbookTopic[];
  hasChildren?: boolean;
  childCount?: number;
  expanded?: boolean;
  depth?: number;
}

export interface EbookTopicFlatNode extends EbookTopic {
  visible: boolean;
  depth: number;
}
