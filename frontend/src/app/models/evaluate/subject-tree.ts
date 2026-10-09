export interface SubjectTreeNode {
  nodeType: 'donvi' | 'program' | 'nganh' | 'monhoc' | 'tailieu';
  id: number;
  publicId: string;
  name?: string | null;
  code?: string | null;
  soTinChi?: number | null;
  linkPublicId?: string | null;
  author?: string | null;
  publishDate?: string | null;
  loaiTaiLieu?: number | null;
  tenantId?: number | null;
  tenantName?: string | null;
  children?: SubjectTreeNode[];
}

export interface SubjectTreeFlatNode extends SubjectTreeNode {
  depth: number;
  visible: boolean;
  expanded: boolean;
}
