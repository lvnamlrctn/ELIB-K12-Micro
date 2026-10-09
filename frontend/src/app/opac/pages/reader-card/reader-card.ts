import { Component, OnInit, OnDestroy, signal, computed, inject, PLATFORM_ID } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../../services/auth.service';
import { BarcodeDirective } from '../../../components/barcode.directive';

/** Thẻ bạn đọc điện tử (Đợt 22.6 — PWA) — mã vạch Code128 từ số thẻ, đọc được cả khi mất mạng nhờ
 *  AuthService.readerProfile cache localStorage + dataGroup "public-api" của service worker
 *  (ngsw-config.json) cache sẵn GET /api/public/PublicReader/Profile lần gần nhất thành công. */
@Component({
  selector: 'app-reader-card',
  standalone: true,
  imports: [CommonModule, RouterModule, TranslateModule, BarcodeDirective],
  templateUrl: './reader-card.html'
})
export class ReaderCardComponent implements OnInit, OnDestroy {
  private authService = inject(AuthService);
  private router = inject(Router);
  private platformId = inject(PLATFORM_ID);

  readerProfile = this.authService.readerProfile;
  currentUser = this.authService.currentUser;
  isOffline = signal(false);

  cardNumber = computed(() => this.readerProfile()?.cardno || this.currentUser()?.username || '');
  displayName = computed(() => this.readerProfile()?.fullName || this.currentUser()?.fullName || 'Bạn đọc');
  cardExpireDate = computed(() => this.readerProfile()?.expireDate || null);
  isExpired = computed(() => {
    const d = this.cardExpireDate();
    return !!d && new Date(d).getTime() < Date.now();
  });

  private onOnline = () => this.isOffline.set(false);
  private onOffline = () => this.isOffline.set(true);

  ngOnInit(): void {
    if (!isPlatformBrowser(this.platformId)) return;

    if (!this.authService.isAuthenticated()) {
      this.router.navigate(['/login']);
      return;
    }

    this.isOffline.set(!navigator.onLine);
    window.addEventListener('online', this.onOnline);
    window.addEventListener('offline', this.onOffline);
    // Làm mới hạn thẻ mỗi lần mở trang — lỗi/mất mạng vẫn hiện được bản cache cũ (xem AuthService.refreshReaderProfile).
    this.authService.refreshReaderProfile();
  }

  ngOnDestroy(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    window.removeEventListener('online', this.onOnline);
    window.removeEventListener('offline', this.onOffline);
  }
}
