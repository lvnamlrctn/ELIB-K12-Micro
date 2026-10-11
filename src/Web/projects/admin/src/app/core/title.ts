import { Injectable, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';
import { Language } from './i18n';

/** Tiêu đề tab theo ngôn ngữ: title của route là khoá dịch (màn chưa chuyển vẫn là câu tiếng Việt — giữ nguyên). */
@Injectable({ providedIn: 'root' })
export class TranslatedTitleStrategy extends TitleStrategy {
  private readonly title = inject(Title);
  private readonly language = inject(Language);

  override updateTitle(snapshot: RouterStateSnapshot): void {
    const key = this.buildTitle(snapshot);
    if (key) this.title.setTitle(this.language.t(key));
  }
}
