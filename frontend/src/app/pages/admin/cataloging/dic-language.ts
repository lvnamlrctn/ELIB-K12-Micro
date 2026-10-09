import { Component, inject } from '@angular/core';
import { DicCodeDescComponent } from '../shared/dic-code-desc';
import { DicLanguageService } from '../../../services/cataloging/dic-language.service';

@Component({
  selector: 'app-dic-language',
  standalone: true,
  imports: [DicCodeDescComponent],
  template: `<app-dic-code-desc [service]="service" translationKeyPrefix="DIC_LANGUAGE"></app-dic-code-desc>`
})
export class DicLanguagePage {
  service = inject(DicLanguageService);
}
