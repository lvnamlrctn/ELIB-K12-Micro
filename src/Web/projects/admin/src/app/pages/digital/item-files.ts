import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, EbookFile, EbookFileSearch, EbookItem, errorMessage } from '../../core/api';
import { LDatePipe } from '../../core/i18n';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { ConfirmDelete, Loading, Modal } from '../../shared/ui';

interface FileForm { publicId: string | null; type: string; description: string; sortOrder: number | null; file: File | null; }

const FILE_TYPES = ['Document', 'attachment'];

/**
 * Quản lý file của một tài liệu số (LRC: ebook-files). Bảng STT / Loại / Thông tin file / Kích thước / Ngày tải lên / Hành động (xem-tải, sửa, xoá);
 * "Thêm file" mở hộp tải lên (loại, mô tả, thứ tự). File lưu riêng tư ở media (PDF, tối đa 200 MB) và chỉ mở bằng URL ký có hạn.
 */
@Component({
  selector: 'app-digital-item-files',
  imports: [FormsModule, ConfirmDelete, LDatePipe, Loading, Modal, TranslatePipe],
  template: `
    <div class="mb-5 flex items-center gap-3">
      <button type="button" class="w-9 h-9 rounded-lg border border-gray-200 bg-white text-gray-600 hover:bg-gray-50 flex items-center justify-center" (click)="back()" [title]="'COMMON.BACK' | translate">
        <span class="material-icons text-[20px]">arrow_back</span>
      </button>
      <div>
        <h4 class="page-title !mb-0">{{ 'DIGITAL_DOC.MANAGE_FILES' | translate }}</h4>
        @if (item(); as i) { <div class="text-sm text-gray-500">{{ i.title }}</div> }
      </div>
    </div>

    <div class="panel flex items-center justify-between gap-3 flex-wrap">
      @if (can('add')) {
        <button type="button" class="inline-flex items-center gap-1.5 px-4 py-2 rounded-lg bg-emerald-600 text-white text-sm font-medium hover:bg-emerald-700" (click)="openUpload()">
          <span class="material-icons text-[18px]">file_upload</span> {{ 'DIGITAL_DOC.ADD_FILE' | translate }}
        </button>
      } @else { <span></span> }
      @if (files().length) { <span class="text-sm text-gray-500">{{ 'DIGITAL_DOC.FILES_TOTAL' | translate: { count: files().length } }}</span> }
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse min-w-[680px]">
          <thead><tr>
            <th class="th w-14 !text-center">{{ 'DIGITAL_DOC.COL_STT' | translate }}</th>
            <th class="th w-28">{{ 'DIGITAL_DOC.FILE_TYPE' | translate }}</th>
            <th class="th">{{ 'DIGITAL_DOC.FILE_INFO' | translate }}</th>
            <th class="th w-28 !text-right">{{ 'DIGITAL_DOC.FILE_SIZE' | translate }}</th>
            <th class="th w-40">{{ 'DIGITAL_DOC.FILE_DATE' | translate }}</th>
            <th class="th w-32 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
          </tr></thead>
          <tbody>
            @for (f of files(); track f.publicId; let i = $index) {
              <tr class="hover:bg-gray-50/50">
                <td class="td text-center text-sm text-gray-500">{{ i + 1 }}</td>
                <td class="td"><span class="text-xs px-2 py-0.5 rounded-full font-medium" [class]="chip(f.type)">{{ typeLabel(f.type) | translate }}</span></td>
                <td class="td">
                  <div class="flex items-center gap-2">
                    <span class="material-icons text-[22px] text-gray-400">{{ icon(f.fileExt) }}</span>
                    <div class="min-w-0">
                      <div class="text-sm font-medium text-gray-800 truncate">{{ f.description || f.fileExt || f.publicId }}</div>
                      @if (f.fileExt) { <span class="text-[10px] uppercase bg-gray-100 text-gray-600 rounded px-1.5 py-0.5">{{ f.fileExt }}</span> }
                    </div>
                  </div>
                </td>
                <td class="td text-right text-sm text-gray-600">{{ size(f.fileSize) }}</td>
                <td class="td text-sm text-gray-600">{{ f.createdAt ? (f.createdAt | ldate: 'dd/MM/yyyy HH:mm') : '—' }}</td>
                <td class="td">
                  <div class="flex items-center justify-center gap-1">
                    <button type="button" class="w-7 h-7 rounded bg-blue-50 text-blue-600 hover:bg-blue-100 flex items-center justify-center" (click)="open(f)" [title]="'DIGITAL_DOC.FILE_VIEW' | translate">
                      <span class="material-icons text-[15px] leading-none">visibility</span></button>
                    @if (can('edit')) {
                      <button type="button" class="w-7 h-7 rounded bg-amber-50 text-amber-600 hover:bg-amber-100 flex items-center justify-center" (click)="openEdit(f)" [title]="'COMMON.EDIT' | translate">
                        <span class="material-icons text-[15px] leading-none">edit</span></button>
                    }
                    @if (can('delete')) {
                      <button type="button" class="w-7 h-7 rounded bg-red-50 text-red-600 hover:bg-red-100 flex items-center justify-center" (click)="deleting.set(f)" [title]="'COMMON.DELETE' | translate">
                        <span class="material-icons text-[15px] leading-none">delete</span></button>
                    }
                  </div>
                </td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="py-14 text-center text-gray-500">
                <span class="material-icons text-[40px] text-gray-300 block">folder_open</span>{{ loading() ? '' : ('COMMON.NO_DATA' | translate) }}</td></tr>
            }
          </tbody>
        </table>
      </div>
    </div>

    @if (form(); as f) {
      <app-modal [title]="f.publicId ? 'DIGITAL_DOC.EDIT_FILE' : 'DIGITAL_DOC.ADD_FILE'" widthClass="max-w-sm" (closed)="form.set(null)">
        <form id="file-form" (ngSubmit)="save()" class="space-y-4">
          @if (!f.publicId) {
            <div>
              <label class="flex flex-col items-center justify-center h-28 border-2 border-dashed border-gray-300 rounded-xl cursor-pointer hover:bg-gray-50 text-gray-500">
                <span class="material-icons text-[30px]">upload_file</span>
                @if (f.file) { <span class="text-sm text-emerald-700 font-medium">{{ f.file.name }} ({{ size(f.file.size) }})</span> }
                @else { <span class="text-sm">{{ 'DIGITAL_DOC.PICK_FILE' | translate }}</span> }
                <input type="file" accept="application/pdf" class="hidden" (change)="pick($event, f)" />
              </label>
            </div>
          }
          <div>
            <label class="form-label" for="ff-type">{{ 'DIGITAL_DOC.FILE_TYPE' | translate }}</label>
            <select id="ff-type" name="type" class="input" [(ngModel)]="f.type">
              @for (t of types; track t) { <option [value]="t">{{ typeLabel(t) | translate }}</option> }
            </select>
          </div>
          <div><label class="form-label" for="ff-desc">{{ 'DIGITAL_DOC.FILE_DESCRIPTION' | translate }}</label>
            <input id="ff-desc" name="description" class="input" maxlength="500" [(ngModel)]="f.description" [placeholder]="'DIGITAL_DOC.FILE_DESCRIPTION_PH' | translate" /></div>
          <div><label class="form-label" for="ff-sort">{{ 'DIGITAL_DOC.FILE_SORT' | translate }}</label>
            <input id="ff-sort" name="sortOrder" type="number" min="0" class="input" [(ngModel)]="f.sortOrder" [placeholder]="'DIGITAL_DOC.FILE_SORT_PH' | translate" /></div>
          @if (saving()) {
            <p class="text-sm text-blue-600 flex items-center gap-2"><span class="material-icons animate-spin text-[18px]">autorenew</span> {{ 'DIGITAL_DOC.UPLOADING' | translate }}</p>
          }
        </form>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="form.set(null)">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="submit" form="file-form" class="btn-primary" [disabled]="saving() || (!f.publicId && !f.file)">
            <span class="material-icons text-[16px]">{{ f.publicId ? 'save' : 'upload' }}</span> {{ (f.publicId ? 'COMMON.SAVE' : 'DIGITAL_DOC.UPLOAD') | translate }}
          </button>
        </ng-container>
      </app-modal>
    }

    @if (deleting(); as d) {
      <app-confirm-delete [busy]="saving()" message="COMMON.DELETE_CONFIRM_IRREVERSIBLE" (confirmed)="doDelete(d)" (cancelled)="deleting.set(null)" />
    }
  `,
})
export class DigitalItemFiles implements OnInit {
  private readonly api = inject(Api);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);
  private readonly fileClient = this.api.crud<EbookFile>('files', 'digital');

  protected readonly types = FILE_TYPES;
  protected readonly item = signal<EbookItem | null>(null);
  protected readonly files = signal<EbookFile[]>([]);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly form = signal<FileForm | null>(null);
  protected readonly deleting = signal<EbookFile | null>(null);
  private publicId = '';

  ngOnInit(): void {
    this.publicId = this.route.snapshot.paramMap.get('publicId') ?? '';
    void this.load();
  }

  protected can(action: string): boolean {
    return this.session.can(`DIGITAL_ITEMS:${action}`);
  }

  protected back(): void {
    void this.router.navigate(['/digital-items']);
  }

  protected typeLabel(type: string | null): string {
    return type === 'attachment' ? 'DIGITAL_DOC.TYPE_ATTACHMENT' : type === 'cover' ? 'DIGITAL_DOC.TYPE_COVER' : 'DIGITAL_DOC.TYPE_FULLTEXT';
  }

  protected chip(type: string | null): string {
    return type === 'attachment' ? 'bg-amber-100 text-amber-700' : type === 'cover' ? 'bg-purple-100 text-purple-700' : 'bg-blue-100 text-blue-700';
  }

  protected icon(ext: string | null): string {
    switch ((ext ?? '').toLowerCase().replace('.', '')) {
      case 'pdf': return 'picture_as_pdf';
      case 'doc': case 'docx': return 'description';
      case 'xls': case 'xlsx': return 'table_chart';
      case 'jpg': case 'jpeg': case 'png': case 'gif': return 'image';
      case 'mp4': case 'avi': case 'mov': return 'videocam';
      default: return 'insert_drive_file';
    }
  }

  protected size(bytes: number | null): string {
    if (bytes == null) return '—';
    const kb = bytes / 1024;
    return kb < 1024 ? `${kb.toFixed(1)} KB` : `${(kb / 1024).toFixed(2)} MB`;
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      const [item, files] = await Promise.all([
        this.api.crud<EbookItem>('items', 'digital').get(this.publicId),
        this.fileClient.search({ ebookItemPublicId: this.publicId, pageSize: 200 } as EbookFileSearch),
      ]);
      this.item.set(item);
      this.files.set([...files.items].sort((a, b) => a.sortOrder - b.sortOrder || a.id - b.id));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }

  protected openUpload(): void {
    this.form.set({ publicId: null, type: 'Document', description: '', sortOrder: this.files().length + 1, file: null });
  }

  protected openEdit(f: EbookFile): void {
    this.form.set({ publicId: f.publicId, type: f.type ?? 'Document', description: f.description ?? '', sortOrder: f.sortOrder, file: null });
  }

  protected pick(event: Event, f: FileForm): void {
    const input = event.target as HTMLInputElement;
    f.file = input.files?.[0] ?? null;
    input.value = '';
  }

  protected async open(f: EbookFile): Promise<void> {
    try {
      window.open(await this.api.fileUrl(f.mediaFileId), '_blank', 'noopener');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected async save(): Promise<void> {
    const f = this.form();
    if (!f) return;
    this.saving.set(true);
    try {
      if (f.publicId) {
        const current = this.files().find((x) => x.publicId === f.publicId)!;
        await this.fileClient.update(f.publicId, {
          ebookItemPublicId: this.publicId, mediaFileId: current.mediaFileId, type: f.type, isConvert: current.isConvert, fileType: current.fileType,
          fileSize: current.fileSize, fileExt: current.fileExt, description: f.description.trim() || null, source: current.source, formatId: current.formatId,
          checkSumAlgorithm: current.checkSumAlgorithm, sortOrder: f.sortOrder ?? 0,
        });
      } else if (f.file) {
        const uploaded = await this.api.upload('ebook-file', f.file);
        const ext = f.file.name.includes('.') ? f.file.name.slice(f.file.name.lastIndexOf('.') + 1).toLowerCase() : null;
        await this.fileClient.add({
          ebookItemPublicId: this.publicId, mediaFileId: uploaded.id, type: f.type, fileType: f.file.type || null, fileExt: ext, fileSize: f.file.size,
          description: f.description.trim() || f.file.name, sortOrder: f.sortOrder ?? this.files().length + 1,
        });
      }
      this.toastr.success(f.publicId ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS');
      this.form.set(null);
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.saving.set(false);
    }
  }

  protected async doDelete(f: EbookFile): Promise<void> {
    this.saving.set(true);
    try {
      await this.fileClient.delete(f.publicId);
      this.toastr.success('COMMON.DELETE_SUCCESS');
      this.deleting.set(null);
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.saving.set(false);
    }
  }
}
