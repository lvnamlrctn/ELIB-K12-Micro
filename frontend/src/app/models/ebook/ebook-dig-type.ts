export interface EbookDigType {
  id: number;
  publicId?: string;
  tenantName?: string;
  code: string;
  descriptionVn: string;
  descriptionEn: string;
  sortOrder?: number | null;
}
