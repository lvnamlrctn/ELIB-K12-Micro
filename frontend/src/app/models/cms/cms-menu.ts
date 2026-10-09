export interface CmsMenu {
  id: number;
  publicId?: string;
  tenantName?: string;
  parentId?: number | null;
  name?: string;
  menuType?: number | null;
  link?: string | null;
  friendUrl?: string | null;
  sortOrder?: number | null;
  status?: number | null;
  openType?: string | null;
  linkType?: string | null;
  subId?: string | null;
  isLogIn?: number | null;
  icon?: string | null;
  children?: CmsMenu[];
  hasChildren?: boolean;
  childCount?: number;
  expanded?: boolean;
  depth?: number;
}

export interface CmsMenuFlatNode extends CmsMenu {
  visible: boolean;
  depth: number;
}
