import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api, CrudClient, CrudPage, NamedItem, Org, Reader, ReaderSearch, STATUS_ACTIVE, errorMessage } from '../../core/api';
import { Session } from '../../core/session';
import { ImportDialog } from '../../shared/import-dialog';
import { ToastrService } from '../../shared/toastr';
import { ConfirmDelete, Loading, Modal, Paginator, StatusBadge } from '../../shared/ui';
import { ReaderExport, ReaderPhotos } from './reader-tools';

type ReaderForm = Omit<Reader, 'id' | 'publicId' | 'fullName' | 'status' | 'lockReason' | 'createdAt' | 'photoId'> & { publicId: string | null };

const BLANK: ReaderForm = {
  publicId: null, cardNo: '', lastName: '', firstName: '', citizenId: null, cardUid: null, email: null, phone: null, address: null,
  birthDate: null, sex: null, readerTypeId: null, classId: null, courseId: null, orgId: null, degreeId: null, ethnicityId: null,
  academicTitleId: null, issueDate: null, expireDate: null,
};

/** Bạn đọc (monolith: pages/admin/reader) — tìm theo bộ lọc, thêm/sửa, ảnh thẻ, khoá/mở thẻ, sửa hàng loạt, nhập/xuất Excel. */
@Component({
  selector: 'app-readers',
  imports: [FormsModule, DatePipe, Loading, Modal, Paginator, StatusBadge, ConfirmDelete, ImportDialog, ReaderExport, ReaderPhotos],
  template: `
    <div class="mb-5"><h4 class="page-title">Bạn đọc</h4></div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-3 xl:grid-cols-6 gap-4">
        <div class="xl:col-span-2">
          <label for="kw" class="field-label">Số thẻ / họ tên / email / điện thoại</label>
          <input id="kw" class="input" [(ngModel)]="search.keyword" (keydown.enter)="find()" placeholder="Nhập từ khoá..." />
        </div>
        <div>
          <label for="f-type" class="field-label">Loại bạn đọc</label>
          <select id="f-type" class="input" [(ngModel)]="search.readerTypeId" (change)="find()">
            <option [ngValue]="null">Tất cả</option>
            @for (t of types(); track t.id) { <option [ngValue]="t.id">{{ t.name }}</option> }
          </select>
        </div>
        <div>
          <label for="f-class" class="field-label">Lớp</label>
          <select id="f-class" class="input" [(ngModel)]="search.classId" (change)="find()">
            <option [ngValue]="null">Tất cả</option>
            @for (c of classes(); track c.id) { <option [ngValue]="c.id">{{ c.name }}</option> }
          </select>
        </div>
        <div>
          <label for="f-status" class="field-label">Trạng thái thẻ</label>
          <select id="f-status" class="input" [(ngModel)]="search.status" (change)="find()">
            <option [ngValue]="null">Tất cả</option>
            <option [ngValue]="2">Hoạt động</option>
            <option [ngValue]="1">Bị khoá</option>
          </select>
        </div>
        <div>
          <label for="f-exp" class="field-label">Hạn thẻ</label>
          <select id="f-exp" class="input" [(ngModel)]="search.expired" (change)="find()">
            <option [ngValue]="null">Tất cả</option>
            <option [ngValue]="false">Còn hạn</option>
            <option [ngValue]="true">Đã hết hạn</option>
          </select>
        </div>
      </div>
      <div class="flex justify-end mt-4">
        <button (click)="find()" class="btn-primary"><span class="material-icons text-[18px]">search</span> Tìm kiếm</button>
      </div>
    </div>

    <div class="panel flex gap-2 items-center justify-between flex-wrap">
      <div class="flex gap-2 flex-wrap">
        @if (can('add')) {
          <button (click)="openForm(null)" class="btn-add"><span class="material-icons text-[18px]">add</span> Thêm bạn đọc</button>
          <button (click)="importing.set(true)" class="btn-secondary !py-1.5 !px-3"><span class="material-icons text-[18px]">upload_file</span> Nhập Excel</button>
        }
        <button (click)="exporting.set(true)" class="btn-secondary !py-1.5 !px-3"><span class="material-icons text-[18px]">download</span> Xuất Excel</button>
        @if (can('edit')) {
          <button (click)="uploadingPhotos.set(true)" class="btn-secondary !py-1.5 !px-3"><span class="material-icons text-[18px]">photo_library</span> Tải ảnh hàng loạt</button>
          <button (click)="openBulk()" [disabled]="selected().size === 0" class="btn-secondary !py-1.5 !px-3">
            <span class="material-icons text-[18px]">tune</span> Sửa hàng loạt ({{ selected().size }})
          </button>
        }
      </div>
      <span class="text-sm text-gray-500">{{ page()?.totalCount ?? 0 }} bạn đọc</span>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse min-w-[1000px]">
          <thead>
            <tr>
              <th class="th w-10 text-center"><input type="checkbox" class="rounded border-gray-300" [checked]="allSelected()" (change)="toggleAll()" /></th>
              <th class="th w-32">Số thẻ</th>
              <th class="th">Họ và tên</th>
              <th class="th w-24">Giới tính</th>
              <th class="th w-28">Ngày sinh</th>
              <th class="th w-36">Loại bạn đọc</th>
              <th class="th w-24">Lớp</th>
              <th class="th w-28">Hạn thẻ</th>
              <th class="th w-32 !text-center">Trạng thái</th>
              <th class="th w-32 !text-center">Thao tác</th>
            </tr>
          </thead>
          <tbody>
            @for (r of page()?.items ?? []; track r.publicId) {
              <tr class="hover:bg-gray-50/50">
                <td class="td text-center"><input type="checkbox" class="rounded border-gray-300" [checked]="selected().has(r.publicId)" (change)="toggle(r)" /></td>
                <td class="td font-mono text-[13px]">{{ r.cardNo }}</td>
                <td class="td font-medium text-gray-800">{{ r.fullName }}
                  @if (r.lockReason) { <div class="text-[11px] text-red-600">Khoá: {{ r.lockReason }}</div> }
                </td>
                <td class="td">{{ r.sex === 1 ? 'Nam' : r.sex === 0 ? 'Nữ' : '—' }}</td>
                <td class="td">{{ r.birthDate ? (r.birthDate | date: 'dd/MM/yyyy') : '—' }}</td>
                <td class="td">{{ name(types(), r.readerTypeId) }}</td>
                <td class="td">{{ name(classes(), r.classId) }}</td>
                <td class="td" [class.text-red-600]="isExpired(r)" [title]="isExpired(r) ? 'Thẻ đã hết hạn' : ''">
                  {{ r.expireDate ? (r.expireDate | date: 'dd/MM/yyyy') : 'Không hạn' }}
                </td>
                <td class="td text-center"><app-status-badge [status]="r.status" inactiveText="Bị khoá" /></td>
                <td class="td">
                  <div class="flex items-center justify-center gap-1.5">
                    @if (can('edit')) {
                      <button (click)="openForm(r)" class="icon-btn bg-blue-50 text-blue-600 hover:bg-blue-100" title="Sửa"><span class="material-icons text-[18px]">edit</span></button>
                      @if (r.status === 2) {
                        <button (click)="locking.set(r); lockReason = ''" class="icon-btn bg-amber-50 text-amber-600 hover:bg-amber-100" title="Khoá thẻ"><span class="material-icons text-[18px]">lock</span></button>
                      } @else {
                        <button (click)="unlock(r)" class="icon-btn bg-green-50 text-green-600 hover:bg-green-100" title="Mở khoá thẻ"><span class="material-icons text-[18px]">lock_open</span></button>
                      }
                    }
                    @if (can('delete')) {
                      <button (click)="deleting.set(r)" class="icon-btn bg-red-50 text-red-600 hover:bg-red-100" title="Xoá"><span class="material-icons text-[18px]">delete</span></button>
                    }
                  </div>
                </td>
              </tr>
            } @empty {
              <tr><td colspan="10" class="py-8 px-4 text-center text-gray-500">{{ loading() ? '' : 'Không có bạn đọc nào' }}</td></tr>
            }
          </tbody>
        </table>
      </div>
      @if (page(); as p) {
        <app-paginator [total]="p.totalCount" [pageIndex]="search.pageIndex!" [pageSize]="search.pageSize!" (pageChange)="onPage($event)" />
      }
    </div>

    @if (form(); as f) {
      <app-modal [title]="f.publicId ? 'Cập nhật bạn đọc' : 'Thêm bạn đọc'" widthClass="max-w-3xl" (closed)="form.set(null)">
        @if (photo(); as ph) {
          <div class="flex items-center gap-4 mb-5 pb-5 border-b border-gray-100">
            <div class="w-24 h-32 rounded-lg bg-gray-100 border border-gray-200 overflow-hidden flex items-center justify-center shrink-0">
              @if (ph.url) { <img [src]="ph.url" alt="Ảnh thẻ" class="w-full h-full object-cover" /> }
              @else { <span class="material-icons text-gray-300 text-[48px]">person</span> }
            </div>
            <div class="text-sm text-gray-600 space-y-2">
              <div class="font-medium text-gray-800">Ảnh thẻ</div>
              <div class="flex gap-2">
                <label class="btn-secondary !py-1.5 !px-3 cursor-pointer" [class.opacity-50]="photoBusy()">
                  <span class="material-icons text-[18px]">{{ photoBusy() ? 'autorenew' : 'photo_camera' }}</span> {{ ph.photoId ? 'Đổi ảnh' : 'Chọn ảnh' }}
                  <input type="file" accept="image/jpeg,image/png,image/webp" class="hidden" [disabled]="photoBusy()" (change)="changePhoto($event)" />
                </label>
                @if (ph.photoId) {
                  <button type="button" class="btn-secondary !py-1.5 !px-3 !text-red-600" [disabled]="photoBusy()" (click)="setPhoto(null)">
                    <span class="material-icons text-[18px]">delete</span> Xoá ảnh</button>
                }
              </div>
              <p class="text-xs text-gray-500">JPG, PNG, WEBP, tối đa 2 MB. Ảnh được lưu ngay, không cần bấm Cập nhật.</p>
            </div>
          </div>
        }
        <form id="reader-form" (ngSubmit)="save(f)" class="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <div><label class="form-label" for="r-card">Số thẻ <span class="text-red-500">*</span></label>
            <input id="r-card" name="card" class="input font-mono uppercase" [(ngModel)]="f.cardNo" required (blur)="checkCard(f)" />
            @if (cardTaken()) { <p class="text-xs text-red-600 mt-1">Số thẻ đã có.</p> }</div>
          <div><label class="form-label" for="r-last">Họ đệm</label><input id="r-last" name="last" class="input" [(ngModel)]="f.lastName" /></div>
          <div><label class="form-label" for="r-first">Tên <span class="text-red-500">*</span></label><input id="r-first" name="first" class="input" [(ngModel)]="f.firstName" required /></div>
          <div><label class="form-label" for="r-sex">Giới tính</label>
            <select id="r-sex" name="sex" class="input" [(ngModel)]="f.sex"><option [ngValue]="null">—</option><option [ngValue]="1">Nam</option><option [ngValue]="0">Nữ</option></select></div>
          <div><label class="form-label" for="r-birth">Ngày sinh</label><input id="r-birth" name="birth" type="date" class="input" [(ngModel)]="f.birthDate" /></div>
          <div><label class="form-label" for="r-cid">Số CCCD</label><input id="r-cid" name="cid" class="input" [(ngModel)]="f.citizenId" /></div>
          <div><label class="form-label" for="r-type">Loại bạn đọc</label>
            <select id="r-type" name="type" class="input" [(ngModel)]="f.readerTypeId"><option [ngValue]="null">—</option>
              @for (t of types(); track t.id) { <option [ngValue]="t.id">{{ t.name }}</option> }</select></div>
          <div><label class="form-label" for="r-class">Lớp</label>
            <select id="r-class" name="class" class="input" [(ngModel)]="f.classId"><option [ngValue]="null">—</option>
              @for (c of classes(); track c.id) { <option [ngValue]="c.id">{{ c.name }}</option> }</select></div>
          <div><label class="form-label" for="r-course">Khoá</label>
            <select id="r-course" name="course" class="input" [(ngModel)]="f.courseId"><option [ngValue]="null">—</option>
              @for (c of courses(); track c.id) { <option [ngValue]="c.id">{{ c.name }}</option> }</select></div>
          <div><label class="form-label" for="r-org">Phòng ban</label>
            <select id="r-org" name="org" class="input" [(ngModel)]="f.orgId"><option [ngValue]="null">—</option>
              @for (o of orgs(); track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }</select></div>
          <div><label class="form-label" for="r-email">Email</label><input id="r-email" name="email" type="email" class="input" [(ngModel)]="f.email" /></div>
          <div><label class="form-label" for="r-phone">Điện thoại</label><input id="r-phone" name="phone" class="input" [(ngModel)]="f.phone" /></div>
          <div class="sm:col-span-3"><label class="form-label" for="r-addr">Địa chỉ</label><input id="r-addr" name="addr" class="input" [(ngModel)]="f.address" /></div>
          <div><label class="form-label" for="r-issue">Ngày cấp thẻ</label><input id="r-issue" name="issue" type="date" class="input" [(ngModel)]="f.issueDate" /></div>
          <div><label class="form-label" for="r-exp">Ngày hết hạn</label><input id="r-exp" name="exp" type="date" class="input" [(ngModel)]="f.expireDate" />
            <p class="text-xs text-gray-500 mt-1">Bỏ trống = thẻ không thời hạn.</p></div>
          <div><label class="form-label" for="r-uid">UID thẻ chip/RFID</label><input id="r-uid" name="uid" class="input font-mono" [(ngModel)]="f.cardUid" /></div>
        </form>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="form.set(null)">Huỷ</button>
          <button type="submit" form="reader-form" class="btn-primary" [disabled]="saving()"><span class="material-icons text-[16px]">save</span>{{ f.publicId ? 'Cập nhật' : 'Lưu' }}</button>
        </ng-container>
      </app-modal>
    }

    @if (locking(); as r) {
      <app-modal title="Khoá thẻ bạn đọc" (closed)="locking.set(null)">
        <p class="text-sm text-gray-600 mb-3">Khoá thẻ <b>{{ r.cardNo }}</b> — {{ r.fullName }}. Bạn đọc bị khoá không mượn được tài liệu.</p>
        <label class="form-label" for="lock-reason">Lý do</label>
        <textarea id="lock-reason" rows="3" class="input" [(ngModel)]="lockReason" placeholder="VD: mất thẻ, vi phạm nội quy..."></textarea>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="locking.set(null)">Huỷ</button>
          <button type="button" class="btn-danger" [disabled]="saving()" (click)="lock(r)"><span class="material-icons text-[18px]">lock</span>Khoá thẻ</button>
        </ng-container>
      </app-modal>
    }

    @if (bulk(); as b) {
      <app-modal [title]="'Sửa hàng loạt ' + selected().size + ' bạn đọc'" (closed)="bulk.set(null)">
        <p class="text-xs text-gray-500 mb-3">Chỉ các trường được chọn mới thay đổi; để trống thì giữ nguyên.</p>
        <div class="grid grid-cols-2 gap-4">
          <div><label class="form-label" for="b-type">Loại bạn đọc</label>
            <select id="b-type" class="input" [(ngModel)]="b.readerTypeId"><option [ngValue]="null">— giữ nguyên —</option>
              @for (t of types(); track t.id) { <option [ngValue]="t.id">{{ t.name }}</option> }</select></div>
          <div><label class="form-label" for="b-class">Lớp</label>
            <select id="b-class" class="input" [(ngModel)]="b.classId"><option [ngValue]="null">— giữ nguyên —</option>
              @for (c of classes(); track c.id) { <option [ngValue]="c.id">{{ c.name }}</option> }</select></div>
          <div><label class="form-label" for="b-course">Khoá</label>
            <select id="b-course" class="input" [(ngModel)]="b.courseId"><option [ngValue]="null">— giữ nguyên —</option>
              @for (c of courses(); track c.id) { <option [ngValue]="c.id">{{ c.name }}</option> }</select></div>
          <div><label class="form-label" for="b-status">Trạng thái thẻ</label>
            <select id="b-status" class="input" [(ngModel)]="b.status"><option [ngValue]="null">— giữ nguyên —</option>
              <option [ngValue]="2">Hoạt động</option><option [ngValue]="1">Bị khoá</option></select></div>
          <div><label class="form-label" for="b-issue">Ngày cấp thẻ</label><input id="b-issue" type="date" class="input" [(ngModel)]="b.issueDate" /></div>
          <div><label class="form-label" for="b-exp">Ngày hết hạn</label><input id="b-exp" type="date" class="input" [(ngModel)]="b.expireDate" /></div>
        </div>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="bulk.set(null)">Huỷ</button>
          <button type="button" class="btn-primary" [disabled]="saving()" (click)="applyBulk(b)"><span class="material-icons text-[18px]">done_all</span>Cập nhật</button>
        </ng-container>
      </app-modal>
    }

    @if (deleting(); as r) {
      <app-confirm-delete [busy]="saving()" [message]="'Xoá bạn đọc ' + r.cardNo + ' — ' + r.fullName + '?'" (confirmed)="remove(r)" (cancelled)="deleting.set(null)" />
    }

    @if (exporting()) {
      <app-reader-export [search]="search" [total]="page()?.totalCount ?? 0" (closed)="exporting.set(false)" />
    }

    @if (uploadingPhotos()) {
      <app-reader-photos (closed)="uploadingPhotos.set(false)" (uploaded)="load()" />
    }

    @if (importing()) {
      <app-import-dialog title="Bạn đọc" resource="readers" service="patron" (closed)="importing.set(false)" (imported)="find()" />
    }
  `,
})
export class Readers implements OnInit {
  private readonly api = inject(Api);
  private readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);
  private readonly client: CrudClient<Reader> = this.api.crud<Reader>('readers', 'patron');

  protected search: ReaderSearch = { keyword: '', readerTypeId: null, classId: null, status: null, expired: null, pageIndex: 1, pageSize: 10 };
  protected readonly page = signal<CrudPage<Reader> | null>(null);
  protected readonly types = signal<NamedItem[]>([]);
  protected readonly classes = signal<NamedItem[]>([]);
  protected readonly courses = signal<NamedItem[]>([]);
  protected readonly orgs = signal<Org[]>([]);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly selected = signal<Set<string>>(new Set());
  protected readonly form = signal<ReaderForm | null>(null);
  protected readonly cardTaken = signal(false);
  protected readonly locking = signal<Reader | null>(null);
  protected readonly deleting = signal<Reader | null>(null);
  protected readonly importing = signal(false);
  protected readonly exporting = signal(false);
  protected readonly uploadingPhotos = signal(false);
  /** Ảnh thẻ của bạn đọc đang sửa (url: link ký có hạn từ media). */
  protected readonly photo = signal<{ publicId: string; photoId: string | null; url: string | null } | null>(null);
  protected readonly photoBusy = signal(false);
  protected readonly bulk = signal<{ readerTypeId: number | null; classId: number | null; courseId: number | null; status: number | null; issueDate: string | null; expireDate: string | null } | null>(null);
  protected lockReason = '';

  protected readonly allSelected = computed(() => {
    const items = this.page()?.items ?? [];
    return items.length > 0 && items.every((r) => this.selected().has(r.publicId));
  });

  async ngOnInit(): Promise<void> {
    void this.load();
    const catalogs = await Promise.allSettled([
      this.api.crud<NamedItem>('reader-types', 'patron').searchAll(),
      this.api.crud<NamedItem>('classes', 'patron').searchAll(),
      this.api.crud<NamedItem>('courses', 'patron').searchAll(),
      this.api.crud<Org>('orgs').searchAll(),
    ]);
    // Thiếu quyền xem một danh mục thì ô chọn trống — không chặn màn bạn đọc.
    const [types, classes, courses, orgs] = catalogs.map((r) => (r.status === 'fulfilled' ? r.value : []));
    this.types.set(types as NamedItem[]);
    this.classes.set(classes as NamedItem[]);
    this.courses.set(courses as NamedItem[]);
    this.orgs.set(orgs as Org[]);
  }

  protected can(action: string): boolean {
    return this.session.can(`READERS:${action}`);
  }

  protected name(items: { id: number; name: string }[], id: number | null): string {
    return id == null ? '—' : (items.find((i) => i.id === id)?.name ?? '—');
  }

  protected isExpired(r: Reader): boolean {
    return !!r.expireDate && r.expireDate < new Date().toISOString().slice(0, 10);
  }

  protected find(): void {
    this.search.pageIndex = 1;
    this.selected.set(new Set());
    void this.load();
  }

  protected onPage(e: { pageIndex: number; pageSize: number }): void {
    this.search.pageIndex = e.pageIndex;
    this.search.pageSize = e.pageSize;
    void this.load();
  }

  protected toggle(r: Reader): void {
    const next = new Set(this.selected());
    if (next.has(r.publicId)) next.delete(r.publicId);
    else next.add(r.publicId);
    this.selected.set(next);
  }

  protected toggleAll(): void {
    this.selected.set(this.allSelected() ? new Set() : new Set((this.page()?.items ?? []).map((r) => r.publicId)));
  }

  protected openForm(r: Reader | null): void {
    this.cardTaken.set(false);
    this.photo.set(r ? { publicId: r.publicId, photoId: r.photoId, url: null } : null);
    if (r?.photoId) void this.showPhoto(r.publicId, r.photoId);
    if (!r) {
      const today = new Date();
      const nextYear = new Date(today.getFullYear() + 1, today.getMonth(), today.getDate());
      this.form.set({ ...BLANK, issueDate: iso(today), expireDate: iso(nextYear) });
      return;
    }
    const { id: _id, fullName: _fn, status: _s, lockReason: _l, createdAt: _c, photoId: _ph, ...rest } = r;
    this.form.set({ ...rest });
  }

  protected async checkCard(f: ReaderForm): Promise<void> {
    if (!f.cardNo.trim()) return;
    try {
      this.cardTaken.set((await this.api.cardNoExists(f.cardNo.trim(), f.publicId ?? undefined)).exists);
    } catch {
      this.cardTaken.set(false);
    }
  }

  protected async save(f: ReaderForm): Promise<void> {
    if (!f.cardNo.trim() || !f.firstName.trim()) {
      this.toastr.warning('Vui lòng nhập số thẻ và tên bạn đọc.');
      return;
    }
    this.saving.set(true);
    try {
      const { publicId, ...body } = f;
      const payload = { ...body, status: publicId ? undefined : STATUS_ACTIVE };
      if (publicId) await this.client.update(publicId, payload);
      else await this.client.add(payload);
      this.toastr.success(publicId ? 'Đã cập nhật bạn đọc.' : 'Đã thêm bạn đọc.');
      this.form.set(null);
      await this.load();
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
      this.toastr.warning('Ảnh tối đa 2 MB.');
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
    const current = this.photo();
    if (!current) return;
    this.photoBusy.set(true);
    try {
      const reader = await this.api.setReaderPhoto(current.publicId, fileId);
      this.photo.set({ publicId: reader.publicId, photoId: reader.photoId, url: null });
      if (reader.photoId) await this.showPhoto(reader.publicId, reader.photoId);
      this.toastr.success(fileId ? 'Đã cập nhật ảnh thẻ.' : 'Đã xoá ảnh thẻ.');
      void this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.photoBusy.set(false);
    }
  }

  private async showPhoto(publicId: string, photoId: string): Promise<void> {
    try {
      const url = await this.api.fileUrl(photoId);
      // Người dùng có thể đã mở bạn đọc khác trong lúc chờ.
      if (this.photo()?.publicId === publicId) this.photo.set({ publicId, photoId, url });
    } catch {
      /* ảnh không còn ở media — giữ khung trống */
    }
  }

  protected lock(r: Reader): Promise<void> {
    return this.run(() => this.api.lockReader(r.publicId, this.lockReason.trim() || null), 'Đã khoá thẻ.', () => this.locking.set(null));
  }

  protected unlock(r: Reader): Promise<void> {
    return this.run(() => this.api.unlockReader(r.publicId), 'Đã mở khoá thẻ.');
  }

  protected remove(r: Reader): Promise<void> {
    return this.run(() => this.client.delete(r.publicId), 'Đã xoá bạn đọc.', () => this.deleting.set(null));
  }

  protected openBulk(): void {
    this.bulk.set({ readerTypeId: null, classId: null, courseId: null, status: null, issueDate: null, expireDate: null });
  }

  protected applyBulk(b: NonNullable<ReturnType<typeof this.bulk>>): Promise<void> {
    return this.run(async () => {
      const { updatedCount } = await this.api.bulkUpdateReaders({ publicIds: [...this.selected()], ...b, issueDate: b.issueDate || null, expireDate: b.expireDate || null });
      this.toastr.success(`Đã cập nhật ${updatedCount} bạn đọc.`);
      this.selected.set(new Set());
    }, null, () => this.bulk.set(null));
  }

  private async run(action: () => Promise<unknown>, success: string | null, after?: () => void): Promise<void> {
    this.saving.set(true);
    try {
      await action();
      if (success) this.toastr.success(success);
      after?.();
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.saving.set(false);
    }
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.page.set(await this.client.search({ ...this.search, keyword: this.search.keyword?.trim() || undefined }) as unknown as CrudPage<Reader>);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}

function iso(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}
