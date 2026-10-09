import { HttpClient } from '@angular/common/http';
import { TranslateLoader, TranslationObject } from '@ngx-translate/core';
import { Observable, of, forkJoin } from 'rxjs';
import { map, catchError } from 'rxjs/operators';

/**
 * Loads translations from BOTH the admin bundle (`/i18n/{lang}.json`) and the
 * OPAC bundle (`/assets/i18n/{lang}.json`) and deep-merges them into a single
 * dictionary. Admin keys take precedence on collision.
 *
 * On the server (SSR) we skip the HTTP fetch and return an empty dictionary;
 * the client re-fetches and hydrates the real translations.
 */
export class MergedTranslateLoader implements TranslateLoader {
  constructor(private http: HttpClient, private isBrowser: boolean) {}

  getTranslation(lang: string): Observable<TranslationObject> {
    if (!this.isBrowser) {
      return of({});
    }

    return forkJoin({
      opac: this.http.get<TranslationObject>(`/assets/i18n/${lang}.json`).pipe(catchError(() => of({}))),
      admin: this.http.get<TranslationObject>(`/i18n/${lang}.json`).pipe(catchError(() => of({})))
    }).pipe(
      map(({ opac, admin }) => deepMerge(deepMerge({}, opac), admin) as TranslationObject)
    );
  }
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function deepMerge(target: any, source: any): any {
  for (const key of Object.keys(source ?? {})) {
    const sv = source[key];
    if (sv && typeof sv === 'object' && !Array.isArray(sv)) {
      target[key] = deepMerge(target[key] && typeof target[key] === 'object' ? target[key] : {}, sv);
    } else {
      target[key] = sv;
    }
  }
  return target;
}
