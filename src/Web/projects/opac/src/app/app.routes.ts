import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./pages/home').then((m) => m.Home) },
  // Chức năng của các giai đoạn sau (thư viện số, tài khoản bạn đọc…).
  { path: 'tim-kiem', loadComponent: () => import('./pages/search').then((m) => m.Search), title: 'Tra cứu tài liệu' },
  { path: 'lien-thu-vien', loadComponent: () => import('./pages/z3950').then((m) => m.Z3950), title: 'Tra cứu liên thư viện' },
  { path: 'tai-lieu/:publicId', loadComponent: () => import('./pages/bib-detail').then((m) => m.BibDetail) },
  { path: 'thu-vien-so', loadComponent: () => import('./pages/coming-soon').then((m) => m.ComingSoon), data: { feature: 'Thư viện số' } },
  { path: 'tai-khoan', loadComponent: () => import('./pages/coming-soon').then((m) => m.ComingSoon), data: { feature: 'Tài khoản bạn đọc' } },
  { path: 'dat-phong', loadComponent: () => import('./pages/coming-soon').then((m) => m.ComingSoon), data: { feature: 'Đặt phòng, chỗ ngồi' } },
  { path: 'tro-ly', loadComponent: () => import('./pages/coming-soon').then((m) => m.ComingSoon), data: { feature: 'Trợ lý AI' } },
  { path: 'tin-tuc', loadComponent: () => import('./pages/coming-soon').then((m) => m.ComingSoon), data: { feature: 'Tin tức, sự kiện' } },
  { path: '**', loadComponent: () => import('./pages/not-found').then((m) => m.NotFound) },
];
