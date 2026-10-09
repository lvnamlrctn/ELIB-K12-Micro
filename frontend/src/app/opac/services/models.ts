import { Injectable, inject, PLATFORM_ID } from '@angular/core';
import { isPlatformServer } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { of, Observable, map, catchError, shareReplay, forkJoin } from 'rxjs';
import { APP_CONFIG } from '../config';

export interface News {
  id: string;
  categoryId: string;
  title: string;
  summary: string;
  content: string;
  imageUrl: string;
  date: string;
  views: number;
}

export interface Comment {
  id: string;
  author: string;
  avatar: string;
  content: string;
  date: string;
  rating?: number;
}

export interface Book {
  id: string;
  title: string;
  author: string;
  publisher: string;
  year: number;
  imageUrl: string;
  description: string;
  views: number;
  downloads: number;
  files: BookFile[];
  rating?: number;
  ratingCount?: number;
  comments?: Comment[];
  free?: number;          // 2 = miễn phí (không cần đăng nhập); 1/null = cần đăng nhập
  allowDownload?: number; // 2 = cho phép tải tài liệu
}

export interface BookFile {
  id: string;
  name: string;
  size: number;
  type: 'pdf' | 'audio' | 'video' | 'docx' | 'pptx' | 'xlsx';
  url: string;
}

// Banner công khai cho ảnh nền hero trang chủ (API PublicBanner). url = URL ảnh tuyệt đối.
export interface PublicBanner {
  url: string;
  link?: string;
  name?: string;
  sortOrder?: number;
}

// Bình luận / đánh giá công khai (API EbookReview). itemId = publicId của tài liệu.
export interface EbookReview {
  id: number;
  itemId: string;
  rating: number;
  displayName: string;
  content: string;
  status: number;
  createdRowDate: string;
}

export interface Category {
  id: string;
  name: string;
}

export interface MenuItem {
  id: string;
  label: string;
  url?: string;
  children?: MenuItem[];
}

export interface User {
  id: string;
  username: string;
  fullName: string;
  email: string;
  role: 'admin' | 'user';
  tenantId?: string; // TenantId của bạn đọc, lấy từ response login — dùng scope EbookFavorite
  token?: string;    // JWT của bạn đọc (Reader) — gửi lên khi kiểm tra quyền đọc/tải tài liệu
}

// Đợt 22.6 — hồ sơ thẻ bạn đọc (hạn thẻ...) cho trang "Thẻ bạn đọc điện tử". Login không trả ExpireDate
// (chỉ có hạn JWT) nên cache riêng, tách khỏi User — xem opac/services/auth.service.ts.
export interface ReaderCardProfile {
  cardno: string;
  fullName: string;
  issueDate: string | null;
  expireDate: string | null;
}

export interface Hyperlink {
  publicId: string;
  name: string;
  linkUrl: string;
  description: string;
  images: string;
  status: number;
}

export interface VisitStats {
  total: number;
  today: number;
  lastWeek: number;
  lastMonth: number;
}

// Thư viện liên kết (cấu hình Z39.50) cho Tìm kiếm nâng cao
export interface Z3950Library {
  publicId: string;
  name: string;
  id?: number;
}
