export interface Reader {
  id:                      number | string;
  publicId?:               string;
  tenantName?:             string | null;
  cardno?:                 string | null;
  citizenId?:              string | null;   // số căn cước công dân
  cardUid?:                string | null;   // UID chip thẻ (đầu đọc kiểm soát cửa)
  firstName?:              string | null;
  lastName?:               string | null;
  email?:                  string | null;
  phone?:                  string | null;
  address?:                string | null;
  orgId?:                  number | null;
  orgName?:                string | null;
  readerTypeId?:           number | null;
  readerTypeName?:         string | null;
  classId?:                number | null;
  courseId?:               number | null;
  degreeId?:               number | null;
  ethenicId?:              number | null;
  profId?:                 number | null;
  blane?:                  number | null;
  createdDate?:            string | null;
  expireDate?:             string | null;
  issueDate?:              string | null;
  birthDate?:              string | null;
  password?:               string | null;
  portalId?:               string | null;
  language?:               string | null;
  photo?:                  string | null;
  lasttimeLogin?:          string | null;
  lastLogin?:              string | null;
  lastUpdate?:             string | null;
  status?:                 number | null;
  sex?:                    number | null;
  requiredChangePassword?: number | null;
  isChangePassword?:       number | null;
  createdBy?:              number | null;
  updatedBy?:              number | null;
  isDelete?:               number | null;
  tenantId?:               string | null;
}
