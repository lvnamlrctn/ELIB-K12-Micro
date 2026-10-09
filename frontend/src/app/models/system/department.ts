export interface Department {
  id: number;
  publicId?: string;
  parentId: number | null;
  name: string;
  code?: string | null;
  order?: number;
  status?: number;
  description?: string | null;
  children?: Department[];
  hasChildren?: boolean;
  childCount?: number;
  expanded?: boolean;
  depth?: number;
}

export interface DepartmentFlatNode extends Department {
  visible: boolean;
  depth: number;
}
