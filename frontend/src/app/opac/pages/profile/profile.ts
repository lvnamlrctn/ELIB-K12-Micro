import { TranslateModule } from '@ngx-translate/core';
import { Component, inject, OnInit, signal, computed, PLATFORM_ID } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { User, Book } from '../../services/models';
import {
  MyLibraryApiService, BorrowingItem, ReservedItem,
  DigitalBorrowingItem, DigitalReservedItem, MyLibraryStats, ReaderBadges
} from '../../services/my-library-api.service';
import { RoomBookingOpacService, RoomOpac, RoomDayAvailability, MyRoomBooking } from '../../services/room-booking-opac.service';
import { toRoomBookingQr } from '../../../shared/room-booking-qr';
import { PaymentQrDialogComponent } from '../../../components/payment-qr-dialog/payment-qr-dialog';
import { FaceCaptureComponent } from '../../../components/face-capture/face-capture';
import { map, tap } from 'rxjs';
import { PaymentReaderService } from '../../../services/payment/payment-reader.service';
import { PaymentProvider, PaymentTargetType, ReaderDebts } from '../../../models/payment/payment';
import { SavedSearchOpacService, SavedSearch } from '../../services/saved-search-opac.service';
import { ResearchWorkspaceOpacService, ResearchProject } from '../../services/research-workspace-opac.service';

interface RoomTimeSlot {
  startAt: Date;
  endAt: Date;
  label: string;
  state: 'available' | 'booked' | 'past';
}

@Component({
  selector: 'app-profile',
  imports: [TranslateModule, CommonModule, FormsModule, RouterModule, PaymentQrDialogComponent, FaceCaptureComponent],
  templateUrl: './profile.html'
})
export class ProfileComponent implements OnInit {
  public authService = inject(AuthService); // Make it public to use in template
  private myLibraryApi = inject(MyLibraryApiService);
  private roomBookingApi = inject(RoomBookingOpacService);
  private paymentApi = inject(PaymentReaderService);
  private savedSearchApi = inject(SavedSearchOpacService);
  private workspaceApi = inject(ResearchWorkspaceOpacService);
  private router = inject(Router);
  private platformId = inject(PLATFORM_ID);

  user = signal<User | null>(null);
  activeTab = signal<'activity' | 'saved' | 'borrowing' | 'reserved' | 'digital' | 'badges' | 'rooms' | 'debts' | 'savedSearches' | 'workspace' | 'settings'>('activity');

  // ── Tìm kiếm đã lưu ──────────────────────────────────────────────────────────
  savedSearchesLoading = signal(false);
  savedSearches = signal<SavedSearch[]>([]);

  // ── Không gian nghiên cứu (chỉ Đề tài — xem ghi chú trong ResearchWorkspaceOpacService) ─────────────
  workspaceLoading = signal(false);
  workspaceSaving  = signal(false);
  workspaceConflict = signal(false);
  private workspaceVersion = '00000000-0000-0000-0000-000000000000';
  private workspaceHighlights: unknown[] = [];
  projects = signal<ResearchProject[]>([]);
  showProjectModal = signal(false);
  editingProjectId: string | null = null;
  projectDraft = { title: '', description: '', category: '', color: '#3b82f6' };

  borrowingLoading = signal(false);
  borrowing = signal<BorrowingItem[]>([]);
  reserved  = signal<ReservedItem[]>([]);

  digitalLoading = signal(false);
  digitalBorrowing = signal<DigitalBorrowingItem[]>([]);
  digitalReserved  = signal<DigitalReservedItem[]>([]);

  statsLoading = signal(false);
  stats = signal<MyLibraryStats | null>(null);
  maxMonthlyReads = computed(() => Math.max(1, ...(this.stats()?.monthly ?? []).map(m => m.reads)));

  badgesLoading = signal(false);
  badges = signal<ReaderBadges | null>(null);

  // ── Đặt phòng học nhóm ──────────────────────────────────────────────────────
  roomsLoading   = signal(false);
  roomsEnabled   = signal(false);
  rooms          = signal<RoomOpac[]>([]);
  selectedRoomId = signal<number | null>(null);
  selectedDate   = signal<string>(this.todayStr());
  slotsLoading   = signal(false);
  private dayAvailability = signal<RoomDayAvailability | null>(null);
  selectedSlot   = signal<RoomTimeSlot | null>(null);
  partySize = 1;
  bookingNote = '';
  /** Thẻ thành viên nhóm (số thẻ hoặc UID), mỗi dòng/dấu phẩy một thẻ — có thì số người = thành viên + bạn. */
  memberCardsText = '';
  bookingSubmitting = signal(false);
  bookingMessage = signal<{ ok: boolean; text: string } | null>(null);

  myBookingsLoading = signal(false);
  myBookings = signal<MyRoomBooking[]>([]);

  selectedRoom = computed(() => this.rooms().find(r => r.mapObjectId === this.selectedRoomId()) ?? null);

  /** Phòng đang chọn tạm ngưng / bạn đọc không thuộc đối tượng được đặt → khoá nút đặt. */
  roomBlocked = computed(() => {
    const r = this.selectedRoom();
    return !!r && (!!r.maintenance || r.readerAllowed === false);
  });

  /** Độ dài 1 lượt: SlotMinutes của phòng, làm tròn lên bội số mốc giờ của máy chủ (30 phút). */
  slotLength = computed(() => {
    const step = this.dayAvailability()?.stepMinutes || 30;
    const room = this.selectedRoom();
    return Math.max(step, Math.ceil((room?.slotMinutes || step) / step) * step);
  });

  // Lưới khung giờ trong ngày theo giờ mở cửa của riêng phòng ngày đó (ngày đặc biệt / giờ theo thứ / giờ chung — máy chủ tính),
  // bước theo độ dài lượt. Trước đây cố định 07:00-21:00 nên có ô ngoài giờ mở cửa thật.
  timeSlots = computed<RoomTimeSlot[]>(() => {
    const room = this.selectedRoom();
    const day = this.dayAvailability();
    if (!room || !day || day.closed) return [];
    const dateStr = this.selectedDate();
    const [y, m, d] = dateStr.split('-').map(Number);
    const [oh, om] = day.openTime.split(':').map(Number);
    const [ch, cm] = day.closeTime.split(':').map(Number);
    const dayStart = new Date(y, (m ?? 1) - 1, d ?? 1, oh || 0, om || 0, 0, 0);
    const dayEnd   = new Date(y, (m ?? 1) - 1, d ?? 1, ch || 0, cm || 0, 0, 0);
    const now = new Date();
    const booked = day.slots.map(s => ({ start: new Date(s.startAt), end: new Date(s.endAt) }));
    const slots: RoomTimeSlot[] = [];
    let cursor = new Date(dayStart);
    while (cursor < dayEnd) {
      const end = new Date(cursor.getTime() + this.slotLength() * 60000);
      if (end > dayEnd) break;
      const isPast = cursor < now;
      const isBooked = booked.some(b => cursor < b.end && end > b.start);
      slots.push({
        startAt: new Date(cursor),
        endAt: end,
        label: `${this.hhmm(cursor)} - ${this.hhmm(end)}`,
        state: isPast ? 'past' : (isBooked ? 'booked' : 'available'),
      });
      cursor = end;
    }
    return slots;
  });

  private todayStr(): string {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  private hhmm(d: Date): string {
    return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
  }

  private loadRooms(): void {
    this.roomsLoading.set(true);
    this.roomBookingApi.getRooms().subscribe(res => {
      this.roomsEnabled.set(res.enabled);
      this.rooms.set(res.rooms);
      this.roomsLoading.set(false);
      if (res.rooms.length > 0 && this.selectedRoomId() === null) this.selectRoom(res.rooms[0].mapObjectId);
    });
  }

  private loadMyBookings(): void {
    this.myBookingsLoading.set(true);
    this.roomBookingApi.getMyBookings().subscribe(items => { this.myBookings.set(items); this.myBookingsLoading.set(false); });
  }

  selectRoom(mapObjectId: number): void {
    this.selectedRoomId.set(mapObjectId);
    this.selectedSlot.set(null);
    this.loadSlots();
  }

  onDateChange(dateStr: string): void {
    this.selectedDate.set(dateStr);
    this.selectedSlot.set(null);
    this.loadSlots();
  }

  private loadSlots(): void {
    const roomId = this.selectedRoomId();
    if (!roomId) return;
    this.slotsLoading.set(true);
    this.roomBookingApi.getAvailability(roomId, this.selectedDate()).subscribe(day => {
      this.dayAvailability.set(day);
      this.slotsLoading.set(false);
    });
  }

  pickSlot(slot: RoomTimeSlot): void {
    if (slot.state !== 'available') return;
    this.selectedSlot.set(slot);
    this.bookingMessage.set(null);
  }

  submitBooking(): void {
    const room = this.selectedRoom();
    const slot = this.selectedSlot();
    if (!room || !slot) return;
    const memberCards = this.memberCardsText.split(/[\n,;]+/).map(x => x.trim()).filter(x => x.length > 0);
    const minGroup = Math.max(1, room.minGroupSize ?? 1);
    if (minGroup > 1 && memberCards.length + 1 < minGroup) {
      this.bookingMessage.set({ ok: false, text: `Phòng này cần tối thiểu ${minGroup} người: vui lòng nhập mã thẻ của ${minGroup - 1} thành viên trở lên.` });
      return;
    }
    const partySize = memberCards.length > 0 ? memberCards.length + 1 : this.partySize;
    if (partySize < 1 || partySize > room.capacity) {
      this.bookingMessage.set({ ok: false, text: `Số người tham gia phải từ ${minGroup} đến ${room.capacity}.` });
      return;
    }
    this.bookingSubmitting.set(true);
    this.roomBookingApi.book({
      mapObjectId: room.mapObjectId,
      startAt: slot.startAt.toISOString(),
      endAt: slot.endAt.toISOString(),
      partySize,
      note: this.bookingNote || undefined,
      memberCards: memberCards.length > 0 ? memberCards : undefined,
    }).subscribe(res => {
      this.bookingSubmitting.set(false);
      this.bookingMessage.set({ ok: res.ok, text: res.ok
        ? (res.status === 2 ? 'Đặt phòng thành công, lượt đặt đã được duyệt.' : 'Đã gửi yêu cầu đặt phòng, chờ thư viện duyệt.')
        : (res.message || 'Không thể đặt phòng.') });
      if (res.ok) {
        this.selectedSlot.set(null);
        this.bookingNote = '';
        this.memberCardsText = '';
        this.partySize = 1;
        this.loadSlots();
        this.loadMyBookings();
      }
    });
  }

  cancelRoomBooking(publicId: string): void {
    this.roomBookingApi.cancel(publicId).subscribe(res => {
      if (res.ok) { this.loadMyBookings(); this.loadSlots(); }
      else this.bookingMessage.set({ ok: false, text: res.message || 'Không thể huỷ lượt đặt phòng này.' });
    });
  }

  /** Trả phòng (kết thúc sớm) — phòng được mở lại ngay cho bạn đọc khác. */
  checkOutRoomBooking(b: MyRoomBooking): void {
    if (!confirm(`Trả phòng ${b.roomName ?? ''} ngay bây giờ?`)) return;
    this.roomBookingApi.checkOut(b.publicId).subscribe(res => {
      if (res.ok) { this.loadMyBookings(); this.loadSlots(); }
      else this.bookingMessage.set({ ok: false, text: res.message || 'Không thể trả phòng.' });
    });
  }

  // ── Mã QR check-in (thủ thư/kiosk quét) — tự làm mới 5 giây/lần để báo ngay khi đã check-in ──────────
  qrBooking = signal<MyRoomBooking | null>(null);
  qrImage = signal<string | null>(null);
  private qrTimer: ReturnType<typeof setInterval> | null = null;

  async openCheckInQr(b: MyRoomBooking): Promise<void> {
    this.qrBooking.set(b);
    this.qrImage.set(null);
    const QRCode = (await import('qrcode')).default;
    this.qrImage.set(await QRCode.toDataURL(toRoomBookingQr(b.publicId), { width: 260, margin: 1 }));
    this.stopQrPolling();
    this.qrTimer = setInterval(() => this.roomBookingApi.getMyBookings().subscribe(items => {
      this.myBookings.set(items);
      const cur = items.find(x => x.publicId === b.publicId);
      if (cur) this.qrBooking.set(cur);
      if (!cur || cur.status !== 2) this.stopQrPolling();
    }), 5000);
  }

  closeCheckInQr(): void {
    this.stopQrPolling();
    this.qrBooking.set(null);
  }

  private stopQrPolling(): void {
    if (this.qrTimer) { clearInterval(this.qrTimer); this.qrTimer = null; }
  }

  /** Check-in bằng khuôn mặt (port ELIB-LRC 09-30): chỉ so với ảnh đã đăng ký của chính bạn đọc; bấm "Chụp & xác minh" (không tự
   *  quét). Không khớp/chưa tới giờ → hiện lý do, camera vẫn mở để thử lại (submit trả null). */
  faceCheckInFor(publicId: string) {
    return (base64: string) => this.roomBookingApi.checkInByFace(publicId, base64).pipe(
      tap(res => this.bookingMessage.set({ ok: res.ok, text: res.message || (res.ok ? 'Check-in thành công.' : 'Không thể check-in.') })),
      map(res => res.ok ? res : null));
  }

  onFaceCheckedIn(): void { this.loadMyBookings(); }

  checkInRoomBooking(publicId: string): void {
    this.roomBookingApi.checkIn(publicId).subscribe(res => {
      if (res.ok) this.loadMyBookings();
      else this.bookingMessage.set({ ok: false, text: res.message || 'Không thể check-in.' });
    });
  }

  /** Chỉ người đặt huỷ được, và chỉ trước giờ bắt đầu (đã tới giờ mà không check-in là vắng mặt). */
  canCancelRoomBooking(b: MyRoomBooking): boolean {
    return (b.status === 1 || b.status === 2) && b.isOwner !== false && new Date(b.startAt).getTime() > Date.now();
  }

  /** Check-in mở từ 15 phút trước giờ bắt đầu tới hết ân hạn của phòng. */
  canCheckInRoomBooking(b: MyRoomBooking): boolean {
    if (b.status !== 2) return false;
    const now = Date.now();
    const until = b.checkInUntil ? new Date(b.checkInUntil).getTime() : new Date(b.endAt).getTime();
    return now >= new Date(b.startAt).getTime() - 15 * 60000 && now <= until;
  }

  /** Mã QR hiện cho lượt đã duyệt chưa quá hạn check-in. */
  canShowCheckInQr(b: MyRoomBooking): boolean {
    return b.status === 2 && (!b.checkInUntil || Date.now() <= new Date(b.checkInUntil).getTime());
  }

  ngOnInit() {
    // Chỉ kiểm tra/điều hướng ở browser — SSR không có localStorage nên isAuthenticated luôn false,
    // redirect lúc SSR sẽ khiến F5 bị đẩy về /login. AuthService đã khôi phục phiên đồng bộ ở browser.
    if (!isPlatformBrowser(this.platformId)) return;

    if (!this.authService.isAuthenticated()) {
      this.router.navigate(['/login']);
      return;
    }

    this.user.set(this.authService.currentUser());
    // Tab mặc định là 'activity' — tải luôn thống kê, không chờ người dùng bấm lại.
    this.loadStats();
    // Tải sớm (không chờ chọn tab) để biết enabled=true/false quyết định có hiện nút tab "Huy hiệu" hay không.
    this.loadBadges();
    // Cùng lý do — cần biết ROOM_BOOKING_ENABLED trước khi vẽ nút tab "Đặt phòng học nhóm".
    this.loadRooms();
    // Tải sớm để hiện chấm đỏ "có kết quả mới" trên nút tab mà không cần bấm vào trước.
    this.loadSavedSearches();
    // Số khoản phí/phạt chưa trả hiện ngay trên nút tab "Phí / Nợ của tôi".
    this.loadDebts();
  }

  // ── Phí / Nợ của tôi — thanh toán QR VietQR/VNPAY (port ELIB-LRC 09-25) ──────────────────────────────
  debts = signal<ReaderDebts>({ tickets: [], photos: [] });
  paymentProviders = signal<PaymentProvider[]>([]);
  debtsLoading = signal(false);
  debtCount = computed(() => this.debts().tickets.length + this.debts().photos.length);
  debtTotal = computed(() => [...this.debts().tickets, ...this.debts().photos].reduce((s, d) => s + (d.remaining || 0), 0));
  debtPaymentTarget = signal<{ targetType: PaymentTargetType; targetId: number } | null>(null);

  loadDebts(): void {
    this.debtsLoading.set(true);
    this.paymentApi.myDebts().subscribe(r => {
      this.debts.set(r.debts);
      this.paymentProviders.set(r.providers);
      this.debtsLoading.set(false);
    });
  }

  openDebtPayment(targetType: PaymentTargetType, targetId: number): void { this.debtPaymentTarget.set({ targetType, targetId }); }

  /** Webhook đã gạch nợ — nạp lại, khoản vừa trả biến mất khỏi danh sách. */
  onDebtPaid(): void { this.debtPaymentTarget.set(null); this.loadDebts(); }

  selectTab(tab: 'activity' | 'saved' | 'borrowing' | 'reserved' | 'digital' | 'badges' | 'rooms' | 'debts' | 'savedSearches' | 'workspace' | 'settings'): void {
    this.activeTab.set(tab);
    if (tab === 'borrowing' || tab === 'reserved') this.loadBorrowing();
    if (tab === 'digital') this.loadDigital();
    if (tab === 'activity' && !this.stats()) this.loadStats();
    if (tab === 'badges' && !this.badges()) this.loadBadges();
    if (tab === 'rooms') { this.loadRooms(); this.loadMyBookings(); }
    if (tab === 'debts') this.loadDebts();
    if (tab === 'savedSearches') this.loadSavedSearches();
    if (tab === 'workspace') this.loadWorkspace();
  }

  // ── Tìm kiếm đã lưu ──────────────────────────────────────────────────────────

  private loadSavedSearches(): void {
    this.savedSearchesLoading.set(true);
    this.savedSearchApi.list().subscribe(items => { this.savedSearches.set(items); this.savedSearchesLoading.set(false); });
  }

  runSavedSearch(item: SavedSearch): void {
    if (item.hasUnread) this.savedSearchApi.markRead(item.id).subscribe();
    const q = item.query ?? {};
    this.router.navigate(['/tra-cuu'], { queryParams: {
      q: q.q || '', title: q.title || '', author: q.author || '', publisher: q.publisher || '', docType: q.docType || null
    }});
  }

  toggleSavedSearchAlerts(item: SavedSearch): void {
    const next = !item.alertsEnabled;
    this.savedSearchApi.toggleAlerts(item.id, next).subscribe(res => { if (res.ok) this.loadSavedSearches(); });
  }

  deleteSavedSearch(item: SavedSearch): void {
    this.savedSearchApi.remove(item.id).subscribe(res => { if (res.ok) this.loadSavedSearches(); });
  }

  // ── Không gian nghiên cứu ────────────────────────────────────────────────────

  private loadWorkspace(): void {
    this.workspaceLoading.set(true);
    this.workspaceConflict.set(false);
    this.workspaceApi.get().subscribe(snap => {
      this.workspaceVersion = snap.version;
      this.workspaceHighlights = snap.highlights;
      this.projects.set(snap.projects);
      this.workspaceLoading.set(false);
    });
  }

  openAddProject(): void {
    this.editingProjectId = null;
    this.projectDraft = { title: '', description: '', category: '', color: '#3b82f6' };
    this.showProjectModal.set(true);
  }

  openEditProject(p: ResearchProject): void {
    this.editingProjectId = p.id;
    this.projectDraft = { title: p.title, description: p.description, category: p.category, color: p.color || '#3b82f6' };
    this.showProjectModal.set(true);
  }

  closeProjectModal(): void { this.showProjectModal.set(false); }

  saveProjectDraft(): void {
    if (!this.projectDraft.title.trim()) return;
    const now = new Date().toISOString();
    let next: ResearchProject[];
    if (this.editingProjectId) {
      const id = this.editingProjectId;
      next = this.projects().map(p => p.id === id ? { ...p, ...this.projectDraft, title: this.projectDraft.title.trim() } : p);
    } else {
      const newProject: ResearchProject = {
        id: (typeof crypto !== 'undefined' && crypto.randomUUID) ? crypto.randomUUID() : `${Date.now()}`,
        title: this.projectDraft.title.trim(),
        description: this.projectDraft.description,
        category: this.projectDraft.category,
        color: this.projectDraft.color,
        createdAt: now,
      };
      next = [newProject, ...this.projects()];
    }
    this.projects.set(next);
    this.showProjectModal.set(false);
    this.persistWorkspace();
  }

  deleteProject(p: ResearchProject): void {
    this.projects.set(this.projects().filter(x => x.id !== p.id));
    this.persistWorkspace();
  }

  private persistWorkspace(): void {
    this.workspaceSaving.set(true);
    this.workspaceApi.save(this.workspaceVersion, this.projects(), this.workspaceHighlights).subscribe(res => {
      this.workspaceSaving.set(false);
      if (res.conflict) { this.workspaceConflict.set(true); return; }
      if (res.ok && res.version) this.workspaceVersion = res.version;
    });
  }

  reloadWorkspaceAfterConflict(): void { this.loadWorkspace(); }

  private loadStats(): void {
    this.statsLoading.set(true);
    this.myLibraryApi.getStats().subscribe(s => { this.stats.set(s); this.statsLoading.set(false); });
  }

  private loadBadges(): void {
    this.badgesLoading.set(true);
    this.myLibraryApi.getBadges().subscribe(b => { this.badges.set(b); this.badgesLoading.set(false); });
  }

  private loadBorrowing(): void {
    this.borrowingLoading.set(true);
    this.myLibraryApi.getBorrowing().subscribe(items => this.borrowing.set(items));
    this.myLibraryApi.getReserved().subscribe(items => { this.reserved.set(items); this.borrowingLoading.set(false); });
  }

  private loadDigital(): void {
    this.digitalLoading.set(true);
    this.myLibraryApi.getDigitalBorrowing().subscribe(items => this.digitalBorrowing.set(items));
    this.myLibraryApi.getDigitalReserved().subscribe(items => { this.digitalReserved.set(items); this.digitalLoading.set(false); });
  }

  returnDigital(loanPublicId: string): void {
    this.myLibraryApi.returnDigital(loanPublicId).subscribe(res => { if (res.ok) this.loadDigital(); });
  }

  cancelDigitalReservation(publicId: string): void {
    this.myLibraryApi.cancelDigitalReservation(publicId).subscribe(res => { if (res.ok) this.loadDigital(); });
  }

  logout() {
    this.authService.logout();
  }
}
