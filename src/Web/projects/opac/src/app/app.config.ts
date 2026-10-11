import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { TitleStrategy, provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';

import { routes } from './app.routes';
import { Language, languageInterceptor } from './core/i18n';
import { LibraryTitleStrategy } from './core/library';
import { readerAuthInterceptor } from './core/reader-auth';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding(), withInMemoryScrolling({ scrollPositionRestoration: 'top' })),
    provideHttpClient(withInterceptors([languageInterceptor, readerAuthInterceptor])),
    // Từ điển public/i18n/{vi,en}.json (khoá theo ELIB-LRC: OPAC.*, COMMON.*); thiếu khoá ở en thì dùng vi.
    provideTranslateService({ fallbackLang: 'vi', loader: provideTranslateHttpLoader({ prefix: 'i18n/', suffix: '.json' }) }),
    provideAppInitializer(() => inject(Language).init()),
    { provide: TitleStrategy, useExisting: LibraryTitleStrategy },
  ],
};
