import { Component, ElementRef, EventEmitter, Input, OnDestroy, Output, ViewChild, inject, signal, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { RoomBookingAdminService } from '../../../services/map/room-booking.service';
import { RoomCheckInResult } from '../../../models/map/room-booking';
import { parseRoomBookingQr } from '../../../shared/room-booking-qr';
import { FaceCaptureComponent } from '../../../components/face-capture/face-capture';

const KIOSK_ROOM_KEY = 'roomCheckinKioskRoom';
const SAME_CODE_COOLDOWN_MS = 4000;
/** Sau mỗi kết quả nhận diện, camera khuôn mặt tự bật lại cho người kế tiếp. */
const FACE_RESTART_MS = 4000;

/** Quét mã QR check-in phòng học nhóm: nhận từ máy quét USB/nhập tay (ô nhập + Enter) hoặc camera (jsQR).
 *  Chọn "Phòng tại kiosk" thì chỉ nhận lượt đặt của đúng phòng đó. Port ELIB-LRC 09-29; nút "Nhận diện khuôn mặt" (09-30): camera tự
 *  chụp 2,5 giây/lần, chỉ so với bạn đọc có lượt đã duyệt đang tới giờ (đúng phòng kiosk nếu có chọn), nhận ra thì check-in.
 *  Tenant: backend chỉ nhận lượt đặt thuộc đơn vị của thủ thư. */
@Component({
  selector: 'app-room-checkin-scanner',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule, TranslateModule, FaceCaptureComponent],
  templateUrl: './room-checkin-scanner.html'
})
export class RoomCheckinScannerComponent implements AfterViewInit, OnDestroy {
  private svc = inject(RoomBookingAdminService);
  private translate = inject(TranslateService);

  @Input() rooms: { id: number; name: string }[] = [];
  @Output() closed = new EventEmitter<void>();
  @Output() checkedIn = new EventEmitter<void>();
  @ViewChild('codeInput') codeInput?: ElementRef<HTMLInputElement>;
  @ViewChild('video') video?: ElementRef<HTMLVideoElement>;
  @ViewChild(FaceCaptureComponent) faceCapture?: FaceCaptureComponent;

  kioskRoomId = signal<number | null>(this.readKioskRoom());
  code = '';
  busy = signal(false);
  result = signal<RoomCheckInResult | null>(null);
  cameraOn = signal(false);
  cameraError = signal<string | null>(null);

  private stream: MediaStream | null = null;
  private frame = 0;
  private lastScan = { code: '', at: 0 };
  private faceRestart: ReturnType<typeof setTimeout> | null = null;

  /** Gửi ảnh camera cho backend nhận diện + check-in; null = chưa nhận ra ai, camera quét tiếp. */
  readonly faceSubmit = (base64: string) => this.svc.checkInByFace(base64, this.kioskRoomId());

  ngAfterViewInit(): void { this.focusInput(); }

  ngOnDestroy(): void { this.stopCamera(); this.stopFace(); }

  onFaceResult(r: RoomCheckInResult): void {
    this.result.set(r);
    if (r.ok) { this.checkedIn.emit(); navigator.vibrate?.(120); }
    if (this.faceRestart) clearTimeout(this.faceRestart);
    this.faceRestart = setTimeout(() => this.faceCapture?.open(), FACE_RESTART_MS);
  }

  private stopFace(): void {
    if (this.faceRestart) { clearTimeout(this.faceRestart); this.faceRestart = null; }
    this.faceCapture?.close();
  }

  setKioskRoom(id: number | null): void {
    this.kioskRoomId.set(id);
    try { id == null ? localStorage.removeItem(KIOSK_ROOM_KEY) : localStorage.setItem(KIOSK_ROOM_KEY, String(id)); } catch { /* bỏ qua */ }
    this.focusInput();
  }

  submitInput(): void {
    const text = this.code;
    this.code = '';
    this.submit(text);
  }

  submit(text: string): void {
    if (this.busy()) return;
    const publicId = parseRoomBookingQr(text);
    if (!publicId) {
      if (text.trim()) this.result.set({ ok: false, message: this.translate.instant('ROOM_CHECKIN.INVALID_CODE'), booking: null });
      this.focusInput();
      return;
    }
    this.busy.set(true);
    this.svc.checkIn(publicId, this.kioskRoomId()).subscribe(r => {
      this.busy.set(false);
      this.result.set(r);
      if (r.ok) { this.checkedIn.emit(); navigator.vibrate?.(120); }
      this.focusInput();
    });
  }

  async toggleCamera(): Promise<void> {
    if (this.cameraOn()) { this.stopCamera(); return; }
    this.stopFace();
    this.cameraError.set(null);
    try {
      this.stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' }, audio: false });
      this.cameraOn.set(true);
      setTimeout(() => {
        const v = this.video?.nativeElement;
        if (!v || !this.stream) return;
        v.srcObject = this.stream;
        v.play().catch(() => { /* autoplay bị chặn: video vẫn hiển thị khi người dùng tương tác */ });
        this.scanLoop();
      });
    } catch {
      this.cameraError.set(this.translate.instant('ROOM_CHECKIN.CAMERA_DENIED'));
      this.stopCamera();
    }
  }

  close(): void {
    this.stopCamera();
    this.stopFace();
    this.closed.emit();
  }

  private async scanLoop(): Promise<void> {
    const { default: jsQR } = await import('jsqr');
    const canvas = document.createElement('canvas');
    let last = 0;
    const tick = (t: number) => {
      if (!this.cameraOn()) return;
      this.frame = requestAnimationFrame(tick);
      if (t - last < 250) return;
      last = t;
      const v = this.video?.nativeElement;
      if (!v || v.readyState < 2 || !v.videoWidth) return;
      const w = Math.min(640, v.videoWidth), h = Math.round(v.videoHeight * (w / v.videoWidth));
      canvas.width = w; canvas.height = h;
      const ctx = canvas.getContext('2d', { willReadFrequently: true });
      if (!ctx) return;
      ctx.drawImage(v, 0, 0, w, h);
      const hit = jsQR(ctx.getImageData(0, 0, w, h).data, w, h, { inversionAttempts: 'dontInvert' });
      if (!hit?.data) return;
      const now = Date.now();
      if (hit.data === this.lastScan.code && now - this.lastScan.at < SAME_CODE_COOLDOWN_MS) return;
      this.lastScan = { code: hit.data, at: now };
      this.submit(hit.data);
    };
    this.frame = requestAnimationFrame(tick);
  }

  stopCamera(): void {
    cancelAnimationFrame(this.frame);
    this.stream?.getTracks().forEach(t => t.stop());
    this.stream = null;
    this.cameraOn.set(false);
  }

  private focusInput(): void { setTimeout(() => this.codeInput?.nativeElement.focus()); }

  private readKioskRoom(): number | null {
    try { const v = Number(localStorage.getItem(KIOSK_ROOM_KEY)); return v > 0 ? v : null; } catch { return null; }
  }
}
