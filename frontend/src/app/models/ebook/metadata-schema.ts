export interface MetadataSchemaRegistry {
  metadataSchemaId: number;
  publicId?: string;
  tenantName?: string;
  nameSpace?: string | null;
  shortId?: string | null;
  descriptionVn?: string | null;
}
