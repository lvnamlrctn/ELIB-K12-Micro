import { Injectable } from '@angular/core';
import { DicCodeDescService } from './dic-code-desc.service';

@Injectable({ providedIn: 'root' })
export class DicLanguageService extends DicCodeDescService {
  protected override get baseUrl(): string { return '/api/PrintBook/DicLanguage'; }
}
