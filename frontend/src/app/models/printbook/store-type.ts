export interface StoreType {
  id:          number;
  publicId?:   string;
  name?:       string;
  parentId?:   number | null;
  children?:   StoreType[];
  hasChildren?: boolean;
  childCount?:  number;
  expanded?:    boolean;
  depth?:       number;
  tenantId?:    number | null;
  tenantName?:  string | null;
}

export interface StoreTypeFlatNode extends StoreType {
  visible: boolean;
  depth:   number;
}
