import { Component, ElementRef, OnDestroy, OnInit, input, output, signal, viewChild } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { Modal } from './ui';

/** Ảnh vừa chụp: file JPEG (để upload) và data URL (để xem trước / gửi nhận diện). */
export interface CapturedPhoto { file: File; dataUrl: string; }

/**
 * Thu nhỏ ảnh chọn từ tệp về cạnh dài tối đa <paramref name="maxSize"/> px, JPEG — ảnh bạn đọc gửi nhận diện không cần lớn, và ảnh chưa thu nhỏ
 * (tới vài MB) làm yêu cầu nhận diện nặng. Không đọc được ảnh (định dạng lạ) thì trả nguyên tệp.
 */
export async function shrinkImage(file: File, maxSize = 1024): Promise<File> {
  try {
    const bitmap = await createImageBitmap(file);
    const scale = Math.min(1, maxSize / Math.max(bitmap.width, bitmap.height));
    if (scale === 1 && file.type === 'image/jpeg' && file.size <= 400_000) {
      bitmap.close();
      return file;
    }
    const canvas = document.createElement('canvas');
    canvas.width = Math.round(bitmap.width * scale);
    canvas.height = Math.round(bitmap.height * scale);
    canvas.getContext('2d')?.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
    bitmap.close();
    const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, 'image/jpeg', 0.88));
    return blob ? new File([blob], file.name.replace(/\.[^.]+$/, '') + '.jpg', { type: 'image/jpeg' }) : file;
  } catch {
    return file;
  }
}

/**
 * Chụp ảnh bằng camera (getUserMedia — cần HTTPS hoặc localhost). Hộp thoại có khung xem trực tiếp; "Chụp" thu ảnh JPEG đã thu nhỏ
 * (cạnh dài tối đa <see cref="maxSize"/>) rồi tắt camera. Dùng cho ảnh thẻ / ảnh khuôn mặt bổ sung của bạn đọc và nhận diện ở quầy mượn trả.
 */
@Component({
  selector: 'app-camera-capture',
  imports: [Modal, TranslatePipe],
  template: `
    <app-modal [title]="title()" widthClass="max-w-xl" (closed)="cancelled.emit()">
      <div class="relative bg-black rounded-lg overflow-hidden aspect-[4/3]">
        <video #video autoplay playsinline muted class="w-full h-full object-cover"></video>
        @if (!ready() && !error()) {
          <div class="absolute inset-0 flex items-center justify-center text-white"><span class="material-icons animate-spin text-4xl">autorenew</span></div>
        }
      </div>
      <canvas #canvas class="hidden"></canvas>
      @if (error(); as e) { <p class="mt-3 text-sm text-red-600">{{ e | translate }}</p> }
      <p class="mt-3 text-xs text-gray-500">{{ hint() | translate }}</p>
      <ng-container footer>
        <button type="button" class="btn-secondary" (click)="cancelled.emit()">{{ 'COMMON.CANCEL' | translate }}</button>
        <button type="button" class="btn-primary" [disabled]="!ready()" (click)="capture()">
          <span class="material-icons text-[18px]">photo_camera</span> {{ captureLabel() | translate }}</button>
      </ng-container>
    </app-modal>
  `,
})
export class CameraCapture implements OnInit, OnDestroy {
  readonly title = input('CAMERA.TITLE');
  readonly hint = input('CAMERA.HINT');
  readonly captureLabel = input('CAMERA.CAPTURE');
  /** Cạnh dài tối đa của ảnh chụp (px) — thu nhỏ trước khi upload / gửi đi. */
  readonly maxSize = input(1024);
  readonly captured = output<CapturedPhoto>();
  readonly cancelled = output<void>();

  private readonly video = viewChild.required<ElementRef<HTMLVideoElement>>('video');
  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
  protected readonly ready = signal(false);
  protected readonly error = signal<string | null>(null);
  private stream: MediaStream | null = null;

  async ngOnInit(): Promise<void> {
    if (!navigator.mediaDevices?.getUserMedia) {
      this.error.set('CAMERA.UNSUPPORTED');
      return;
    }
    try {
      this.stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'user' }, audio: false });
      const el = this.video().nativeElement;
      el.srcObject = this.stream;
      el.onloadedmetadata = () => this.ready.set(true);
    } catch {
      this.error.set('CAMERA.DENIED');
    }
  }

  protected capture(): void {
    const video = this.video().nativeElement;
    const canvas = this.canvas().nativeElement;
    if (!video.videoWidth || !video.videoHeight) return;
    const scale = Math.min(1, this.maxSize() / Math.max(video.videoWidth, video.videoHeight));
    canvas.width = Math.round(video.videoWidth * scale);
    canvas.height = Math.round(video.videoHeight * scale);
    canvas.getContext('2d')?.drawImage(video, 0, 0, canvas.width, canvas.height);
    const dataUrl = canvas.toDataURL('image/jpeg', 0.88);
    canvas.toBlob((blob) => {
      if (!blob) return;
      this.stop();
      this.captured.emit({ file: new File([blob], `camera-${Date.now()}.jpg`, { type: 'image/jpeg' }), dataUrl });
    }, 'image/jpeg', 0.88);
  }

  private stop(): void {
    this.stream?.getTracks().forEach((t) => t.stop());
    this.stream = null;
  }

  ngOnDestroy(): void {
    this.stop();
  }
}
