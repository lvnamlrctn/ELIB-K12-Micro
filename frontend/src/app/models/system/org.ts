export interface OrgNode {
  id: number;
  publicId?: string;
  tenantName?: string;
  parentId: number | null;
  name: string;
  code?: string | null;
  order?: number;
  status?: number;
  description?: string | null;
  children?: OrgNode[];
  hasChildren?: boolean;
  childCount?: number;
  expanded?: boolean;
  depth?: number;
}

export interface OrgFlatNode extends OrgNode {
  visible: boolean;
  depth: number;
}
