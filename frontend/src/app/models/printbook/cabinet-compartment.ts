export interface CabinetCompartment {
  id: number;
  cabinetId?: number;
  rowIndex: number;
  colIndex: number;
  code?: string;
  name?: string;
  note?: string;
  status?: number;
  isOccupied?: boolean;
  publicId?: string;
}

export interface CabinetCompartmentGrid {
  cabinetId: number;
  rows?: number;
  cols?: number;
  items: CabinetCompartment[];
}
