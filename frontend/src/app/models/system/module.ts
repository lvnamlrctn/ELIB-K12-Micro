export interface AppModule {
  id: number;
  publicId?: string;
  parentId: number | null;
  name: string;
  moduleCode?: string;
  icon?: string;
  link?: string;
  sortOrder?: number;
  status: number | boolean;
  level?: number;
  children?: AppModule[];
  hasChildren?: boolean;
  childCount?: number;
  expanded?: boolean;
  depth?: number;
}

export interface AppModuleFlatNode extends AppModule {
  visible: boolean;
  depth: number;
}
