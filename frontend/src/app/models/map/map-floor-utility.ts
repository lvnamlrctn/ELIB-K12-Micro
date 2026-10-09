export interface MapFloorUtility {
  id:               number;
  floorId:          number;
  name?:            string;
  description?:     string;
  quantity?:        number;
  conditionStatus?: string; // HOAT_DONG, BAO_TRI, HU_HONG
  iconName?:        string;
  status?:          number;
  publicId?:        string;
}
