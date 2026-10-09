import { Component, ElementRef, EventEmitter, Input, OnDestroy, Output, ViewChild, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule } from '@ngx-translate/core';
import { Observable } from 'rxjs';
import { FaceRecognitionService } from '../../services/circulation/face-recognition.service';

const CAPTURE_INTERVAL_MS = 2500;
/** Chiều rộng tối đa ảnh gửi lên nhận diện — đủ cho so khớp khuôn mặt, giảm dung lượng request (port ELIB-LRC 09-30). */
const MAX_CAPTURE_WIDTH = 640;

/** Camera nhận diện khuôn mặt dùng chung: màn Mượn/Trả, Vào/Ra (mặc định — nhận diện bạn đọc, tự chụp 2,5 giây/lần) và check-in
 *  phòng học nhóm (port ELIB-LRC 09-30: kiosk tự quét qua `submitFn`; bạn đọc tự xác minh ở OPAC với `manual`). */
@Component({
  selector: 'app-face-capture',
  standalone: true,
  imports: [CommonModule, MatIconModule, TranslateModule],
  templateUrl: './face-capture.html'
})
export class FaceCaptureComponent implements OnDestroy {
  /** Gửi ảnh (base64 JPEG thuần) đi xử lý; kết quả null = chưa nhận ra, camera tiếp tục. Mặc định: nhận diện bạn đọc. */
  @Input() submitFn?: (base64: string) => Observable<unknown | null>;
  /** true: chỉ chụp khi bấm "Chụp & xác minh" (không tự quét định kỳ — giới hạn số lượt gọi AI). */
  @Input() manual = false;
  @Input() titleKey = 'FACE_CAPTURE.TITLE';
  @Input() hintKey = 'FACE_CAPTURE.HINT';
  /** Nhãn chữ cạnh biểu tượng nút mở (rỗng = chỉ biểu tượng như màn Mượn/Trả). */
  @Input() openLabel = '';
  @Input() openClass = 'bg-gray-100 hover:bg-gray-200 text-gray-700 px-3 rounded-lg flex items-center gap-1 text-sm';

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  @Output() matched = new EventEmitter<any>();
  @ViewChild('video') videoRef?: ElementRef<HTMLVideoElement>;
  @ViewChild('canvas') canvasRef?: ElementRef<HTMLCanvasElement>;

  private faceRecognition = inject(FaceRecognitionService);
  private stream: MediaStream | null = null;
  private timer: ReturnType<typeof setInterval> | null = null;

  isOpen = signal(false);
  isChecking = signal(false);
  error = signal<string | null>(null);

  async open(): Promise<void> {
    if (this.isOpen()) return;
    this.error.set(null);
    this.isOpen.set(true);
    try {
      this.stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'user' } });
      // Đợi @if render xong <video> trước khi gán srcObject.
      setTimeout(() => {
        const video = this.videoRef?.nativeElement;
        if (video) { video.srcObject = this.stream; video.play(); }
      });
      if (!this.manual) this.timer = setInterval(() => this.captureAndIdentify(), CAPTURE_INTERVAL_MS);
    } catch {
      this.error.set('FACE_CAPTURE.CAMERA_DENIED');
    }
  }

  close(): void {
    if (this.timer) { clearInterval(this.timer); this.timer = null; }
    this.stream?.getTracks().forEach(t => t.stop());
    this.stream = null;
    this.isChecking.set(false);
    this.isOpen.set(false);
  }

  captureAndIdentify(): void {
    if (this.isChecking()) return;
    const video = this.videoRef?.nativeElement;
    const canvas = this.canvasRef?.nativeElement;
    if (!video || !canvas || video.videoWidth === 0) return;

    const scale = Math.min(1, MAX_CAPTURE_WIDTH / video.videoWidth);
    canvas.width = Math.round(video.videoWidth * scale);
    canvas.height = Math.round(video.videoHeight * scale);
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    ctx.drawImage(video, 0, 0, canvas.width, canvas.height);

    const base64 = canvas.toDataURL('image/jpeg', 0.85).split(',')[1];
    if (!base64) return;

    this.isChecking.set(true);
    const request = this.submitFn ? this.submitFn(base64) : this.faceRecognition.identify(base64);
    request.subscribe({
      next: result => {
        this.isChecking.set(false);
        if (result) { this.matched.emit(result); this.close(); }
      },
      error: () => this.isChecking.set(false)
    });
  }

  ngOnDestroy(): void { this.close(); }
}
