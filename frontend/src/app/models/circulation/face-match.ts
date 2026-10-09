export interface FaceMatchResult {
  readerId:   number;
  publicId:   string;
  cardNo?:    string;
  name?:      string;
  photo?:     string;
  confidence: number;
}
