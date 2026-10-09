export interface AccessPolicy {
  id: number;
  publicId?: string;
  tenantName?: string;
  readerTypeid?: number | null;
  maxpage?: number | null;
  maxsize?: number | null;
  maxdocument?: number | null;
}
