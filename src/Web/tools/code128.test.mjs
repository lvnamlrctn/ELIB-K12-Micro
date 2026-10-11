// node --test tools/code128.test.mjs — bộ mã hoá Code 128 dùng cho in thẻ/thẻ điện tử (projects/shared/code128.ts).
import assert from 'node:assert/strict';
import test from 'node:test';
import { PATTERNS, code128Bars, code128Values, code128Widths, decodeCode128 } from '../projects/shared/code128.ts';

test('bảng 107 ký hiệu: mỗi ký hiệu 11 module, stop 13 module, không trùng', () => {
  assert.equal(PATTERNS.length, 107);
  PATTERNS.forEach((p, i) => assert.equal([...p].reduce((s, c) => s + Number(c), 0), i === 106 ? 13 : 11, `ký hiệu ${i}`));
  assert.equal(new Set(PATTERNS).size, 107);
});

test('ký tự kiểm tra theo ví dụ chuẩn "PJJ123C" (start B) = 55', () => {
  const values = code128Values('PJJ123C');
  assert.deepEqual(values.slice(0, 1), [104]);
  assert.equal(values.at(-2), 55);
  assert.equal(values.at(-1), 106);
});

test('mã hoá rồi giải mã lại đúng số thẻ', () => {
  for (const card of ['HS-001', 'LTADA75A', 'sv.2026_01', ' ~!', 'E2E5A9F0C-2']) {
    assert.equal(decodeCode128(code128Widths(card)), card);
  }
});

test('vẽ: vạch đen bắt đầu sau vùng trắng, tổng chiều rộng = 11 * (start + n + kiểm tra) + 13 (stop) + 2 vùng trắng', () => {
  const { bars, width } = code128Bars('ABC', 10);
  assert.equal(bars[0].x, 10);
  assert.equal(width, 11 * (1 + 3 + 1) + 13 + 20);
});

test('ký tự ngoài ASCII in được bị từ chối; ký tự kiểm tra sai thì không giải mã', () => {
  assert.throws(() => code128Values('Thẻ'));
  const widths = code128Widths('AB');
  widths.splice(6, 6, ...PATTERNS[35].split('').map(Number)); // đổi 'A' thành 'C' mà giữ ký tự kiểm tra cũ
  assert.equal(decodeCode128(widths), null);
});
