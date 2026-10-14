import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, Item, ReRegisterImportResult, StoreOption, errorMessage, saveFile } from '../../core/api';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';

/**
 * Đăng ký lại mã vạch (monolith: /admin/re-register-barcode, quyền RE_REGISTER): đổi số ĐKCB của một bản (tuỳ chọn chuyển kho) hoặc
 * hàng loạt từ Excel — kiểm tra trước, chỉ đổi khi cả file không có dòng lỗi. Bản đang mượn phải trả trước.
 */
@Component({
  selector: 'app-re-register',
  imports: [FormsModule, TranslatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'RE_REGISTER_PAGE.TITLE' | translate }}</h4></div>

    <div class="grid grid-cols-1 lg:grid-cols-2 gap-5">
      <div class="panel !mb-0 self-start">
        <h5 class="font-semibold text-gray-800 mb-3">{{ 'RE_REGISTER_PAGE.SINGLE' | translate }}</h5>
        <div class="grid grid-cols-2 gap-3">
          <div><label for="rr-old" class="form-label">{{ 'RE_REGISTER_PAGE.OLD' | translate }}</label><input id="rr-old" class="input font-mono" [(ngModel)]="oldBarcode" /></div>
          <div><label for="rr-new" class="form-label">{{ 'RE_REGISTER_PAGE.NEW' | translate }}</label><input id="rr-new" class="input font-mono" [(ngModel)]="newBarcode" (keydown.enter)="single()" /></div>
          <div class="col-span-2">
            <label for="rr-store" class="form-label">{{ 'SHELVING_PAGE.MOVE_TO' | translate }}</label>
            <select id="rr-store" class="input" [(ngModel)]="storeId">
              <option [ngValue]="null">{{ 'SHELVING_PAGE.KEEP_STORE' | translate }}</option>
              @for (s of stores(); track s.id) { <option [ngValue]="s.id">{{ s.code }} — {{ s.name }}</option> }
            </select>
          </div>
        </div>
        @if (can()) {
          <button type="button" class="btn-primary w-full justify-center mt-4" [disabled]="busy() || !oldBarcode.trim() || !newBarcode.trim()" (click)="single()">
            <span class="material-icons text-[18px]">published_with_changes</span> {{ 'RE_REGISTER_PAGE.APPLY' | translate }}</button>
        }
        @if (done(); as d) {
          <div class="alert-success !block text-sm mt-3">{{ 'RE_REGISTER_PAGE.DONE' | translate: { barcode: d.barcode, title: d.title ?? '' } }}</div>
        }
        <p class="text-xs text-gray-500 mt-3">{{ 'RE_REGISTER_PAGE.HINT' | translate }}</p>
      </div>

      <div class="panel !mb-0">
        <h5 class="font-semibold text-gray-800 mb-3">{{ 'RE_REGISTER_PAGE.BATCH' | translate }}</h5>
        <p class="text-sm text-gray-600 mb-3">{{ 'RE_REGISTER_PAGE.BATCH_HINT' | translate }}
          <button type="button" class="text-blue-600 hover:underline" (click)="template()">{{ 'IMPORT.TEMPLATE_FILE' | translate }}</button></p>
        <input type="file" accept=".xlsx" class="input" (change)="pickFile($event)" />
        @if (can()) {
          <div class="flex gap-2 mt-3">
            <button type="button" class="btn-secondary flex-1 justify-center" [disabled]="busy() || !file()" (click)="runImport(true)">
              <span class="material-icons text-[18px]">fact_check</span> {{ 'RE_REGISTER_PAGE.CHECK' | translate }}</button>
            <button type="button" class="btn-primary flex-1 justify-center" [disabled]="busy() || !file()" (click)="runImport(false)">
              <span class="material-icons text-[18px]">published_with_changes</span> {{ 'RE_REGISTER_PAGE.APPLY_FILE' | translate }}</button>
          </div>
        }
        @if (imported(); as r) {
          <div class="mt-4 text-sm">
            <div [class]="r.errorCount ? 'alert-error !block' : 'alert-success !block'">
              @if (r.errorCount) { {{ 'RE_REGISTER_PAGE.ERRORS' | translate: { count: r.errorCount, total: r.total } }} }
              @else if (r.dryRun) { {{ 'RE_REGISTER_PAGE.CHECKED' | translate: { total: r.total } }} }
              @else { {{ 'RE_REGISTER_PAGE.APPLIED' | translate: { count: r.applied } }} }
            </div>
            <div class="max-h-80 overflow-y-auto mt-2">
              <table class="w-full text-left border-collapse">
                <thead><tr>
                  <th class="th w-14">#</th><th class="th">{{ 'RE_REGISTER_PAGE.OLD' | translate }}</th><th class="th">{{ 'RE_REGISTER_PAGE.NEW' | translate }}</th>
                  <th class="th">{{ 'COMMON.STATUS' | translate }}</th>
                </tr></thead>
                <tbody>
                  @for (l of r.lines; track l.row) {
                    <tr>
                      <td class="td text-xs text-gray-500">{{ l.row }}</td><td class="td font-mono text-[13px]">{{ l.oldBarcode }}</td>
                      <td class="td font-mono text-[13px]">{{ l.newBarcode }}</td>
                      <td class="td text-xs">@if (l.error) { <span class="text-red-600">{{ l.error }}</span> } @else { <span class="text-green-700">OK</span> }</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }
      </div>
    </div>
  `,
})
export class ReRegister implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  private readonly session = inject(Session);

  protected oldBarcode = '';
  protected newBarcode = '';
  protected storeId: number | null = null;
  protected readonly stores = signal<StoreOption[]>([]);
  protected readonly file = signal<File | null>(null);
  protected readonly done = signal<Item | null>(null);
  protected readonly imported = signal<ReRegisterImportResult | null>(null);
  protected readonly busy = signal(false);

  ngOnInit(): void {
    this.api.storeOptions().then((s) => this.stores.set(s), () => undefined);
  }

  protected can(): boolean {
    return this.session.can('RE_REGISTER:edit');
  }

  protected pickFile(event: Event): void {
    this.file.set((event.target as HTMLInputElement).files?.[0] ?? null);
    this.imported.set(null);
  }

  protected async single(): Promise<void> {
    if (!this.oldBarcode.trim() || !this.newBarcode.trim()) return;
    this.busy.set(true);
    try {
      this.done.set(await this.api.reRegister({ oldBarcode: this.oldBarcode.trim(), newBarcode: this.newBarcode.trim(), storeId: this.storeId }));
      this.oldBarcode = '';
      this.newBarcode = '';
    } catch (e) {
      this.done.set(null);
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async runImport(dryRun: boolean): Promise<void> {
    const file = this.file();
    if (!file) return;
    this.busy.set(true);
    try {
      this.imported.set(await this.api.reRegisterImport(file, dryRun));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async template(): Promise<void> {
    try {
      saveFile(await this.api.reRegisterTemplate(), 'mau-dang-ky-lai-ma-vach.xlsx');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }
}
