import { Component, inject } from '@angular/core';
import { DicCodeDescComponent } from '../shared/dic-code-desc';
import { DicCountryService } from '../../../services/cataloging/dic-country.service';

@Component({
  selector: 'app-dic-country',
  standalone: true,
  imports: [DicCodeDescComponent],
  template: `<app-dic-code-desc [service]="service" translationKeyPrefix="DIC_COUNTRY"></app-dic-code-desc>`
})
export class DicCountryPage {
  service = inject(DicCountryService);
}
