import { Component, DestroyRef, OnInit, ViewEncapsulation, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Meta, Title } from '@angular/platform-browser';
import { ActivatedRoute } from '@angular/router';
import { Language } from '../core/i18n';
import { Library } from '../core/library';
import { PortalApi, PortalPageContent } from '../core/portal';

/** Trang tĩnh của cổng thông tin (/trang/{mã}) — nội dung HTML đã được server làm sạch; Angular lọc lại khi gán [innerHTML]. */
@Component({
  selector: 'opac-static-page',
  template: `
    <article class="max-w-3xl mx-auto px-4 py-8">
      @if (loading()) { <div class="flex justify-center py-20"><span class="material-icons animate-spin text-blue-600 text-3xl">autorenew</span></div> }
      @else if (page(); as p) {
        <h1 class="text-3xl font-bold text-slate-900">{{ p.title }}</h1>
        <div class="mt-5 portal-content text-slate-800 leading-relaxed" [innerHTML]="p.content ?? ''"></div>
      } @else if (error()) { <p class="mt-10 text-center text-slate-500" role="alert">{{ error() }}</p> }
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
    .portal-content img.img-left { float: left; margin: 0 1rem 0.5rem 0; }
    .portal-content img.img-right { float: right; margin: 0 0 0.5rem 1rem; }
    .portal-content img.img-center { display: block; margin: 0.5rem auto; }
    .portal-content :where(table) { border-collapse: collapse; width: 100%; margin: 0.8em 0; }
    .portal-content :where(td, th) { border: 1px solid #cbd5e1; padding: 6px 10px; }
  `,
  encapsulation: ViewEncapsulation.None,
})
export class StaticPage implements OnInit {
  private readonly api = inject(PortalApi);
  private readonly route = inject(ActivatedRoute);
  private readonly language = inject(Language);
  private readonly library = inject(Library);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly page = signal<PortalPageContent | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((p) => void this.load(p.get('slug') ?? ''));
  }

  private async load(slug: string): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    this.page.set(null);
    try {
      const page = await this.api.page(slug, this.language.lang());
      this.page.set(page);
      this.title.setTitle(`${page.title} — ${this.library.name()}`);
      this.meta.updateTag({ name: 'description', content: page.metaDescription ?? '' });
    } catch (e) {
      this.error.set(this.language.apiError(e));
    } finally {
      this.loading.set(false);
    }
  }
}
