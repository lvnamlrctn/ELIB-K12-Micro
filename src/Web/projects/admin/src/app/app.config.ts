import { registerLocaleData } from '@angular/common';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import localeVi from '@angular/common/locales/vi';
import { ApplicationConfig, LOCALE_ID, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';

import { routes } from './app.routes';
import { authInterceptor } from './core/auth.interceptor';
import { Language, languageInterceptor } from './core/i18n';

// Pipe date/number gốc (màn chưa chuyển sang ldate/lnumber) giữ kiểu Việt Nam (27.000; 08/10/2026) như admin cũ.
registerLocaleData(localeVi);

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([languageInterceptor, authInterceptor])),
    { provide: LOCALE_ID, useValue: 'vi' },
    // Từ điển public/i18n/{vi,en}.json — khoá theo ELIB-LRC (MENU.*, COMMON.*, READER.*…); thiếu khoá ở en thì dùng vi.
    provideTranslateService({ fallbackLang: 'vi', loader: provideTranslateHttpLoader({ prefix: 'i18n/', suffix: '.json' }) }),
    provideAppInitializer(() => inject(Language).init()),
  ],
};
