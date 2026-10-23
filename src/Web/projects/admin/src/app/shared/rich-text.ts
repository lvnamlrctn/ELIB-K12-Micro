import { AfterViewInit, Component, ElementRef, OnDestroy, ViewEncapsulation, effect, input, model, signal, viewChild } from '@angular/core';
import { Editor } from '@tiptap/core';
import Image from '@tiptap/extension-image';
import { Table, TableCell, TableHeader, TableRow } from '@tiptap/extension-table';
import StarterKit from '@tiptap/starter-kit';
import { TranslatePipe } from '@ngx-translate/core';
import { MediaFile } from '../core/api';
import { MediaPicker } from './media-picker';

/** Địa chỉ liên kết được phép chèn: http(s), mailto, tel hoặc đường dẫn nội bộ "/…" — server vẫn làm sạch lại khi lưu. */
const SAFE_HREF = /^(https?:\/\/|mailto:|tel:|\/(?!\/))/i;

/** Ảnh có thêm thuộc tính class (vị trí img-left/center/right); kích thước là width/height do kéo góc ảnh — server chỉ giữ đúng các lớp này và kiểm chiều rộng. */
const ResizableImage = Image.extend({
  addAttributes() {
    return {
      ...this.parent?.(),
      class: {
        default: null,
        parseHTML: (el: HTMLElement) => el.getAttribute('class'),
        renderHTML: (attrs: Record<string, unknown>) => (attrs['class'] ? { class: attrs['class'] as string } : {}),
      },
    };
  },
});

const ALIGN_CLASSES = ['img-left', 'img-center', 'img-right'];

/**
 * Trình soạn nội dung CMS (TipTap, giấy phép MIT): đậm/nghiêng/gạch chân, tiêu đề, danh sách, trích dẫn, liên kết, bảng, ảnh từ thư viện ảnh của đơn vị.
 * Giá trị là HTML. HTML này luôn được server làm sạch (danh sách thẻ cho phép) trước khi lưu — trình soạn chỉ là tiện lợi, không phải hàng rào bảo mật.
 */
@Component({
  selector: 'app-rich-text',
  encapsulation: ViewEncapsulation.None,
  imports: [MediaPicker, TranslatePipe],
  template: `
    <div class="border border-gray-300 rounded-lg overflow-hidden bg-white">
      @if (!disabled()) {
        <div class="flex flex-wrap items-center gap-0.5 px-2 py-1 border-b border-gray-200 bg-gray-50" role="toolbar" [attr.aria-label]="'RICH.TOOLBAR' | translate">
          @for (b of buttons; track b.key) {
            <button type="button" class="icon-btn !w-8 !h-8" [class.bg-blue-100]="active(b.key)" [attr.aria-pressed]="active(b.key)" [attr.aria-label]="b.label | translate" [title]="b.label | translate" (click)="run(b.key)">
              <span class="material-icons text-[18px]">{{ b.icon }}</span>
            </button>
          }
        </div>
      }
      @if (imageSelected()) {
        <div class="flex flex-wrap items-center gap-1 px-2 py-1 border-b border-gray-200 bg-blue-50 text-xs" role="toolbar" [attr.aria-label]="'RICH.IMAGE' | translate">
          <span class="material-icons text-[16px] text-blue-700">image</span>
          <button type="button" class="px-2 py-1 rounded border border-gray-300 bg-white hover:bg-gray-50" (click)="resetImageSize()">{{ 'RICH.IMG_RESET' | translate }}</button>
          <span class="mx-1 h-4 w-px bg-gray-300"></span>
          @for (a of imageAligns; track a.key) {
            <button type="button" class="icon-btn !w-7 !h-7 border border-gray-300 bg-white" [class.!bg-blue-600]="imageHas(a.key)" [class.!text-white]="imageHas(a.key)"
              [attr.aria-pressed]="imageHas(a.key)" [attr.aria-label]="a.label | translate" [title]="a.label | translate" (click)="setImage(a.key)">
              <span class="material-icons text-[16px]">{{ a.icon }}</span></button>
          }
        </div>
      }
      <div #host class="prose-editor px-3 py-2 min-h-[220px] max-h-[55vh] overflow-auto"></div>
    </div>
    @if (picking()) { <app-media-picker purpose="cms-image" (picked)="insertImage($event)" (closed)="picking.set(false)" /> }
  `,
  styles: `
    .prose-editor .tiptap { outline: none; min-height: 200px; }
    .prose-editor .tiptap h2 { font-size: 1.4rem; font-weight: 700; margin: 0.8em 0 0.4em; }
    .prose-editor .tiptap h3 { font-size: 1.2rem; font-weight: 700; margin: 0.8em 0 0.4em; }
    .prose-editor .tiptap h4 { font-size: 1.05rem; font-weight: 700; margin: 0.8em 0 0.4em; }
    .prose-editor .tiptap ul { list-style: disc; padding-left: 1.4rem; }
    .prose-editor .tiptap ol { list-style: decimal; padding-left: 1.4rem; }
    .prose-editor .tiptap blockquote { border-left: 3px solid #cbd5e1; padding-left: 0.8rem; color: #475569; margin: 0.6em 0; }
    .prose-editor .tiptap a { color: #1d4ed8; text-decoration: underline; }
    .prose-editor .tiptap img { max-width: 100%; height: auto; }
    .prose-editor .tiptap img.img-left { float: left; margin: 0 1rem 0.5rem 0; }
    .prose-editor .tiptap img.img-right { float: right; margin: 0 0 0.5rem 1rem; }
    .prose-editor .tiptap img.img-center { display: block; margin: 0.5rem auto; }
    .prose-editor .tiptap img.ProseMirror-selectednode { outline: 2px solid #2563eb; }
    .prose-editor .tiptap [data-resize-container] { max-width: 100%; width: fit-content; }
    .prose-editor .tiptap [data-resize-container]:has(img.img-left) { float: left; margin: 0 1rem 0.5rem 0; }
    .prose-editor .tiptap [data-resize-container]:has(img.img-right) { float: right; margin: 0 0 0.5rem 1rem; }
    .prose-editor .tiptap [data-resize-container]:has(img.img-center) { margin: 0.5rem auto; }
    .prose-editor .tiptap [data-resize-container] img { float: none; margin: 0; display: block; }
    .prose-editor .tiptap [data-resize-container].ProseMirror-selectednode img { outline: 2px solid #2563eb; }
    .prose-editor .tiptap [data-resize-handle] { display: none; width: 12px; height: 12px; background: #2563eb; border: 2px solid #fff; border-radius: 3px; box-shadow: 0 0 0 1px #2563eb; z-index: 5; }
    .prose-editor .tiptap[contenteditable='true'] [data-resize-container].ProseMirror-selectednode [data-resize-handle],
    .prose-editor .tiptap[contenteditable='true'] [data-resize-container]:hover [data-resize-handle],
    .prose-editor .tiptap[contenteditable='true'] [data-resize-container][data-resize-state='true'] [data-resize-handle] { display: block; }
    .prose-editor .tiptap [data-resize-handle='top-left'] { transform: translate(-50%, -50%); cursor: nwse-resize; }
    .prose-editor .tiptap [data-resize-handle='top-right'] { transform: translate(50%, -50%); cursor: nesw-resize; }
    .prose-editor .tiptap [data-resize-handle='bottom-left'] { transform: translate(-50%, 50%); cursor: nesw-resize; }
    .prose-editor .tiptap [data-resize-handle='bottom-right'] { transform: translate(50%, 50%); cursor: nwse-resize; }
    .prose-editor .tiptap table { border-collapse: collapse; width: 100%; margin: 0.6em 0; }
    .prose-editor .tiptap td, .prose-editor .tiptap th { border: 1px solid #cbd5e1; padding: 4px 8px; min-width: 40px; vertical-align: top; }
    .prose-editor .tiptap th { background: #f1f5f9; }
  `,
})
export class RichText implements AfterViewInit, OnDestroy {
  /** HTML (hai chiều). */
  readonly value = model('');
  readonly disabled = input(false);

  private readonly host = viewChild.required<ElementRef<HTMLElement>>('host');
  private editor: Editor | null = null;
  protected readonly picking = signal(false);
  protected readonly imageAligns = [
    { key: 'img-left', icon: 'format_align_left', label: 'RICH.IMG_LEFT' }, { key: 'img-center', icon: 'format_align_center', label: 'RICH.IMG_CENTER' },
    { key: 'img-right', icon: 'format_align_right', label: 'RICH.IMG_RIGHT' },
  ];
  private readonly tick = signal(0); // làm mới trạng thái nút (đậm/nghiêng…) khi con trỏ đổi

  protected readonly buttons = [
    { key: 'undo', icon: 'undo', label: 'RICH.UNDO' }, { key: 'redo', icon: 'redo', label: 'RICH.REDO' },
    { key: 'h2', icon: 'title', label: 'RICH.H2' }, { key: 'h3', icon: 'text_fields', label: 'RICH.H3' },
    { key: 'bold', icon: 'format_bold', label: 'RICH.BOLD' }, { key: 'italic', icon: 'format_italic', label: 'RICH.ITALIC' },
    { key: 'underline', icon: 'format_underlined', label: 'RICH.UNDERLINE' }, { key: 'strike', icon: 'strikethrough_s', label: 'RICH.STRIKE' },
    { key: 'bullet', icon: 'format_list_bulleted', label: 'RICH.BULLET' }, { key: 'ordered', icon: 'format_list_numbered', label: 'RICH.ORDERED' },
    { key: 'quote', icon: 'format_quote', label: 'RICH.QUOTE' }, { key: 'link', icon: 'link', label: 'RICH.LINK' },
    { key: 'image', icon: 'image', label: 'RICH.IMAGE' }, { key: 'table', icon: 'table_chart', label: 'RICH.TABLE' },
    { key: 'rule', icon: 'horizontal_rule', label: 'RICH.RULE' }, { key: 'clear', icon: 'format_clear', label: 'RICH.CLEAR' },
  ];

  constructor() {
    // Đổi bản ghi đang sửa (giá trị đến từ ngoài) thì nạp lại nội dung; gõ trong trình soạn thì không ghi đè.
    effect(() => {
      const next = this.value();
      if (this.editor && next !== this.editor.getHTML()) this.editor.commands.setContent(next, { emitUpdate: false });
    });
    effect(() => {
      this.editor?.setEditable(!this.disabled());
    });
  }

  ngAfterViewInit(): void {
    this.editor = new Editor({
      element: this.host().nativeElement,
      extensions: [
        StarterKit.configure({ heading: { levels: [2, 3, 4] }, link: { openOnClick: false, autolink: false, protocols: ['http', 'https', 'mailto', 'tel'] } }),
        ResizableImage.configure({
          inline: false,
          resize: { enabled: true, directions: ['top-left', 'top-right', 'bottom-left', 'bottom-right'], minWidth: 40, minHeight: 40, alwaysPreserveAspectRatio: true },
        }),
        Table.configure({ resizable: false }), TableRow, TableHeader, TableCell,
      ],
      content: this.value(),
      editable: !this.disabled(),
      onUpdate: ({ editor }) => this.value.set(editor.isEmpty ? '' : editor.getHTML()),
      onSelectionUpdate: () => this.tick.update((n) => n + 1),
      onTransaction: () => this.tick.update((n) => n + 1),
    });
  }

  ngOnDestroy(): void {
    this.editor?.destroy();
    this.editor = null;
  }

  /** Đang chọn một ảnh → hiện thanh chỉnh kích thước / vị trí. */
  protected imageSelected(): boolean {
    this.tick();
    return !!this.editor?.isActive('image');
  }

  protected imageHas(cssClass: string): boolean {
    this.tick();
    return ((this.editor?.getAttributes('image')['class'] as string | null) ?? '').split(' ').includes(cssClass);
  }

  /** Đặt vị trí cho ảnh đang chọn; bấm lại nút đang bật thì bỏ vị trí (ảnh về dòng chữ bình thường). */
  protected setImage(cssClass: string): void {
    const editor = this.editor;
    if (!editor) return;
    const current = ((editor.getAttributes('image')['class'] as string | null) ?? '').split(' ').filter(Boolean);
    const kept = current.filter((c) => !ALIGN_CLASSES.includes(c));
    const next = current.includes(cssClass) ? kept : [...kept, cssClass];
    editor.chain().focus().updateAttributes('image', { class: next.length ? next.join(' ') : null }).run();
  }

  /** Về kích thước gốc của ảnh (bỏ chiều rộng/cao đã kéo). */
  protected resetImageSize(): void {
    this.editor?.chain().focus().updateAttributes('image', { width: null, height: null }).run();
  }

  protected active(key: string): boolean {
    this.tick();
    const e = this.editor;
    if (!e) return false;
    switch (key) {
      case 'h2': return e.isActive('heading', { level: 2 });
      case 'h3': return e.isActive('heading', { level: 3 });
      case 'bold': case 'italic': case 'underline': case 'strike': return e.isActive(key);
      case 'bullet': return e.isActive('bulletList');
      case 'ordered': return e.isActive('orderedList');
      case 'quote': return e.isActive('blockquote');
      case 'link': return e.isActive('link');
      default: return false;
    }
  }

  protected run(key: string): void {
    const chain = this.editor?.chain().focus();
    if (!chain) return;
    switch (key) {
      case 'undo': chain.undo().run(); break;
      case 'redo': chain.redo().run(); break;
      case 'h2': chain.toggleHeading({ level: 2 }).run(); break;
      case 'h3': chain.toggleHeading({ level: 3 }).run(); break;
      case 'bold': chain.toggleBold().run(); break;
      case 'italic': chain.toggleItalic().run(); break;
      case 'underline': chain.toggleUnderline().run(); break;
      case 'strike': chain.toggleStrike().run(); break;
      case 'bullet': chain.toggleBulletList().run(); break;
      case 'ordered': chain.toggleOrderedList().run(); break;
      case 'quote': chain.toggleBlockquote().run(); break;
      case 'link': this.setLink(); break;
      case 'image': this.picking.set(true); break;
      case 'table': chain.insertTable({ rows: 3, cols: 3, withHeaderRow: true }).run(); break;
      case 'rule': chain.setHorizontalRule().run(); break;
      case 'clear': chain.unsetAllMarks().clearNodes().run(); break;
    }
  }

  protected insertImage(file: MediaFile): void {
    this.picking.set(false);
    if (file.url) this.editor?.chain().focus().setImage({ src: file.url, alt: file.fileName }).run();
  }

  private setLink(): void {
    const e = this.editor;
    if (!e) return;
    const current = (e.getAttributes('link')['href'] as string | undefined) ?? '';
    const input = window.prompt('URL', current);
    if (input === null) return;
    const href = input.trim();
    if (!href) {
      e.chain().focus().extendMarkRange('link').unsetLink().run();
    } else if (SAFE_HREF.test(href)) {
      e.chain().focus().extendMarkRange('link').setLink({ href }).run();
    }
  }
}
