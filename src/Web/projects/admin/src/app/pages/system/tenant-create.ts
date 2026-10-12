import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Api, ModuleDef, errorMessage } from '../../core/api';
import { TranslatePipe } from '@ngx-translate/core';
import { ToastrService } from '../../shared/toastr';
import { ModulePicker } from './module-picker';

@Component({
  selector: 'app-tenant-create',
  imports: [FormsModule, RouterLink, ModulePicker, TranslatePipe],
  template: `
    <div class="mb-5 flex items-center gap-3">
      <a routerLink="/tenant" class="icon-btn bg-white border border-gray-200 text-gray-600 hover:bg-gray-50"><span class="material-icons text-[18px]">arrow_back</span></a>
      <h4 class="page-title">{{ 'TENANT_ADMIN.CREATE' | translate }}</h4>
    </div>

    <form (ngSubmit)="submit()" class="max-w-5xl">
      <div class="bg-white rounded-xl shadow-sm border border-gray-100 mb-5">
        <div class="px-6 py-4 border-b border-gray-100 font-bold text-gray-800">{{ 'TENANT_ADMIN.INFO' | translate }}</div>
        <div class="p-6 grid grid-cols-1 md:grid-cols-3 gap-4">
          <div>
            <label class="form-label" for="code">{{ 'TENANT_ADMIN.CODE' | translate }} <span class="text-red-500">*</span></label>
            <input id="code" name="code" class="input font-mono" [(ngModel)]="code" required [placeholder]="'TENANT_ADMIN.CODE_PH' | translate" />
            <p class="text-xs text-gray-500 mt-1">{{ 'TENANT_ADMIN.CODE_HINT' | translate }}</p>
          </div>
          <div>
            <label class="form-label" for="name">{{ 'TENANT_ADMIN.NAME' | translate }} <span class="text-red-500">*</span></label>
            <input id="name" name="name" class="input" [(ngModel)]="name" required [placeholder]="'TENANT_ADMIN.NAME_PH' | translate" />
          </div>
          <div>
            <label class="form-label" for="sub">{{ 'TENANT_ADMIN.SUBDOMAIN' | translate }} <span class="text-red-500">*</span></label>
            <input id="sub" name="sub" class="input font-mono" [(ngModel)]="subdomain" required placeholder="th-nguyendu" />
            <p class="text-xs text-gray-500 mt-1">{{ subdomain || '…' }}.{{ baseDomain }}</p>
          </div>
        </div>
      </div>

      <div class="bg-white rounded-xl shadow-sm border border-gray-100 mb-5">
        <div class="px-6 py-4 border-b border-gray-100 font-bold text-gray-800">{{ 'TENANT_ADMIN.MODULES_SOLD' | translate }}</div>
        <div class="p-6">
          @if (modules().length) {
            <app-module-picker [modules]="modules()" [(selected)]="selected" />
          } @else {
            <p class="text-sm text-gray-500">{{ 'TENANT_ADMIN.MODULES_LOADING' | translate }}</p>
          }
        </div>
      </div>

      <div class="flex justify-end gap-3">
        <a routerLink="/tenant" class="btn-secondary bg-white">{{ 'COMMON.CANCEL' | translate }}</a>
        <button type="submit" [disabled]="busy()" class="btn-primary !px-6"><span class="material-icons text-[18px]">save</span> {{ 'TENANT_ADMIN.CREATE' | translate }}</button>
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
      this.toastr.success('TENANT_ADMIN.CREATED', { code: created.code });
      await this.router.navigate(['/tenant', created.publicId]);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
