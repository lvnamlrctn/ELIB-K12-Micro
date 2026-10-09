import { Component, inject } from '@angular/core';
import { BaseEntityComponent } from '../shared/base-entity';
import { ReaderTypeService } from '../../../services/circulation/reader-type.service';

@Component({
  selector: 'app-reader-type',
  standalone: true,
  imports: [BaseEntityComponent],
  template: `<app-base-entity [service]="service" translationKeyPrefix="READER_TYPE"></app-base-entity>`
})
export class ReaderTypePage {
  service = inject(ReaderTypeService);
}
