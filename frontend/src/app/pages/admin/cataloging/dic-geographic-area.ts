import { Component, inject } from '@angular/core';
import { DicCodeDescComponent } from '../shared/dic-code-desc';
import { DicGeographicAreaService } from '../../../services/cataloging/dic-geographic-area.service';

@Component({
  selector: 'app-dic-geographic-area',
  standalone: true,
  imports: [DicCodeDescComponent],
  template: `<app-dic-code-desc [service]="service" translationKeyPrefix="DIC_GEO_AREA"></app-dic-code-desc>`
})
export class DicGeographicAreaPage {
  service = inject(DicGeographicAreaService);
}
