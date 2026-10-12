import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, CrudClient, Reader, STATUS_ACTIVE, errorMessage } from '../../core/api';
import { ToastrService } from '../../shared/toastr';
import { Modal } from '../../shared/ui';
import { ReaderCatalogs } from './reader-catalogs';

type ReaderForm = Omit<Reader, 'id' | 'publicId' | 'fullName' | 'status' | 'lockReason' | 'createdAt' | 'photoId'> & { publicId: string | null };

const BLANK: ReaderForm = {
  publicId: null, cardNo: '', lastName: '', firstName: '', citizenId: null, cardUid: null, email: null, phone: null, address: null,
  birthDate: null, sex: null, readerTypeId: null, classId: null, courseId: null, orgId: null, degreeId: null, ethnicityId: null,
  academicTitleId: null, issueDate: null, expireDate: null,
};

/**
 * Thêm/sửa bạn đọc — các trường và thứ tự như form của ELIB-LRC (pages/admin/reader): mã thẻ, CCCD, UID thẻ chip, loại, họ, tên, ngày
 * sinh, giới tính, khoá, lớp, dân tộc, trình độ, học hàm, đơn vị, liên hệ, ngày cấp/hết hạn. Ảnh thẻ lưu ngay khi chọn (media, riêng tư).
 * Khác LRC: mật khẩu OPAC đặt ở hộp thoại riêng (identity) — không đi cùng hồ sơ.
 * Khoá dịch giữ như ELIB-LRC: READER.FIRST_NAME là ô "Họ" (lastName), READER.LAST_NAME là ô "Tên" (firstName).
 */
@Component({
  selector: 'app-reader-form',
  imports: [FormsModule, Modal, TranslatePipe],
  template: `
    <app-modal [title]="f.publicId ? 'READER.EDIT_TITLE' : 'READER.ADD_TITLE'" widthClass="max-w-4xl" (closed)="closed.emit()">
      <div class="flex flex-col md:flex-row gap-6">
        @if (f.publicId) {
          <div class="md:w-40 shrink-0 text-sm text-gray-600 space-y-2">
            <div class="w-32 h-40 rounded-lg bg-gray-100 border border-gray-200 overflow-hidden flex items-center justify-center mx-auto md:mx-0">
              @if (photoUrl()) { <img [src]="photoUrl()" [alt]="'READER.AVATAR' | translate" class="w-full h-full object-cover" /> }
              @else { <span class="material-icons text-gray-300 text-[56px]">person</span> }
            </div>
            <label class="btn-secondary !py-1.5 !px-3 cursor-pointer w-full justify-center" [class.opacity-50]="photoBusy()">
              <span class="material-icons text-[18px]">{{ photoBusy() ? 'autorenew' : 'photo_camera' }}</span>
              {{ (photoId() ? 'READER.CHANGE_PHOTO' : 'READER.CHOOSE_PHOTO') | translate }}
              <input type="file" accept="image/jpeg,image/png,image/webp" class="hidden" [disabled]="photoBusy()" (change)="changePhoto($event)" />
            </label>
            @if (photoId()) {
              <button type="button" class="btn-secondary !py-1.5 !px-3 !text-red-600 w-full justify-center" [disabled]="photoBusy()" (click)="setPhoto(null)">
                <span class="material-icons text-[18px]">delete</span> {{ 'READER.DELETE_PHOTO' | translate }}</button>
            }
            <p class="text-xs text-gray-500">{{ 'READER.PHOTO_HINT' | translate }}</p>
          </div>
        }
        <form id="reader-form" (ngSubmit)="save()" class="flex-1 grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          <div><label class="form-label" for="r-card">{{ 'READER.CARD_CODE' | translate }} <span class="text-red-500">*</span></label>
            <input id="r-card" name="card" class="input font-mono uppercase" [(ngModel)]="f.cardNo" required (blur)="checkCard()" />
            @if (cardTaken()) { <p class="text-xs text-red-600 mt-1">{{ 'READER.CARDNO_EXISTS' | translate }}</p> }</div>
          <div><label class="form-label" for="r-cid">{{ 'READER.CITIZEN_ID' | translate }}</label>
            <input id="r-cid" name="cid" class="input" maxlength="20" [(ngModel)]="f.citizenId" /></div>
          <div><label class="form-label" for="r-uid">{{ 'READER.CARD_UID' | translate }}</label>
            <input id="r-uid" name="uid" class="input font-mono" maxlength="64" [(ngModel)]="f.cardUid" [placeholder]="'READER.CARD_UID_PH' | translate" /></div>
          <div><label class="form-label" for="r-type">{{ 'READER.READER_TYPE' | translate }}</label>
            <select id="r-type" name="type" class="input" [(ngModel)]="f.readerTypeId"><option [ngValue]="null">—</option>
              @for (t of c.types(); track t.id) { <option [ngValue]="t.id">{{ t.name }}</option> }</select></div>
          <div><label class="form-label" for="r-last">{{ 'READER.FIRST_NAME' | translate }}</label>
            <input id="r-last" name="last" class="input" [(ngModel)]="f.lastName" /></div>
          <div><label class="form-label" for="r-first">{{ 'READER.LAST_NAME' | translate }} <span class="text-red-500">*</span></label>
            <input id="r-first" name="first" class="input" [(ngModel)]="f.firstName" required /></div>
          <div><label class="form-label" for="r-birth">{{ 'READER.BIRTH_DATE' | translate }}</label>
            <input id="r-birth" name="birth" type="date" class="input" [(ngModel)]="f.birthDate" /></div>
          <div><label class="form-label" for="r-sex">{{ 'READER.GENDER' | translate }}</label>
            <select id="r-sex" name="sex" class="input" [(ngModel)]="f.sex"><option [ngValue]="null">—</option>
              <option [ngValue]="1">{{ 'READER.GENDER_MALE' | translate }}</option><option [ngValue]="0">{{ 'READER.GENDER_FEMALE' | translate }}</option></select></div>
          <div><label class="form-label" for="r-course">{{ 'READER.COURSE' | translate }}</label>
            <select id="r-course" name="course" class="input" [(ngModel)]="f.courseId"><option [ngValue]="null">—</option>
              @for (o of c.courses(); track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }</select></div>
          <div><label class="form-label" for="r-class">{{ 'READER.ACADEMIC_CLASS' | translate }}</label>
            <select id="r-class" name="class" class="input" [(ngModel)]="f.classId"><option [ngValue]="null">—</option>
              @for (o of c.classes(); track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }</select></div>
          <div><label class="form-label" for="r-eth">{{ 'READER.ETHNIC' | translate }}</label>
            <select id="r-eth" name="eth" class="input" [(ngModel)]="f.ethnicityId"><option [ngValue]="null">—</option>
              @for (o of c.ethnicities(); track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }</select></div>
          <div><label class="form-label" for="r-degree">{{ 'READER.DEGREE' | translate }}</label>
            <select id="r-degree" name="degree" class="input" [(ngModel)]="f.degreeId"><option [ngValue]="null">—</option>
              @for (o of c.degrees(); track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }</select></div>
          <div><label class="form-label" for="r-prof">{{ 'READER.PROF' | translate }}</label>
            <select id="r-prof" name="prof" class="input" [(ngModel)]="f.academicTitleId"><option [ngValue]="null">—</option>
              @for (o of c.academicTitles(); track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }</select></div>
          <div class="sm:col-span-2"><label class="form-label" for="r-org">{{ 'READER.ORG' | translate }}</label>
            <select id="r-org" name="org" class="input" [(ngModel)]="f.orgId"><option [ngValue]="null">—</option>
              @for (o of c.orgOptions(); track o.id) { <option [ngValue]="o.id">{{ indent(o.depth) }}{{ o.name }}</option> }</select></div>
          <div><label class="form-label" for="r-email">{{ 'READER.EMAIL' | translate }}</label>
            <input id="r-email" name="email" type="email" class="input" [(ngModel)]="f.email" /></div>
          <div><label class="form-label" for="r-phone">{{ 'READER.PHONE' | translate }}</label>
            <input id="r-phone" name="phone" class="input" [(ngModel)]="f.phone" /></div>
          <div><label class="form-label" for="r-issue">{{ 'READER.ISSUED_DATE' | translate }}</label>
            <input id="r-issue" name="issue" type="date" class="input" [(ngModel)]="f.issueDate" /></div>
          <div><label class="form-label" for="r-exp">{{ 'READER.EXPIRED_DATE' | translate }}</label>
            <input id="r-exp" name="exp" type="date" class="input" [(ngModel)]="f.expireDate" />
            <p class="text-xs text-gray-500 mt-1">{{ 'READER.EXPIRE_EMPTY_HINT' | translate }}</p></div>
          <div class="sm:col-span-2 lg:col-span-3"><label class="form-label" for="r-addr">{{ 'READER.ADDRESS' | translate }}</label>
            <input id="r-addr" name="addr" class="input" [(ngModel)]="f.address" /></div>
          @if (c.groups().length) {
            <fieldset class="sm:col-span-2 lg:col-span-3">
              <legend class="form-label">{{ 'READER_HISTORY.GROUPS' | translate }}</legend>
              <div class="flex flex-wrap gap-x-4 gap-y-1 text-sm">
                @for (g of c.groups(); track g.id) {
                  <label class="flex items-center gap-2 cursor-pointer select-none">
                    <input type="checkbox" class="rounded border-gray-300" [checked]="groupIds().includes(g.id)" (change)="toggleGroup(g.id)" /> {{ g.name }}</label>
                }
              </div>
            </fieldset>
          }
          @if (f.publicId) {
            <div class="sm:col-span-2 lg:col-span-3"><label class="form-label" for="r-reason">{{ 'READER_HISTORY.REASON' | translate }}</label>
              <input id="r-reason" name="reason" class="input" maxlength="500" [(ngModel)]="reason" [placeholder]="'READER_HISTORY.REASON_PH' | translate" /></div>
          }
        </form>
      </div>
      <ng-container footer>
        @if (f.publicId) {
          <button type="button" class="btn-secondary mr-auto" (click)="history.emit()">
            <span class="material-icons text-[16px]">history</span> {{ 'READER_HISTORY.OPEN' | translate }}</button>
        }
        <button type="button" class="btn-secondary" (click)="closed.emit()">{{ 'COMMON.CANCEL' | translate }}</button>
        <button type="submit" form="reader-form" class="btn-primary" [disabled]="saving()">
          <span class="material-icons text-[16px]">save</span>{{ (f.publicId ? 'COMMON.UPDATE' : 'COMMON.SAVE') | translate }}</button>
      </ng-container>
    </app-modal>
  `,
})
export class ReaderFormDialog implements OnInit {
  /** null = thêm mới. */
  readonly reader = input<Reader | null>(null);
  readonly closed = output<void>();
  /** Đã lưu hồ sơ — đóng hộp thoại, tải lại danh sách. */
  readonly saved = output<void>();
  /** Ảnh thẻ đã đổi (lưu ngay) — chỉ tải lại danh sách. */
  readonly changed = output<void>();
  /** Mở lịch sử thay đổi của bạn đọc đang sửa. */
  readonly history = output<void>();

  protected readonly c = inject(ReaderCatalogs);
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  private readonly client: CrudClient<Reader> = this.api.crud<Reader>('readers', 'patron');

  protected f: ReaderForm = { ...BLANK };
  protected readonly saving = signal(false);
  protected readonly cardTaken = signal(false);
  protected readonly photoId = signal<string | null>(null);
  protected readonly photoUrl = signal<string | null>(null);
  protected readonly photoBusy = signal(false);
  /** Nhóm bạn đọc (bảng nối ở patron) — lưu sau hồ sơ, chỉ khi đổi. */
  protected readonly groupIds = signal<number[]>([]);
  private initialGroups: number[] = [];
  protected reason = '';

  ngOnInit(): void {
    const r = this.reader();
    if (!r) {
      const today = new Date();
      this.f = { ...BLANK, issueDate: iso(today), expireDate: iso(new Date(today.getFullYear() + 1, today.getMonth(), today.getDate())) };
      return;
    }
    const { id: _id, fullName: _fn, status: _s, lockReason: _l, createdAt: _c, photoId, ...rest } = r;
    this.f = { ...rest };
    this.photoId.set(photoId);
    if (photoId) void this.showPhoto(photoId);
    void this.loadGroups(r.publicId);
  }

  private async loadGroups(publicId: string): Promise<void> {
    try {
      this.initialGroups = await this.api.readerGroups(publicId);
      this.groupIds.set([...this.initialGroups]);
    } catch {
      /* không tải được nhóm — để trống, không ghi đè khi lưu */
      this.initialGroups = [];
    }
  }

  protected toggleGroup(id: number): void {
    this.groupIds.update((ids) => (ids.includes(id) ? ids.filter((x) => x !== id) : [...ids, id]));
  }

  protected indent(depth: number): string {
    return '   '.repeat(depth);
  }

  protected async checkCard(): Promise<void> {
    if (!this.f.cardNo.trim()) return;
    try {
      this.cardTaken.set((await this.api.cardNoExists(this.f.cardNo.trim(), this.f.publicId ?? undefined)).exists);
    } catch {
      this.cardTaken.set(false);
    }
  }

  protected async save(): Promise<void> {
    if (!this.f.cardNo.trim() || !this.f.firstName.trim()) {
      this.toastr.warning('READER.REQUIRED_CARD_NAME');
      return;
    }
    this.saving.set(true);
    try {
      const { publicId, ...body } = this.f;
      const payload = { ...body, status: publicId ? undefined : STATUS_ACTIVE };
      const saved = publicId ? await this.client.update(publicId, payload, this.reason) : await this.client.add(payload);
      const groups = [...this.groupIds()].sort((a, b) => a - b);
      if (groups.join() !== [...this.initialGroups].sort((a, b) => a - b).join())
        await this.api.setReaderGroups(saved.publicId, groups, this.reason);
      this.toastr.success(publicId ? 'READER.SAVED_UPDATE' : 'READER.SAVED_ADD');
      this.saved.emit();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.saving.set(false);
    }
  }

  protected async changePhoto(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    if (file.size > 2 * 1024 * 1024) {
      this.toastr.warning('READER.PHOTO_TOO_BIG');
      return;
    }
    this.photoBusy.set(true);
    try {
      const uploaded = await this.api.upload('reader-photo', file);
      await this.setPhoto(uploaded.id);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.photoBusy.set(false);
    }
  }

  protected async setPhoto(fileId: string | null): Promise<void> {
    const publicId = this.f.publicId;
    if (!publicId) return;
    this.photoBusy.set(true);
    try {
      const reader = await this.api.setReaderPhoto(publicId, fileId);
      this.photoId.set(reader.photoId);
      this.photoUrl.set(null);
      if (reader.photoId) await this.showPhoto(reader.photoId);
      this.toastr.success(fileId ? 'READER.PHOTO_UPDATED' : 'READER.PHOTO_DELETED');
      this.changed.emit();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.photoBusy.set(false);
    }
  }

  private async showPhoto(photoId: string): Promise<void> {
    try {
      const url = await this.api.fileUrl(photoId);
      if (this.photoId() === photoId) this.photoUrl.set(url);
    } catch {
      /* ảnh không còn ở media — giữ khung trống */
    }
  }
}

export function iso(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}
