import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Language } from '../core/i18n';
import { Library } from '../core/library';
import { PortalApi, PortalContactGroup } from '../core/portal';

/** Liên hệ với thư viện: gửi góp ý/câu hỏi tới thủ thư (ẩn danh). Ô "website" ẩn là bẫy bot; nội dung giới hạn độ dài ở server. */
@Component({
  selector: 'opac-contact',
  imports: [FormsModule, TranslatePipe],
  template: `
    <div class="max-w-2xl mx-auto px-4 py-8">
      <h1 class="text-2xl font-bold text-slate-900">{{ 'OPAC.PORTAL.CONTACT_TITLE' | translate }}</h1>
      <p class="mt-1 text-slate-600 text-sm">{{ 'OPAC.PORTAL.CONTACT_HINT' | translate }}</p>
      @if (library.parameters()['LIBRARY_TEL'] || library.parameters()['LIBRARY_EMAIL'] || library.parameters()['LIBRARY_ADDR']) {
        <div class="mt-4 bg-white border border-slate-200 rounded-xl p-4 text-sm space-y-1">
          @if (library.parameters()['LIBRARY_ADDR']; as v) { <div class="flex gap-2"><span class="material-icons text-[18px] text-slate-400">place</span>{{ v }}</div> }
          @if (library.parameters()['LIBRARY_TEL']; as v) { <div class="flex gap-2"><span class="material-icons text-[18px] text-slate-400">call</span>{{ v }}</div> }
          @if (library.parameters()['LIBRARY_EMAIL']; as v) { <div class="flex gap-2"><span class="material-icons text-[18px] text-slate-400">mail</span>{{ v }}</div> }
        </div>
      }
      <form class="mt-5 bg-white border border-slate-200 rounded-xl p-5 space-y-3" (ngSubmit)="submit()">
        <input type="text" name="website" [(ngModel)]="website" tabindex="-1" autocomplete="off" aria-hidden="true" class="absolute -left-[9999px] w-px h-px opacity-0" />
        @if (groups().length) {
          <div>
            <label class="block text-sm font-medium text-slate-700 mb-1" for="c-group">{{ 'OPAC.PORTAL.CONTACT_GROUP' | translate }}</label>
            <select id="c-group" name="group" [(ngModel)]="groupId" class="w-full px-3 py-2 border border-slate-300 rounded-lg text-sm">
              <option [ngValue]="null">—</option>@for (g of groups(); track g.id) { <option [ngValue]="g.id">{{ g.name }}</option> }
            </select>
          </div>
        }
        <div class="grid sm:grid-cols-2 gap-3">
          <div><label class="block text-sm font-medium text-slate-700 mb-1" for="c-name">{{ 'OPAC.PORTAL.YOUR_NAME' | translate }} *</label>
            <input id="c-name" name="name" [(ngModel)]="name" required maxlength="200" class="w-full px-3 py-2 border border-slate-300 rounded-lg text-sm" /></div>
          <div><label class="block text-sm font-medium text-slate-700 mb-1" for="c-email">{{ 'OPAC.PORTAL.YOUR_EMAIL' | translate }} *</label>
            <input id="c-email" name="email" type="email" [(ngModel)]="email" required maxlength="200" class="w-full px-3 py-2 border border-slate-300 rounded-lg text-sm" /></div>
          <div><label class="block text-sm font-medium text-slate-700 mb-1" for="c-mobile">{{ 'OPAC.PORTAL.YOUR_PHONE' | translate }}</label>
            <input id="c-mobile" name="mobile" [(ngModel)]="mobile" maxlength="30" class="w-full px-3 py-2 border border-slate-300 rounded-lg text-sm" /></div>
          <div><label class="block text-sm font-medium text-slate-700 mb-1" for="c-title">{{ 'OPAC.PORTAL.SUBJECT' | translate }}</label>
            <input id="c-title" name="title" [(ngModel)]="subject" maxlength="250" class="w-full px-3 py-2 border border-slate-300 rounded-lg text-sm" /></div>
        </div>
        <div><label class="block text-sm font-medium text-slate-700 mb-1" for="c-content">{{ 'OPAC.PORTAL.MESSAGE' | translate }} *</label>
          <textarea id="c-content" name="content" [(ngModel)]="content" required rows="5" maxlength="4000" class="w-full px-3 py-2 border border-slate-300 rounded-lg text-sm"></textarea></div>
        @if (sent()) { <div class="rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2" role="status">{{ 'OPAC.PORTAL.CONTACT_SENT' | translate }}</div> }
        @if (error(); as err) { <p class="text-red-600 text-sm" role="alert">{{ err }}</p> }
        <button type="submit" [disabled]="busy()" class="px-4 py-2 rounded-lg bg-blue-600 text-white text-sm font-medium hover:bg-blue-700 disabled:opacity-50">{{ 'OPAC.PORTAL.SEND' | translate }}</button>
      </form>
    </div>
  `,
})
export class Contact implements OnInit {
  private readonly api = inject(PortalApi);
  private readonly language = inject(Language);
  protected readonly library = inject(Library);

  protected readonly groups = signal<PortalContactGroup[]>([]);
  protected readonly busy = signal(false);
  protected readonly sent = signal(false);
  protected readonly error = signal<string | null>(null);
  protected groupId: number | null = null;
  protected name = '';
  protected email = '';
  protected mobile = '';
  protected subject = '';
  protected content = '';
  protected website = '';

  ngOnInit(): void {
    this.api.contactGroups(this.language.lang()).then((g) => this.groups.set(g), () => this.groups.set([]));
  }

  protected async submit(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    this.sent.set(false);
    try {
      await this.api.contact({
        contactGroupId: this.groupId, name: this.name, email: this.email, mobile: this.mobile || null, address: null, title: this.subject || null, content: this.content,
        website: this.website || null,
      });
      this.sent.set(true);
      this.content = '';
      this.subject = '';
    } catch (e) {
      this.error.set(this.language.apiError(e));
    } finally {
      this.busy.set(false);
    }
  }
}
