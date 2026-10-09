export interface MetaDataFieldRegistery {
  metaDataFieldId: number;
  metaDataSchemaId?: number | null;
  field?: string | null;
  subfield?: string | null;
  descriptionVn?: string | null;
  descriptionEn?: string | null;
  sortOrder?: number | null;
  status?: number | null;
  input?: string | null;
  exportField?: number | null;
}
