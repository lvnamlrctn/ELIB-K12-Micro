#!/usr/bin/env node
// Kiểm tra đa ngữ của các app Angular (chạy trong CI: node tools/i18n-check.mjs):
//  1. vi.json và en.json của từng app có cùng bộ khoá;
//  2. mọi khoá viết thẳng trong code ('OPAC.SEARCH.TITLE', 'COMMON.SAVE'…) có trong từ điển;
//  3. thư mục đã chuyển đa ngữ (STRICT) không còn chữ tiếng Việt có dấu ngoài chú thích.
// Khoá ghép động (`ERRORS.${code}`, `OPAC.LANG_CODE.${c}`) khai báo ở DYNAMIC để không bị báo thiếu và được giữ trong từ điển.
// --list <app>: in các khoá đang dùng mà từ điển vi chưa có (để bổ sung).
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = fileURLToPath(new URL('..', import.meta.url));
const APPS = {
  opac: { src: 'projects/opac/src/app', i18n: 'projects/opac/public/i18n', strict: ['projects/opac/src/app'] },
  // Admin đã chuyển hết (GĐ C): mọi file — chuỗi dữ liệu (không phải chữ giao diện) đánh dấu i18n-data.
  admin: {
    src: 'projects/admin/src/app', i18n: 'projects/admin/public/i18n',
    strict: ['projects/admin/src/app'],
  },
};
const DYNAMIC = ['ERRORS.', 'OPAC.LANG_CODE.', 'OPAC.BOOK_DETAIL.STATUS_', 'READER_IMPORT.STEP_', 'READER_IMPORT.STATUS_', 'READER_HISTORY.ACTION_', 'TENANT_ADMIN.STATE_', 'TENANT_ADMIN.STEP_', 'TENANT_ADMIN.PACKAGE_', 'ROLE.ACTION_', 'SIP2.CMD_', 'CIRC_PLACE_PAGE.DAY_'];
const VIETNAMESE = /[àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ]/i;
const KEY = /['"`]((?:[A-Z][A-Z0-9_]*)(?:\.[A-Z0-9_]+)+)['"`]/g;

function files(dir) {
  return readdirSync(dir).flatMap((f) => {
    const p = join(dir, f);
    return statSync(p).isDirectory() ? files(p) : p.endsWith('.ts') && !p.endsWith('.spec.ts') ? [p] : [];
  });
}

function flatten(obj, prefix = '', out = new Map()) {
  for (const [k, v] of Object.entries(obj)) {
    const key = prefix ? `${prefix}.${k}` : k;
    if (v && typeof v === 'object') flatten(v, key, out);
    else out.set(key, v);
  }
  return out;
}

/**
 * Bỏ chú thích (TS và HTML trong template) để không báo chữ có dấu trong comment (giữ nguyên chuỗi).
 * Dòng có chú thích "i18n-data" là dữ liệu (tên vai trò dựng sẵn, nhãn MARC từ API…) — không phải chữ giao diện, bỏ qua.
 */
function stripComments(code) {
  return code.split('\n').map((l) => (l.includes('i18n-data') ? '' : l)).join('\n').replace(/\/\*[\s\S]*?\*\//g, '').replace(/<!--[\s\S]*?-->/g, '').replace(/(^|[^:'"`])\/\/.*$/gm, '$1');
}

const listApp = process.argv[2] === '--list' ? process.argv[3] : null;
let failed = false;
const fail = (msg) => {
  failed = true;
  console.error('✗ ' + msg);
};

for (const [app, cfg] of Object.entries(APPS)) {
  if (listApp && listApp !== app) continue;
  const dir = join(ROOT, cfg.i18n);
  let vi, en;
  try {
    vi = flatten(JSON.parse(readFileSync(join(dir, 'vi.json'), 'utf8')));
    en = flatten(JSON.parse(readFileSync(join(dir, 'en.json'), 'utf8')));
  } catch (e) {
    if (cfg.strict.length || listApp) fail(`${app}: không đọc được từ điển (${e.message})`);
    continue;
  }

  const used = new Map();
  for (const file of files(join(ROOT, cfg.src))) {
    const code = readFileSync(file, 'utf8');
    for (const m of stripComments(code).matchAll(KEY)) {
      if (!used.has(m[1])) used.set(m[1], relative(ROOT, file));
    }
    if (cfg.strict.some((s) => file.startsWith(join(ROOT, s)))) {
      stripComments(code).split('\n').forEach((line, i) => {
        if (VIETNAMESE.test(line)) fail(`${relative(ROOT, file)}:${i + 1}: còn chữ tiếng Việt chưa đưa vào từ điển: ${line.trim().slice(0, 100)}`);
      });
    }
  }

  const missing = [...used].filter(([k]) => !vi.has(k) && !DYNAMIC.some((d) => k === d || k === d.slice(0, -1)));
  if (listApp) {
    for (const [k, f] of missing) console.log(`${k}\t${f}`);
    continue;
  }
  for (const [k, f] of missing) fail(`${app}: khoá '${k}' (${f}) chưa có trong vi.json`);
  for (const k of vi.keys()) if (!en.has(k)) fail(`${app}: khoá '${k}' có ở vi.json nhưng thiếu ở en.json`);
  for (const k of en.keys()) if (!vi.has(k)) fail(`${app}: khoá '${k}' có ở en.json nhưng thiếu ở vi.json`);
  for (const [k, v] of en) if (typeof v === 'string' && v.trim() === '') fail(`${app}: khoá '${k}' ở en.json để trống`);
  if (!failed) console.log(`✓ ${app}: ${vi.size} khoá, ${used.size} khoá dùng trong code`);
}

process.exit(failed ? 1 : 0);
