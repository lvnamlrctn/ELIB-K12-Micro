export interface User {
  id:            number | string;
  publicId?:     string;
  tenantName?:   string | null;
  fullName?:     string | null;
  loginName?:    string | null;
  email?:        string | null;
  phone?:        string | null;
  tenantId?:     string | null;
  portalId?:     string | null;
  language?:     string | null;
  roleId?:       number | null;
  postionId?:    number | null;
  status?:       number | null;
  sex?:          number | null;
  address?:      string | null;
  birthDate?:    string | null;
  createdDate?:  string | null;
  photo?:        string | null;
  roleName?:     string | null;
}
