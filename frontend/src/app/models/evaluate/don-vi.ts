export interface DonViNode {
  id: number;
  publicId?: string;
  tenantName?: string;
  parentId: number | null;
  name: string;
  order?: number;
  status?: number;
  children?: DonViNode[];
  hasChildren?: boolean;
  childCount?: number;
  expanded?: boolean;
  depth?: number;
}

export interface DonViFlatNode extends DonViNode {
  visible: boolean;
  depth: number;
}
