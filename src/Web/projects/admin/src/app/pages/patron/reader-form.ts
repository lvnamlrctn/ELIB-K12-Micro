import { Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, CrudClient, ExtraPhoto, Reader, STATUS_ACTIVE, errorMessage } from '../../core/api';
import { CameraCapture, CapturedPhoto, shrinkImage } from '../../shared/camera';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { Modal } from '../../shared/ui';
import { ReaderCatalogs } from './reader-catalogs';
import { DateInput } from '../../shared/date-input';

type ReaderForm = Omit<Reader, 'id' | 'publicId' | 'fullName' | 'status' | 'lockReason' | 'createdAt' | 'photoId'> & { publicId: string | null };

const BLANK: ReaderForm = {
  publicId: null, cardNo: '', lastName: '', firstName: '', citizenId: null, cardUid: null, email: null, phone: null, address: null,
  birthDate: null, sex: null, readerTypeId: null, classId: null, courseId: null, orgId: null, degreeId: null, ethnicityId: null,
  academicTitleId: null, issueDate: null, expireDate: null,
};

/**
 * Thêm/sửa bạn đọc — các trường và thứ tự như form của ELIB-LRC (pages/admin/reader): mã thẻ, CCCD, UID thẻ chip, loại, họ, tên, ngày
 * sinh, giới tính, khoá, lớp, dân tộc, trình độ, học hàm, đơn vị, liên hệ, ngày cấp/hết hạn. Ảnh thẻ lưu ngay khi chọn (media, riêng tư).
 * Mật khẩu OPAC (ô Mật khẩu / Nhập lại như LRC) nằm ở identity — không đi cùng hồ sơ: đặt bằng API tài khoản bạn đọc sau khi lưu hồ sơ, chỉ
 * khi có nhập và cán bộ có quyền READERS:edit.
 * Khoá dịch giữ như ELIB-LRC: READER.FIRST_NAME là ô "Họ" (lastName), READER.LAST_NAME là ô "Tên" (firstName).
 */
@Component({
  selector: 'app-reader-form',
  imports: [DateInput, FormsModule, Modal, TranslatePipe, CameraCapture],
  template: `
    <app-modal [title]="f.publicId ? 'READER.EDIT_TITLE' : 'READER.ADD_TITLE'" widthClass="max-w-4xl" (closed)="closed.emit()">
      <div class="flex flex-col md:flex-row gap-6">
        <div class="md:w-40 shrink-0 text-sm text-gray-600 space-y-2">
          <button type="button" class="w-32 h-40 rounded-xl bg-gray-50 border-2 border-dashed border-gray-300 hover:border-blue-400 overflow-hidden flex items-center justify-center mx-auto md:mx-0 transition-colors"
                  [title]="'READER.CHOOSE_PHOTO' | translate" [disabled]="photoBusy()" (click)="fileInput.click()">
            @if (shownPhoto(); as src) { <img [src]="src" [alt]="'READER.AVATAR' | translate" class="w-full h-full object-cover" /> }
            @else { <span class="flex flex-col items-center text-gray-300"><span class="material-icons text-[48px]">person</span><span class="text-[10px]">{{ 'READER.CLICK_TO_CHOOSE' | translate }}</span></span> }
          </button>
          <input #fileInput type="file" accept="image/jpeg,image/png,image/webp" class="hidden" (change)="changePhoto($event)" />
          <button type="button" class="btn-secondary !py-1.5 !px-3 w-full justify-center" [disabled]="photoBusy()" (click)="camera.set('main')">
            <span class="material-icons text-[18px]">{{ photoBusy() ? 'autorenew' : 'photo_camera' }}</span> {{ 'READER.TAKE_PHOTO' | translate }}</button>
          @if (shownPhoto()) {
            <button type="button" class="btn-secondary !py-1.5 !px-3 !text-red-600 w-full justify-center" [disabled]="photoBusy()" (click)="clearPhoto()">
              <span class="material-icons text-[18px]">delete</span> {{ 'READER.DELETE_PHOTO' | translate }}</button>
          }
          <p class="text-xs text-gray-500">{{ (f.publicId ? 'READER.PHOTO_HINT' : 'READER.PHOTO_PENDING_HINT') | translate }}</p>
          @if (f.publicId) {
            <div class="pt-2 border-t border-gray-100">
              <p class="text-[10px] font-bold text-gray-400 uppercase tracking-wider mb-1.5">{{ 'READER.EXTRA_PHOTOS' | translate }}</p>
              <div class="flex flex-wrap gap-1.5">
                @for (p of extraPhotos(); track p.fileId) {
                  <div class="relative w-10 h-10 rounded-lg overflow-hidden border border-gray-200 group bg-gray-100">
                    @if (extraUrls()[p.fileId]; as u) { <img [src]="u" class="w-full h-full object-cover" alt="" /> }
                    <button type="button" class="absolute inset-0 bg-black/50 opacity-0 group-hover:opacity-100 focus:opacity-100 flex items-center justify-center transition-opacity"
                            [title]="'READER.DELETE_PHOTO' | translate" (click)="removeExtra(p.fileId)"><span class="material-icons text-white text-[16px]">close</span></button>
                  </div>
                }
                @if (extraPhotos().length < maxExtra) {
                  <button type="button" class="w-10 h-10 rounded-lg border-2 border-dashed border-gray-300 hover:border-blue-400 flex items-center justify-center text-gray-400"
                          [title]="'READER.EXTRA_FROM_FILE' | translate" [disabled]="photoBusy()" (click)="extraInput.click()"><span class="material-icons text-[18px]">add</span></button>
                  <button type="button" class="w-10 h-10 rounded-lg border-2 border-dashed border-gray-300 hover:border-blue-400 flex items-center justify-center text-gray-400"
                          [title]="'READER.EXTRA_FROM_CAMERA' | translate" [disabled]="photoBusy()" (click)="camera.set('extra')"><span class="material-icons text-[18px]">photo_camera</span></button>
                }
                <input #extraInput type="file" accept="image/jpeg,image/png,image/webp" class="hidden" (change)="addExtraFromFile($event)" />
              </div>
              <p class="text-[10px] text-gray-400 mt-1.5 leading-tight">{{ 'READER.EXTRA_PHOTOS_HINT' | translate: { max: maxExtra } }}</p>
            </div>
          }
        </div>
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
            <app-date-input fieldId="r-birth" name="birth" [(ngModel)]="f.birthDate"></app-date-input></div>
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
            <app-date-input fieldId="r-issue" name="issue" [(ngModel)]="f.issueDate"></app-date-input></div>
          <div><label class="form-label" for="r-exp">{{ 'READER.EXPIRED_DATE' | translate }}</label>
            <app-date-input fieldId="r-exp" name="exp" [(ngModel)]="f.expireDate"></app-date-input>
            <p class="text-xs text-gray-500 mt-1">{{ 'READER.EXPIRE_EMPTY_HINT' | translate }}</p></div>
          <div class="sm:col-span-2 lg:col-span-3"><label class="form-label" for="r-addr">{{ 'READER.ADDRESS' | translate }}</label>
            <input id="r-addr" name="addr" class="input" [(ngModel)]="f.address" /></div>
          @if (canSetPassword) {
            <div><label class="form-label" for="r-pw">{{ 'READER.PASSWORD' | translate }}</label>
              <input id="r-pw" name="pw" type="password" class="input" autocomplete="new-password" maxlength="128" [(ngModel)]="password"
                     [placeholder]="f.publicId ? '••••••••' : ''" /></div>
            <div><label class="form-label" for="r-pw2">{{ 'READER.CONFIRM_PASSWORD' | translate }}</label>
              <input id="r-pw2" name="pw2" type="password" class="input" autocomplete="new-password" maxlength="128" [(ngModel)]="confirm"
                     [placeholder]="f.publicId ? '••••••••' : ''" [class.!border-red-400]="!!confirm && confirm !== password" />
              @if (confirm && confirm !== password) { <p class="text-xs text-red-600 mt-1">{{ 'READER.PASSWORD_MISMATCH' | translate }}</p> }</div>
            <p class="text-xs text-gray-500 self-end pb-2">{{ (f.publicId ? 'READER.PASSWORD_EDIT_HINT' : 'READER.PASSWORD_ADD_HINT') | translate }}</p>
          }
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
    @if (camera()) {
      <app-camera-capture title="READER.CAMERA_TITLE" hint="READER.CAMERA_HINT" (captured)="onCaptured($event)" (cancelled)="camera.set(null)" />
    }
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
  protected readonly camera = signal<'main' | 'extra' | null>(null);
  protected readonly extraPhotos = signal<ExtraPhoto[]>([]);
  protected readonly extraUrls = signal<Record<string, string>>({});
  protected readonly maxExtra = 5;
  /** Thêm mới: ảnh đã chọn/chụp, upload sau khi hồ sơ được lưu (chưa có bạn đọc để gán ngay). */
  private readonly pendingFile = signal<File | null>(null);
  private readonly pendingUrl = signal<string | null>(null);
  protected readonly shownPhoto = computed(() => this.pendingUrl() ?? this.photoUrl());
  /** Nhóm bạn đọc (bảng nối ở patron) — lưu sau hồ sơ, chỉ khi đổi. */
  protected readonly groupIds = signal<number[]>([]);
  private initialGroups: number[] = [];
  protected reason = '';
  /** Mật khẩu OPAC — giữ riêng, không nằm trong payload hồ sơ gửi patron. */
  protected password = '';
  protected confirm = '';
  protected readonly canSetPassword = inject(Session).can('READERS:edit');

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
    void this.loadExtra(r.publicId);
  }

  private async loadExtra(publicId: string): Promise<void> {
    try {
      const list = await this.api.readerExtraPhotos(publicId);
      this.extraPhotos.set(list);
      for (const p of list) void this.showExtra(p.fileId);
    } catch {
      /* thiếu quyền / chưa tải được — để trống */
    }
  }

  private async showExtra(fileId: string): Promise<void> {
    try {
      const url = await this.api.fileUrl(fileId);
      this.extraUrls.update((u) => ({ ...u, [fileId]: url }));
    } catch {
      /* ảnh không còn ở media */
    }
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
    const password = this.canSetPassword ? this.password : '';
    if (password || this.confirm) {
      if (password.length < 6 || !password.trim()) {
        this.toastr.warning('READER.RESET_PASSWORD_MIN');
        return;
      }
      if (password !== this.confirm) {
        this.toastr.warning('READER.PASSWORD_MISMATCH');
        return;
      }
    }
    this.saving.set(true);
    try {
      const { publicId, ...body } = this.f;
      const payload = { ...body, status: publicId ? undefined : STATUS_ACTIVE };
      const saved = publicId ? await this.client.update(publicId, payload, this.reason) : await this.client.add(payload);
      const groups = [...this.groupIds()].sort((a, b) => a - b);
      if (groups.join() !== [...this.initialGroups].sort((a, b) => a - b).join())
        await this.api.setReaderGroups(saved.publicId, groups, this.reason);
      const pending = this.pendingFile();
      if (!publicId && pending) {
        try {
          const uploaded = await this.api.upload('reader-photo', pending);
          await this.api.setReaderPhoto(saved.publicId, uploaded.id);
        } catch (e) {
          this.toastr.error(errorMessage(e)); // hồ sơ đã lưu — chỉ ảnh lỗi, thêm lại khi sửa bạn đọc
        }
      }
      if (password) {
        try {
          await this.api.setReaderPassword(saved.publicId, password, false);
        } catch (e) {
          // Hồ sơ đã lưu — chỉ mật khẩu lỗi, đặt lại ở nút Mật khẩu trên danh sách.
          this.toastr.warning('READER.PASSWORD_NOT_SAVED');
          this.toastr.error(errorMessage(e));
        }
      }
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
    await this.useMainPhoto(file);
  }

  private async useMainPhoto(picked: File): Promise<void> {
    const file = await shrinkImage(picked);
    if (file.size > 2 * 1024 * 1024) {
      this.toastr.warning('READER.PHOTO_TOO_BIG');
      return;
    }
    if (!this.f.publicId) {
      this.setPending(file);
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

  private setPending(file: File | null): void {
    const old = this.pendingUrl();
    if (old) URL.revokeObjectURL(old);
    this.pendingFile.set(file);
    this.pendingUrl.set(file ? URL.createObjectURL(file) : null);
  }

  protected clearPhoto(): void {
    if (this.pendingFile()) this.setPending(null);
    else void this.setPhoto(null);
  }

  protected async onCaptured(p: CapturedPhoto): Promise<void> {
    const target = this.camera();
    this.camera.set(null);
    if (target === 'extra') await this.addExtra(p.file);
    else await this.useMainPhoto(p.file);
  }

  protected async addExtraFromFile(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (file) await this.addExtra(file);
  }

  private async addExtra(picked: File): Promise<void> {
    const publicId = this.f.publicId;
    if (!publicId) return;
    const file = await shrinkImage(picked);
    if (file.size > 2 * 1024 * 1024) {
      this.toastr.warning('READER.PHOTO_TOO_BIG');
      return;
    }
    this.photoBusy.set(true);
    try {
      const uploaded = await this.api.upload('reader-photo', file);
      const added = await this.api.addReaderExtraPhoto(publicId, uploaded.id);
      this.extraPhotos.update((l) => [...l, added]);
      void this.showExtra(added.fileId);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.photoBusy.set(false);
    }
  }

  protected async removeExtra(fileId: string): Promise<void> {
    const publicId = this.f.publicId;
    if (!publicId) return;
    try {
      await this.api.removeReaderExtraPhoto(publicId, fileId);
      this.extraPhotos.update((l) => l.filter((p) => p.fileId !== fileId));
    } catch (e) {
      this.toastr.error(errorMessage(e));
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
