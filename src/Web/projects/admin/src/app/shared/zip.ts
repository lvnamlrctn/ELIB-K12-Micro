/**
 * Đọc file .zip ngay trên trình duyệt (DecompressionStream 'deflate-raw') — đủ cho zip ảnh thông thường
 * (stored/deflate, không mã hoá, không zip64). Server không phải nhận file zip nên không lo zip bomb phía server.
 */
export interface ZipEntry {
  /** Tên file, đã bỏ thư mục. */
  name: string;
  /** Dung lượng sau giải nén (theo central directory). */
  size: number;
  read(): Promise<Blob>;
}

export async function readZip(file: Blob): Promise<ZipEntry[]> {
  const buffer = await file.arrayBuffer();
  const view = new DataView(buffer);
  const eocd = findEndOfCentralDirectory(view);
  if (eocd < 0) throw new Error('File không phải .zip hợp lệ.');
  const count = view.getUint16(eocd + 10, true);
  let offset = view.getUint32(eocd + 16, true);
  if (count === 0xffff || offset === 0xffffffff) throw new Error('File .zip quá lớn (zip64) — chia thành nhiều file nhỏ hơn.');

  const entries: ZipEntry[] = [];
  for (let i = 0; i < count; i++) {
    if (view.getUint32(offset, true) !== 0x02014b50) throw new Error('File .zip bị hỏng.');
    const flags = view.getUint16(offset + 8, true);
    const method = view.getUint16(offset + 10, true);
    const compressedSize = view.getUint32(offset + 20, true);
    const size = view.getUint32(offset + 24, true);
    const nameLength = view.getUint16(offset + 28, true);
    const extraLength = view.getUint16(offset + 30, true);
    const commentLength = view.getUint16(offset + 32, true);
    const localOffset = view.getUint32(offset + 42, true);
    const path = new TextDecoder().decode(new Uint8Array(buffer, offset + 46, nameLength));
    offset += 46 + nameLength + extraLength + commentLength;

    const name = path.split('/').pop() ?? '';
    // Bỏ thư mục, file ẩn và rác của macOS (__MACOSX/, .DS_Store).
    if (!name || name.startsWith('.') || path.startsWith('__MACOSX/')) continue;
    if (flags & 1) throw new Error('File .zip có mật khẩu — hãy nén lại không đặt mật khẩu.');

    entries.push({
      name,
      size,
      read: async () => {
        const start = localOffset + 30 + view.getUint16(localOffset + 26, true) + view.getUint16(localOffset + 28, true);
        const data = new Blob([new Uint8Array(buffer, start, compressedSize)]);
        if (method === 0) return data;
        if (method !== 8) throw new Error(`Kiểu nén ${method} không được hỗ trợ.`);
        return new Response(data.stream().pipeThrough(new DecompressionStream('deflate-raw'))).blob();
      },
    });
  }
  return entries;
}

function findEndOfCentralDirectory(view: DataView): number {
  // Bản ghi cuối dài 22 byte + chú thích tối đa 65535 byte.
  for (let i = view.byteLength - 22; i >= Math.max(0, view.byteLength - 22 - 0xffff); i--) {
    if (view.getUint32(i, true) === 0x06054b50) return i;
  }
  return -1;
}
