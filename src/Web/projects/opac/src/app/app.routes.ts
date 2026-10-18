import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./pages/home').then((m) => m.Home) },
  // Chức năng của các giai đoạn sau (thư viện số, tài khoản bạn đọc…).
  { path: 'tim-kiem', loadComponent: () => import('./pages/search').then((m) => m.Search), title: 'OPAC.HOME.SERVICE_SEARCH' },
  { path: 'lien-thu-vien', loadComponent: () => import('./pages/z3950').then((m) => m.Z3950), title: 'OPAC.Z3950.TITLE' },
  { path: 'tai-lieu/:publicId', loadComponent: () => import('./pages/bib-detail').then((m) => m.BibDetail) },
  { path: 'thu-vien-so', loadComponent: () => import('./pages/ebook-search').then((m) => m.EbookSearch), title: 'OPAC.HOME.SERVICE_DIGITAL' },
  { path: 'thu-vien-so/:publicId', loadComponent: () => import('./pages/ebook-detail').then((m) => m.EbookDetail) },
  { path: 'tai-khoan', loadComponent: () => import('./pages/account').then((m) => m.Account), title: 'OPAC.MY_LIBRARY.ACCOUNT_TITLE' },
  { path: 'tai-khoan/callback', loadComponent: () => import('./pages/account-callback').then((m) => m.AccountCallback) },
  { path: 'dat-phong', loadComponent: () => import('./pages/coming-soon').then((m) => m.ComingSoon), data: { feature: 'OPAC.HOME.SERVICE_SPACE' } },
  { path: 'tro-ly', loadComponent: () => import('./pages/coming-soon').then((m) => m.ComingSoon), data: { feature: 'OPAC.HOME.SERVICE_AI' } },
  { path: 'tin-tuc', loadComponent: () => import('./pages/coming-soon').then((m) => m.ComingSoon), data: { feature: 'OPAC.HOME.SERVICE_PORTAL' } },
  { path: '**', loadComponent: () => import('./pages/not-found').then((m) => m.NotFound) },
];
