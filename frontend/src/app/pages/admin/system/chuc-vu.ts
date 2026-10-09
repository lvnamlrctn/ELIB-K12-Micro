import { Component, inject } from '@angular/core';
import { BaseEntityComponent } from '../shared/base-entity';
import { ChucVuService } from '../../../services/system/chuc-vu.service';

@Component({
  selector: 'app-chuc-vu',
  standalone: true,
  imports: [BaseEntityComponent],
  template: `<app-base-entity [service]="service" translationKeyPrefix="CHUC_VU"></app-base-entity>`
})
export class ChucVuPage {
  service = inject(ChucVuService);
}
