import { Injectable, signal, inject, PLATFORM_ID, computed } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Router } from '@angular/router';
import { User, Book, ReaderCardProfile } from './models';
import { FavoriteApiService } from './favorite-api.service';
import { AuthApiService, isGuid } from './auth-api.service';

const OPAC_USER_KEY = 'opac_user'; // key riêng cho session OPAC (tách biệt admin)
const OPAC_READER_PROFILE_KEY = 'opac_reader_profile'; // Đợt 22.6 — cache hồ sơ thẻ, đọc offline được

@Injectable({ providedIn: 'root' })
export class AuthService {
  private router = inject(Router);
  private platformId = inject(PLATFORM_ID);
  private favoriteApi = inject(FavoriteApiService);
  private authApi = inject(AuthApiService);
  private isBrowser = isPlatformBrowser(this.platformId);

  currentUser = signal<User | null>(null);
  isAuthenticated = signal<boolean>(false);
  savedBooks = signal<Book[]>([]);
  // Đợt 22.6 — hồ sơ thẻ bạn đọc (hạn thẻ...), khôi phục từ cache lúc khởi động như currentUser;
  // refreshReaderProfile() gọi lại mỗi lần mở trang Thẻ, ghi đè cache khi thành công.
  readerProfile = signal<ReaderCardProfile | null>(null);

  constructor() {
    if (this.isBrowser) {
      // Check localStorage for existing session
      const savedUser = localStorage.getItem(OPAC_USER_KEY);
      if (savedUser) {
        try {
          const user = JSON.parse(savedUser);
          // Khôi phục phiên (giữ đăng nhập khi F5). Không xoá phiên dù id không hợp lệ —
          // việc chặn gửi readerId rác đã do guard isGuid trong loadSavedBooks/toggleSaveBook lo.
          this.currentUser.set(user);
          this.isAuthenticated.set(true);
          this.loadSavedBooks(user.id, user.tenantId);
        } catch {
          localStorage.removeItem(OPAC_USER_KEY);
        }
      }
      const savedProfile = localStorage.getItem(OPAC_READER_PROFILE_KEY);
      if (savedProfile) {
        try { this.readerProfile.set(JSON.parse(savedProfile)); }
        catch { localStorage.removeItem(OPAC_READER_PROFILE_KEY); }
      }
    }
  }

  setUser(user: User) {
    this.currentUser.set(user);
    this.isAuthenticated.set(true);
    if (this.isBrowser) {
      localStorage.setItem(OPAC_USER_KEY, JSON.stringify(user));
      this.loadSavedBooks(user.id, user.tenantId);
    }
  }

  /** Làm mới hồ sơ thẻ (hạn thẻ...) — lỗi/mất mạng thì im lặng giữ nguyên bản cache cũ (offline-first),
   *  KHÔNG xoá cache như logout, vì mất mạng không có nghĩa là hết hạn thẻ. */
  refreshReaderProfile(): void {
    if (!this.isBrowser || !this.isAuthenticated()) return;
    this.authApi.getProfile().subscribe(profile => {
      if (!profile) return;
      this.readerProfile.set(profile);
      localStorage.setItem(OPAC_READER_PROFILE_KEY, JSON.stringify(profile));
    });
  }

  logout() {
    this.currentUser.set(null);
    this.isAuthenticated.set(false);
    this.savedBooks.set([]);
    this.readerProfile.set(null);
    if (this.isBrowser) {
      localStorage.removeItem(OPAC_USER_KEY);
      localStorage.removeItem(OPAC_READER_PROFILE_KEY);
    }
    this.router.navigate(['/']);
  }
  
  // Tài liệu yêu thích — lưu DB theo bạn đọc (readerId = user.id = publicId).
  private loadSavedBooks(userId: string, tenantId?: string) {
    if (!isGuid(userId)) return; // chỉ tải khi readerId là GUID hợp lệ
    this.favoriteApi.getFavorites(userId, tenantId).subscribe(books => this.savedBooks.set(books));
  }

  toggleSaveBook(book: Book) {
    if (!this.isAuthenticated()) {
      this.router.navigate(['/login']);
      return;
    }

    const user = this.currentUser();
    if (!user) return;
    if (!isGuid(user.id)) { // thiếu/không hợp lệ publicId bạn đọc → không gửi readerId rác lên API
      console.warn('Không thể lưu tài liệu: PublicId bạn đọc không hợp lệ (login chưa trả PublicId GUID)');
      return;
    }

    const wasSaved = this.isBookSaved(book.id);
    const previous = this.savedBooks();

    // Cập nhật lạc quan
    this.savedBooks.set(
      wasSaved ? previous.filter(b => b.id !== book.id) : [...previous, book]
    );

    const req$ = wasSaved
      ? this.favoriteApi.removeFavorite(user.id, book.id, user.tenantId)
      : this.favoriteApi.addFavorite(user.id, book.id, user.tenantId);

    req$.subscribe(ok => {
      if (!ok) this.savedBooks.set(previous); // lỗi → hoàn lại
    });
  }

  isBookSaved(bookId: string): boolean {
    return this.savedBooks().some(b => b.id === bookId);
  }
}

