/**
 * Mã vạch Code 128 (tập B — chữ, số, ký hiệu ASCII 32..126) cho số thẻ bạn đọc: in thẻ ở Admin, thẻ điện tử ở OPAC.
 * Thay directive appBarcode (JsBarcode) của ELIB-LRC — không thêm thư viện. Hàm thuần, không phụ thuộc Angular (kiểm thử bằng node --test).
 */

/** Độ rộng vạch/khoảng trắng (đơn vị module) của 107 ký hiệu Code 128, theo bảng chuẩn ISO/IEC 15417. */
export const PATTERNS: readonly string[] = [
  '212222', '222122', '222221', '121223', '121322', '131222', '122213', '122312', '132212', '221213',
  '221312', '231212', '112232', '122132', '122231', '113222', '123122', '123221', '223211', '221132',
  '221231', '213212', '223112', '312131', '311222', '321122', '321221', '312212', '322112', '322211',
  '212123', '212321', '232121', '111323', '131123', '131321', '112313', '132113', '132311', '211313',
  '231113', '231311', '112133', '112331', '132131', '113123', '113321', '133121', '313121', '211331',
  '231131', '213113', '213311', '213131', '311123', '311321', '331121', '312113', '312311', '332111',
  '314111', '221411', '431111', '111224', '111422', '121124', '121421', '141122', '141221', '112214',
  '112412', '122114', '122411', '142112', '142211', '241211', '221114', '413111', '241112', '134111',
  '111242', '121142', '121241', '114212', '124112', '124211', '411212', '421112', '421211', '212141',
  '214121', '412121', '111143', '111341', '131141', '114113', '114311', '411113', '411311', '113141',
  '114131', '311141', '411131', '211412', '211214', '211232', '2331112',
];

const START_B = 104;
const STOP = 106;

/** Giá trị ký hiệu: start B, dữ liệu, ký tự kiểm tra (mod 103), stop. Ký tự ngoài ASCII 32..126 → lỗi. */
export function code128Values(text: string): number[] {
  const values = [START_B];
  for (const ch of text) {
    const code = ch.charCodeAt(0);
    if (ch.length !== 1 || code < 32 || code > 126) throw new Error(`Code 128 B: ký tự không mã hoá được '${ch}'`);
    values.push(code - 32);
  }
  const checksum = values.reduce((sum, v, i) => sum + v * (i === 0 ? 1 : i), 0) % 103;
  return [...values, checksum, STOP];
}

/** Dãy độ rộng (module) xen kẽ vạch đen / trắng, bắt đầu bằng vạch đen. */
export function code128Widths(text: string): number[] {
  return code128Values(text).flatMap((v) => [...PATTERNS[v]].map(Number));
}

/** Các vạch đen dạng {x, w} (đơn vị module) và tổng chiều rộng — để vẽ SVG; kèm vùng trắng 10 module hai bên. */
export function code128Bars(text: string, quiet = 10): { bars: { x: number; w: number }[]; width: number } {
  const bars: { x: number; w: number }[] = [];
  let x = quiet;
  code128Widths(text).forEach((w, i) => {
    if (i % 2 === 0) bars.push({ x, w });
    x += w;
  });
  return { bars, width: x + quiet };
}

/** Giải mã lại từ dãy độ rộng (kiểm thử, và để test giao diện đọc được mã vạch đã vẽ). Sai ký tự kiểm tra → null. */
export function decodeCode128(widths: number[]): string | null {
  const values: number[] = [];
  for (let i = 0; i < widths.length; ) {
    const len = values.length > 0 && widths.length - i === 7 ? 7 : 6;
    const v = PATTERNS.indexOf(widths.slice(i, i + len).join(''));
    if (v < 0) return null;
    values.push(v);
    i += len;
  }
  if (values[0] !== START_B || values.at(-1) !== STOP || values.length < 3) return null;
  const data = values.slice(1, -2);
  const checksum = [START_B, ...data].reduce((sum, v, i) => sum + v * (i === 0 ? 1 : i), 0) % 103;
  return checksum === values.at(-2) ? data.map((v) => String.fromCharCode(v + 32)).join('') : null;
}
