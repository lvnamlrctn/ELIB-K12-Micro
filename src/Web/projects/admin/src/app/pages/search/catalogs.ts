import { CrudConfig } from '../../shared/crud-page';

/** Máy chủ Z39.50 / SRU của thư viện khác (monolith: /admin/z3950-configs, quyền Z3950_CONFIGS). */
export const Z3950_SERVERS: CrudConfig = {
  title: 'Máy chủ Z39.50',
  resource: 'z3950-servers',
  service: 'search',
  perm: 'Z3950_CONFIGS',
  searchLabel: 'Tên / địa chỉ',
  searchPlaceholder: 'VD: Thư viện Quốc hội Mỹ, lx2.loc.gov',
  hasStatus: true,
  columns: [
    { key: 'name', label: 'Thư viện' },
    { key: 'groupName', label: 'Nhóm', width: '160px' },
    { key: 'host', label: 'Máy chủ', type: 'mono' },
    { key: 'port', label: 'Cổng', type: 'mono', width: '80px' },
    { key: 'databaseName', label: 'CSDL', type: 'mono', width: '130px' },
    { key: 'recordSyntax', label: 'Khổ mẫu', width: '100px' },
    { key: 'showOnOpac', label: 'OPAC', type: 'bool', width: '80px' },
  ],
  fields: [
    { key: 'name', label: 'Tên thư viện', type: 'text', required: true, placeholder: 'VD: Thư viện Quốc hội Mỹ (LOC)' },
    { key: 'groupName', label: 'Nhóm', type: 'text', half: true, placeholder: 'VD: Thư viện nước ngoài' },
    { key: 'recordSyntax', label: 'Khổ mẫu bản ghi', type: 'select', half: true,
      options: [{ value: 'USMARC', label: 'USMARC / MARC21' }, { value: 'UNIMARC', label: 'UNIMARC' }] },
    { key: 'host', label: 'Địa chỉ máy chủ', type: 'text', required: true, half: true, placeholder: 'VD: lx2.loc.gov', hint: 'Tên miền hoặc IP, không kèm http://' },
    { key: 'port', label: 'Cổng', type: 'number', required: true, half: true, placeholder: '210' },
    { key: 'databaseName', label: 'Tên cơ sở dữ liệu', type: 'text', required: true, placeholder: 'VD: LCDB' },
    { key: 'userName', label: 'Tên đăng nhập', type: 'text', half: true, hint: 'Nếu máy chủ yêu cầu' },
    { key: 'password', label: 'Mật khẩu', type: 'password', half: true },
    { key: 'sruUrl', label: 'Địa chỉ SRU (tuỳ chọn)', type: 'text', placeholder: 'https://...', hint: 'Có địa chỉ SRU thì tra qua HTTP thay cho Z39.50.' },
    { key: 'showOnOpac', label: 'Cho bạn đọc tra trên OPAC (Tra cứu liên thư viện)', type: 'checkbox' },
    { key: 'status', label: 'Trạng thái', type: 'status' },
  ],
  defaults: { port: 210, recordSyntax: 'USMARC', showOnOpac: false },
  modalWidth: 'max-w-xl',
  rowActions: [
    {
      icon: 'cable',
      title: 'Kiểm tra kết nối',
      action: 'view',
      run: async (api, row) => {
        const r = await api.testZ3950Server(row.publicId);
        if (!r.ok) throw new Error(r.error ?? 'Không kết nối được.');
        return `Kết nối được (${r.elapsedMs} ms).`;
      },
    },
  ],
};
