import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { Title } from '@angular/platform-browser';
import { firstValueFrom } from 'rxjs';

/** /api/opac/tenant/features — đơn vị lấy theo host (gateway), không cần đăng nhập. */
export interface Features {
  tenantId: number; code: string; name: string; status: string; modules: string[]; logoUrl: string | null; logoText: string | null;
}
interface PublicParameter { code: string; value: string | null; }

/** Tham số công khai OPAC đọc (service tenant chỉ trả tham số có cờ công khai). */
const PARAMETER_CODES = ['LIBRARYNAME', 'LIBRARY_ADDR', 'LIBRARY_TEL', 'LIBRARY_EMAIL', 'OPENHOUR'] as const;

/** Thông tin thư viện của host hiện tại — tải một lần khi mở app. */
@Injectable({ providedIn: 'root' })
export class Library {
  private readonly http = inject(HttpClient);
  private readonly title = inject(Title);
  private readonly document = inject(DOCUMENT);

  readonly features = signal<Features | null>(null);
  readonly parameters = signal<Record<string, string>>({});
  /** null = đang tải; chuỗi = lỗi hiển thị cho người dùng. */
  readonly error = signal<string | null>(null);
  readonly loaded = signal(false);

  readonly name = computed(() => this.parameters()['LIBRARYNAME'] || this.features()?.name || 'Thư viện');
  readonly shortName = computed(() => this.features()?.logoText || this.name());
  readonly active = computed(() => this.features()?.status === 'Active');

  has(module: string): boolean {
    return this.features()?.modules.includes(module) ?? false;
  }

  async load(): Promise<void> {
    try {
      const [features, parameters] = await Promise.all([
        firstValueFrom(this.http.get<Features>('/api/opac/tenant/features')),
        firstValueFrom(this.http.get<PublicParameter[]>('/api/opac/tenant/parameters', { params: { codes: PARAMETER_CODES.join(',') } })),
      ]);
      this.features.set(features);
      this.parameters.set(Object.fromEntries(parameters.filter((p) => p.value).map((p) => [p.code, p.value!])));
      this.title.setTitle(this.name());
      if (features.logoUrl) this.setIcon(features.logoUrl);
    } catch (e) {
      const status = (e as { status?: number }).status;
      this.error.set(
        status === 403 ? 'Thư viện đang tạm ngưng hoạt động.'
          : status === 404 ? 'Không có thư viện nào ở địa chỉ này.'
            : status === 503 ? 'Thư viện đang được khởi tạo, vui lòng quay lại sau ít phút.'
              : 'Không kết nối được máy chủ, vui lòng thử lại.');
    } finally {
      this.loaded.set(true);
    }
  }

  private setIcon(url: string): void {
    const link = this.document.querySelector<HTMLLinkElement>('link[rel="icon"]');
    if (link) {
      link.type = '';
      link.href = url;
    }
  }
}
