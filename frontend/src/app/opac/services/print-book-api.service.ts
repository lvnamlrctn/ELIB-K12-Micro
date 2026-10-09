import { APP_CONFIG } from '../config';
import { Injectable, inject, PLATFORM_ID } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { isPlatformServer } from '@angular/common';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';

/** Chi tiết 1 biểu ghi tài liệu in (PrintBook.Bib), OPAC công khai. */

export interface PrintBookHolding {
  barcodeId: number;
  barcode?: string;
  storeName?: string;
  status?: string;
  statusName?: string;
}

export interface PrintBookControlField {
  field: string;
  value: string;
  description?: string;
}

export interface PrintBookMarcField {
  field: string;
  indicator1: string;
  indicator2: string;
  subField: string;
  data: string;
  fieldDescription?: string;
  subFieldDescription?: string;
}

export interface PrintBookDetail {
  publicId: string;
  bibId: number;
  mfn?: number;
  title?: string;
  author?: string;
  publisher?: string;
  publishDate?: string;
  publishYear?: number;
  ddc?: string;
  cutter?: string;
  isbn?: string;
  summary?: string;
  keyword?: string;
  language?: string;
  materialType?: string;
  collectionId?: string;
  collectionName?: string;
  contributor?: string;
  images?: string;
  isbd?: string;
  status?: string;
  statusName?: string;
  url?: string;
  copyCount: number;
  availableCount: number;
  holdings: PrintBookHolding[];
  controlFields: PrintBookControlField[];
  marcFields: PrintBookMarcField[];
}

@Injectable({ providedIn: 'root' })
export class PrintBookApiService {
  private http = inject(HttpClient);
  private platformId = inject(PLATFORM_ID);

  private get backendRoot(): string {
    return isPlatformServer(this.platformId) ? APP_CONFIG.BackendBase : '';
  }

  getDetail(publicId: string): Observable<PrintBookDetail | null> {
    return this.http.get<any>(`${this.backendRoot}/api/public/PublicPrintBook/${publicId}`).pipe(
      map(res => (res?.success && res.data ? (res.data as PrintBookDetail) : null)),
      catchError(() => of(null))
    );
  }

  getMarcBinary(publicId: string): Observable<ArrayBuffer> {
    return this.http.get(`${this.backendRoot}/api/public/PublicPrintBook/${publicId}/marc21`, {
      responseType: 'arraybuffer'
    });
  }
}
