// Cấu hình server Z39.50 — nguồn: Entities/PrintBook/Opac/Z3950Config.cs
export interface Z3950Config {
  id:           number;
  name?:        string;
  host?:        string;
  port?:        string;
  databaseName?: string;
  userName?:    string;
  password?:    string;
  systax?:      string;   // syntax (theo tên gốc ELIB)
  url?:         string;
  language?:    string;
  groupId?:     number;
  portalId?:    string;
  publicId?:    string;
  tenantName?:  string;
}
