export interface MapFloor {
  id:              number;
  buildingId:      number;
  floorNumber?:    number;
  name?:           string;
  layoutImageUrl?: string;
  width?:          number;
  height?:         number;
  status?:         number;
  publicId?:       string;
  tenantName?:     string;
}
