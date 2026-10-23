import { Component, DestroyRef, OnInit, ViewEncapsulation, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Meta, Title } from '@angular/platform-browser';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { LDatePipe, Language } from '../core/i18n';
import { Library } from '../core/library';
import { PortalApi, PortalComment, PortalNewsDetail } from '../core/portal';

/**
 * Chi tiết tin. Nội dung HTML do server đã làm sạch; Angular vẫn tự lọc lại khi gán [innerHTML] (không bypass). Bình luận (nếu tin cho phép) luôn chờ
 * thủ thư duyệt; ô "website" ẩn là bẫy bot.
 */
@Component({
  selector: 'opac-news-detail',
  imports: [FormsModule, RouterLink, LDatePipe, TranslatePipe],
  template: `
    <article class="max-w-3xl mx-auto px-4 py-8">
      <a routerLink="/tin-tuc" class="text-sm text-blue-600 hover:underline inline-flex items-center gap-1"><span class="material-icons text-[18px]">arrow_back</span>{{ 'OPAC.PORTAL.BACK_TO_NEWS' | translate }}</a>
      @if (loading()) { <div class="flex justify-center py-20"><span class="material-icons animate-spin text-blue-600 text-3xl">autorenew</span></div> }
      @else if (news(); as n) {
        <h1 class="mt-3 text-3xl font-bold text-slate-900 leading-tight">{{ n.title }}</h1>
        <div class="mt-2 text-sm text-slate-500">{{ n.publishedAt | ldate: 'dd/MM/yyyy HH:mm' }}@if (n.author) { · {{ n.author }} }@if (n.categoryName) { · {{ n.categoryName }} } · {{ 'OPAC.PORTAL.VIEWS_N' | translate: { count: n.totalView } }}</div>
        @if (n.brief) { <p class="mt-4 text-lg text-slate-700 font-medium">{{ n.brief }}</p> }
        @if (n.images) { <img [src]="n.images" alt="" class="mt-4 w-full rounded-xl object-cover max-h-[420px]" /> }
        <div class="mt-5 portal-content text-slate-800 leading-relaxed" [innerHTML]="n.content ?? ''"></div>
        @if (n.source) { <p class="mt-4 text-sm text-slate-500">{{ 'OPAC.PORTAL.SOURCE' | translate }}: {{ n.source }}</p> }

        @if (n.attachments.length) {
          <section class="mt-6">
            <h2 class="font-semibold text-slate-900">{{ 'OPAC.PORTAL.ATTACHMENTS' | translate }}</h2>
            <ul class="mt-2 space-y-1">
              @for (a of n.attachments; track a.publicId) {
                <li><a [href]="a.url" target="_blank" rel="noopener noreferrer" class="inline-flex items-center gap-1 text-blue-700 hover:underline"><span class="material-icons text-[18px]">attach_file</span>{{ a.name }}</a></li>
              }
            </ul>
          </section>
        }

        @if (n.allowComment) {
          <section class="mt-8 border-t border-slate-200 pt-6">
            <h2 class="font-semibold text-slate-900">{{ 'OPAC.PORTAL.COMMENTS' | translate }}</h2>
            <ul class="mt-3 space-y-3">
              @for (c of comments(); track c.publicId) {
                <li class="bg-white border border-slate-200 rounded-lg px-4 py-3 text-sm"><div class="font-medium text-slate-900">{{ c.name }} <span class="text-xs text-slate-400 font-normal">{{ c.createdAt | ldate: 'dd/MM/yyyy HH:mm' }}</span></div>
                  @if (c.title) { <div class="text-slate-700">{{ c.title }}</div> }<p class="text-slate-700 whitespace-pre-line">{{ c.content }}</p></li>
              } @empty { <li class="text-sm text-slate-500">{{ 'OPAC.PORTAL.NO_COMMENTS' | translate }}</li> }
            </ul>
            <form class="mt-4 border border-slate-200 rounded-lg p-4 space-y-2 bg-white" (ngSubmit)="submit(n)">
              <div class="font-medium text-slate-900 text-sm">{{ 'OPAC.PORTAL.WRITE_COMMENT' | translate }}</div>
              <input type="text" name="website" [(ngModel)]="website" tabindex="-1" autocomplete="off" aria-hidden="true" class="absolute -left-[9999px] w-px h-px opacity-0" />
              <div class="grid sm:grid-cols-2 gap-2">
                <input name="name" [(ngModel)]="name" required maxlength="200" [placeholder]="'OPAC.PORTAL.YOUR_NAME' | translate" [attr.aria-label]="'OPAC.PORTAL.YOUR_NAME' | translate" class="px-3 py-1.5 border border-slate-300 rounded-lg text-sm" />
                <input name="email" type="email" [(ngModel)]="email" maxlength="200" [placeholder]="'OPAC.PORTAL.YOUR_EMAIL' | translate" [attr.aria-label]="'OPAC.PORTAL.YOUR_EMAIL' | translate" class="px-3 py-1.5 border border-slate-300 rounded-lg text-sm" />
              </div>
              <textarea name="content" [(ngModel)]="content" required rows="3" maxlength="2000" [placeholder]="'OPAC.PORTAL.YOUR_COMMENT' | translate" [attr.aria-label]="'OPAC.PORTAL.YOUR_COMMENT' | translate" class="w-full px-3 py-1.5 border border-slate-300 rounded-lg text-sm"></textarea>
              @if (sent()) { <p class="text-green-700 text-sm" role="status">{{ 'OPAC.PORTAL.COMMENT_SENT' | translate }}</p> }
              @if (formError(); as err) { <p class="text-red-600 text-sm" role="alert">{{ err }}</p> }
              <button type="submit" [disabled]="busy()" class="px-3 py-1.5 rounded-lg bg-blue-600 text-white text-sm font-medium hover:bg-blue-700 disabled:opacity-50">{{ 'OPAC.PORTAL.SEND' | translate }}</button>
            </form>
          </section>
        }

        @if (n.related.length) {
          <section class="mt-8 border-t border-slate-200 pt-6">
            <h2 class="font-semibold text-slate-900">{{ 'OPAC.PORTAL.RELATED' | translate }}</h2>
            <ul class="mt-2 space-y-1">
              @for (r of n.related; track r.publicId) { <li><a [routerLink]="['/tin-tuc', r.slug]" class="text-blue-700 hover:underline">{{ r.title }}</a> <span class="text-xs text-slate-400">{{ r.publishedAt | ldate }}</span></li> }
            </ul>
          </section>
        }
      } @else if (error()) {
        <p class="mt-10 text-center text-slate-500" role="alert">{{ error() }}</p>
      }
    </article>
  `,
  styles: `
    .portal-content :where(h2) { font-size: 1.4rem; font-weight: 700; margin: 1.2em 0 0.4em; }
    .portal-content :where(h3) { font-size: 1.2rem; font-weight: 700; margin: 1.1em 0 0.4em; }
    .portal-content :where(p) { margin: 0.7em 0; }
    .portal-content :where(ul) { list-style: disc; padding-left: 1.5rem; margin: 0.7em 0; }
    .portal-content :where(ol) { list-style: decimal; padding-left: 1.5rem; margin: 0.7em 0; }
    .portal-content :where(blockquote) { border-left: 3px solid #cbd5e1; padding-left: 1rem; color: #475569; margin: 0.8em 0; }
    .portal-content :where(a) { color: #1d4ed8; text-decoration: underline; }
    .portal-content :where(img) { max-width: 100%; height: auto; border-radius: 0.5rem; }
    .portal-content :where(table) { border-collapse: collapse; width: 100%; margin: 0.8em 0; }
    .portal-content :where(td, th) { border: 1px solid #cbd5e1; padding: 6px 10px; }
  `,
  encapsulation: ViewEncapsulation.None, // nội dung [innerHTML] không mang thuộc tính _ngcontent
})
export class NewsDetail implements OnInit {
  private readonly api = inject(PortalApi);
  private readonly route = inject(ActivatedRoute);
  private readonly language = inject(Language);
  private readonly library = inject(Library);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly news = signal<PortalNewsDetail | null>(null);
  protected readonly comments = signal<PortalComment[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly sent = signal(false);
  protected readonly formError = signal<string | null>(null);
  protected name = '';
  protected email = '';
  protected content = '';
  protected website = '';

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((p) => void this.load(p.get('slug') ?? ''));
  }

  protected async submit(n: PortalNewsDetail): Promise<void> {
    this.busy.set(true);
    this.formError.set(null);
    this.sent.set(false);
    try {
      await this.api.comment(n.slug, { name: this.name, email: this.email || null, title: null, content: this.content, website: this.website || null });
      this.sent.set(true);
      this.content = '';
    } catch (e) {
      this.formError.set(this.language.apiError(e));
    } finally {
      this.busy.set(false);
    }
  }

  private async load(slug: string): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    this.news.set(null);
    try {
      const news = await this.api.news(slug, this.language.lang());
      this.news.set(news);
      this.title.setTitle(`${news.metaTitle || news.title} — ${this.library.name()}`);
      this.meta.updateTag({ name: 'description', content: news.metaDescription || news.brief || '' });
      if (news.allowComment) this.api.comments(slug).then((c) => this.comments.set(c), () => this.comments.set([]));
      void this.api.viewed(slug).catch(() => undefined);
    } catch (e) {
      this.error.set(this.language.apiError(e));
    } finally {
      this.loading.set(false);
    }
  }
}
