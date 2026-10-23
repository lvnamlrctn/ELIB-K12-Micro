import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./pages/home').then((m) => m.Home) },
  // Chức năng của các giai đoạn sau (thư viện số, tài khoản bạn đọc…).
  { path: 'tim-kiem', loadComponent: () => import('./pages/search').then((m) => m.Search), title: 'OPAC.HOME.SERVICE_SEARCH' },
  { path: 'lien-thu-vien', loadComponent: () => import('./pages/z3950').then((m) => m.Z3950), title: 'OPAC.Z3950.TITLE' },
  { path: 'tai-lieu/:publicId', loadComponent: () => import('./pages/bib-detail').then((m) => m.BibDetail) },
  { path: 'thu-vien-so', loadComponent: () => import('./pages/ebook-search').then((m) => m.EbookSearch), title: 'OPAC.HOME.SERVICE_DIGITAL' },
  { path: 'thu-vien-so/:publicId', loadComponent: () => import('./pages/ebook-detail').then((m) => m.EbookDetail) },
  { path: 'thu-vien-so/:publicId/doc/:fileId', loadComponent: () => import('./pages/ebook-reader').then((m) => m.EbookReader), title: 'OPAC.READER.TITLE' },
  { path: 'nop-tai-lieu', loadComponent: () => import('./pages/submit-document').then((m) => m.SubmitDocument), title: 'OPAC.SUBMIT_DOC.TITLE' },
  { path: 'tai-khoan', loadComponent: () => import('./pages/account').then((m) => m.Account), title: 'OPAC.MY_LIBRARY.ACCOUNT_TITLE' },
  { path: 'tai-khoan/callback', loadComponent: () => import('./pages/account-callback').then((m) => m.AccountCallback) },
  { path: 'dat-phong', loadComponent: () => import('./pages/space-booking').then((m) => m.SpaceBooking), title: 'OPAC.HOME.SERVICE_SPACE' },
  { path: 'tro-ly', loadComponent: () => import('./pages/coming-soon').then((m) => m.ComingSoon), data: { feature: 'OPAC.HOME.SERVICE_AI' } },
  { path: 'tin-tuc', loadComponent: () => import('./pages/news-list').then((m) => m.NewsList), title: 'OPAC.PORTAL.NEWS_TITLE' },
  { path: 'tin-tuc/:slug', loadComponent: () => import('./pages/news-detail').then((m) => m.NewsDetail) },
  { path: 'trang/:slug', loadComponent: () => import('./pages/static-page').then((m) => m.StaticPage) },
  { path: 'lien-he', loadComponent: () => import('./pages/contact').then((m) => m.Contact), title: 'OPAC.PORTAL.CONTACT_TITLE' },
  { path: 'hinh-anh-video', loadComponent: () => import('./pages/gallery').then((m) => m.Gallery), title: 'OPAC.PORTAL.GALLERY_TITLE' },
  { path: '**', loadComponent: () => import('./pages/not-found').then((m) => m.NotFound) },
];
