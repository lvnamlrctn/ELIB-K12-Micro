import { CrudConfig } from '../../shared/crud-page';

/** Mẫu email của đơn vị — mã do service nghiệp vụ dùng khi gửi tin nên không đổi được; biến dạng {{ ten_bien }}. */
export const EMAIL_TEMPLATES: CrudConfig = {
  title: 'Mẫu email',
  resource: 'email-templates',
  service: 'notification',
  perm: 'NOTIFICATION_TEMPLATES',
  searchLabel: 'Mã / tên / tiêu đề',
  searchPlaceholder: 'Nhập mã, tên hoặc tiêu đề mẫu...',
  hasStatus: true,
  modalWidth: 'max-w-3xl',
  columns: [
    { key: 'code', label: 'Mã mẫu', type: 'mono', width: '200px' },
    { key: 'name', label: 'Tên mẫu' },
    { key: 'subject', label: 'Tiêu đề thư' },
  ],
  fields: [
    {
      key: 'code', label: 'Mã mẫu', type: 'text', required: true, lockOnEdit: true, half: true, placeholder: 'VD: LOAN_DUE_SOON',
      hint: 'Chữ in hoa, số, dấu gạch dưới. Không đổi được sau khi tạo.',
    },
    { key: 'name', label: 'Tên mẫu', type: 'text', required: true, half: true },
    { key: 'subject', label: 'Tiêu đề thư', type: 'text', required: true, hint: 'Có thể dùng biến, ví dụ {{ tenant_name }}.' },
    {
      key: 'body', label: 'Nội dung (HTML)', type: 'textarea', required: true,
      hint: 'Biến dạng {{ ten_bien }}: {{ tenant_name }} luôn có; LOGIN_OTP có thêm {{ full_name }}, {{ otp }}, {{ minutes }}. Giá trị biến được mã hoá HTML.',
    },
    { key: 'status', label: 'Trạng thái', type: 'status', hint: 'Tắt mẫu = không gửi thư cho mã này.' },
  ],
  badge: (item) => (item['isBuiltIn'] ? 'Mặc định' : null),
  toolbar: [
    {
      label: 'Khôi phục mẫu mặc định',
      icon: 'settings_backup_restore',
      action: 'add',
      run: async (api) => {
        const { added } = await api.restoreEmailTemplates();
        return added ? `Đã thêm ${added} mẫu mặc định` : 'Đã có đủ mẫu mặc định';
      },
    },
  ],
};
