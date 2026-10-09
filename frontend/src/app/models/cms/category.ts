export interface Category {
  id: number;
  publicId?: string;
  tenantName?: string;
  parentId: number | null;
  name: string;
  status: number | boolean;
  pageTitle?: string;
  keyword?: string;
  metaDescription?: string;
  order?: number;
  link?: string;
  description?: string;
  language?: string;
  level?: number;
  children?: Category[];
  hasChildren?: boolean;
  childCount?: number;
  expanded?: boolean;
  depth?: number;
}

export interface CategoryFlatNode extends Category {
  visible: boolean;
  depth: number;
}
