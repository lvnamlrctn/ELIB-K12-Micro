export interface CircPlace {
  id:                      number;
  name?:                   string;
  workSession?:            string;
  cirType?:                number;
  cirWorkFollow?:          number;
  accessRequestValidtime?: number;
  limitBook?:              number;
  vitual?:                 number;
  code?:                   string;
  autoAccept?:             number;
  publicId?:               string;
}

export interface CircPlaceMapping {
  storeIds:      number[];
  readerTypeIds: number[];
}
