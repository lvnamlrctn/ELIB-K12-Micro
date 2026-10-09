import {
  ApplicationConfig,
  provideBrowserGlobalErrorListeners,
  provideAppInitializer,
  inject,
  isDevMode,
  PLATFORM_ID,
} from '@angular/core';
import { provideServiceWorker } from '@angular/service-worker';
import {provideRouter, withPreloading} from '@angular/router';
import {provideHttpClient, withFetch, withInterceptors, HttpClient} from '@angular/common/http';
import { isPlatformBrowser } from '@angular/common';
import { provideTranslateService, TranslateLoader } from '@ngx-translate/core';

import {routes} from './app.routes';
import {authInterceptor} from './services/auth.interceptor';
import { apiInterceptor } from './opac/api.interceptor';
import { errorInterceptor } from './opac/error.interceptor';
import { opacAuthInterceptor } from './opac/auth.interceptor';
import { MergedTranslateLoader } from './services/shared/merged-translate-loader';
import { AdminPreloadStrategy } from './services/shared/admin-preload.strategy';

import { MatPaginatorIntl } from '@angular/material/paginator';
import { CustomPaginatorIntl } from './services/shared/custom-paginator-intl';
import { TenantService } from './opac/services/tenant.service';
import { provideMarkdown } from 'ngx-markdown';
import { MAT_DATE_FORMATS, MAT_DATE_LOCALE, DateAdapter } from '@angular/material/core';
import { CustomDateAdapter, APP_DATE_FORMATS } from './components/date-input/date-input-adapter';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideMarkdown(),
    // Đa đơn vị: resolve tenant theo subdomain trước khi app gọi API (chỉ browser).
    provideAppInitializer(() => inject(TenantService).resolve()),
    provideRouter(routes, withPreloading(AdminPreloadStrategy)),
    provideHttpClient(withFetch(), withInterceptors([authInterceptor, apiInterceptor, opacAuthInterceptor, errorInterceptor])),
    provideTranslateService({
      defaultLanguage: 'vi',
      loader: {
        provide: TranslateLoader,
        useFactory: (http: HttpClient, platformId: object) =>
          new MergedTranslateLoader(http, isPlatformBrowser(platformId)),
        deps: [HttpClient, PLATFORM_ID]
      }
    }),
    { provide: MatPaginatorIntl, useClass: CustomPaginatorIntl },
    { provide: MAT_DATE_LOCALE, useValue: 'vi-VN' },
    { provide: DateAdapter, useClass: CustomDateAdapter, deps: [MAT_DATE_LOCALE] },
    { provide: MAT_DATE_FORMATS, useValue: APP_DATE_FORMATS },
    // Đợt 22.6 — PWA (OPAC). Chỉ đăng ký khi build production; PLATFORM_ID no-op sẵn lúc SSR nên không
    // cần bọc thêm isPlatformBrowser ở đây (đúng cách ELIB-LRC làm).
    provideServiceWorker('ngsw-worker.js', {
      enabled: !isDevMode(),
      registrationStrategy: 'registerWhenStable:30000',
    }),
  ],
};
