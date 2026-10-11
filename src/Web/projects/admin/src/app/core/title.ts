import { Injectable, Injector, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';
import { Language } from './i18n';

/**
 * Tiêu đề tab theo ngôn ngữ: title của route là khoá dịch (màn chưa chuyển vẫn là câu tiếng Việt — giữ nguyên).
 * Language lấy lúc đổi tiêu đề, không inject khi khởi tạo: Router cần TitleStrategy, còn TranslateService khi khởi tạo đã gọi HTTP qua
 * authInterceptor (inject Router) — inject thẳng thành vòng phụ thuộc (NG0200).
 */
@Injectable({ providedIn: 'root' })
export class TranslatedTitleStrategy extends TitleStrategy {
  private readonly title = inject(Title);
  private readonly injector = inject(Injector);

  override updateTitle(snapshot: RouterStateSnapshot): void {
    const key = this.buildTitle(snapshot);
    if (key) this.title.setTitle(this.injector.get(Language).t(key));
  }
}
