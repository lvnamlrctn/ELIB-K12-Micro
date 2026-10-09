import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Api, ModuleDef, errorMessage } from '../../core/api';
import { ToastrService } from '../../shared/toastr';
import { ModulePicker } from './module-picker';

@Component({
  selector: 'app-tenant-create',
  imports: [FormsModule, RouterLink, ModulePicker],
  template: `
    <div class="mb-5 flex items-center gap-3">
      <a routerLink="/tenant" class="icon-btn bg-white border border-gray-200 text-gray-600 hover:bg-gray-50"><span class="material-icons text-[18px]">arrow_back</span></a>
      <h4 class="page-title">Tạo đơn vị</h4>
    </div>

    <form (ngSubmit)="submit()" class="max-w-5xl">
      <div class="bg-white rounded-xl shadow-sm border border-gray-100 mb-5">
        <div class="px-6 py-4 border-b border-gray-100 font-bold text-gray-800">Thông tin đơn vị</div>
        <div class="p-6 grid grid-cols-1 md:grid-cols-3 gap-4">
          <div>
            <label class="form-label" for="code">Mã đơn vị <span class="text-red-500">*</span></label>
            <input id="code" name="code" class="input font-mono" [(ngModel)]="code" required placeholder="TH-NGUYENDU" />
            <p class="text-xs text-gray-500 mt-1">Chữ in hoa, số, '-'. Không đổi được sau khi tạo.</p>
          </div>
          <div>
            <label class="form-label" for="name">Tên đơn vị <span class="text-red-500">*</span></label>
            <input id="name" name="name" class="input" [(ngModel)]="name" required placeholder="Trường Tiểu học Nguyễn Du" />
          </div>
          <div>
            <label class="form-label" for="sub">Tên miền con <span class="text-red-500">*</span></label>
            <input id="sub" name="sub" class="input font-mono" [(ngModel)]="subdomain" required placeholder="th-nguyendu" />
            <p class="text-xs text-gray-500 mt-1">{{ subdomain || '…' }}.{{ baseDomain }}</p>
          </div>
        </div>
      </div>

      <div class="bg-white rounded-xl shadow-sm border border-gray-100 mb-5">
        <div class="px-6 py-4 border-b border-gray-100 font-bold text-gray-800">Phân hệ bán cho đơn vị</div>
        <div class="p-6">
          @if (modules().length) {
            <app-module-picker [modules]="modules()" [(selected)]="selected" />
          } @else {
            <p class="text-sm text-gray-500">Đang tải danh mục phân hệ…</p>
          }
        </div>
      </div>

      <div class="flex justify-end gap-3">
        <a routerLink="/tenant" class="btn-secondary bg-white">Huỷ</a>
        <button type="submit" [disabled]="busy()" class="btn-primary !px-6"><span class="material-icons text-[18px]">save</span> Tạo đơn vị</button>
      </div>
    </form>
  `,
})
export class TenantCreate implements OnInit {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly toastr = inject(ToastrService);
  protected readonly baseDomain = location.hostname;

  protected code = '';
  protected name = '';
  protected subdomain = '';
  protected selected: string[] = [];
  protected readonly modules = signal<ModuleDef[]>([]);
  protected readonly busy = signal(false);

  async ngOnInit(): Promise<void> {
    try {
      this.modules.set(await this.api.modules());
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected async submit(): Promise<void> {
    this.busy.set(true);
    try {
      const created = await this.api.createTenant({
        code: this.code.trim(),
        name: this.name.trim(),
        subdomain: this.subdomain.trim(),
        licenses: this.selected.map((moduleCode) => ({ moduleCode })),
      });
      this.toastr.success(`Đã tạo đơn vị ${created.code}. Đang khởi tạo…`);
      await this.router.navigate(['/tenant', created.publicId]);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
