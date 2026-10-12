import { CrudConfig } from '../../shared/crud-page';

/** Mẫu email của đơn vị — mã do service nghiệp vụ dùng khi gửi tin nên không đổi được; biến dạng {{ ten_bien }}. */
export const EMAIL_TEMPLATES: CrudConfig = {
  title: 'EMAIL_TEMPLATE.TITLE',
  resource: 'email-templates',
  service: 'notification',
  perm: 'NOTIFICATION_TEMPLATES',
  searchLabel: 'EMAIL_TEMPLATE.SEARCH_LABEL',
  searchPlaceholder: 'EMAIL_TEMPLATE.SEARCH_PH',
  hasStatus: true,
  modalWidth: 'max-w-3xl',
  columns: [
    { key: 'code', label: 'EMAIL_TEMPLATE.CODE', type: 'mono', width: '200px' },
    { key: 'name', label: 'EMAIL_TEMPLATE.NAME' },
    { key: 'subject', label: 'EMAIL_TEMPLATE.SUBJECT' },
  ],
  fields: [
    {
      key: 'code', label: 'EMAIL_TEMPLATE.CODE', type: 'text', required: true, lockOnEdit: true, half: true, placeholder: 'EMAIL_TEMPLATE.CODE_EG',
      hint: 'EMAIL_TEMPLATE.CODE_HINT',
    },
    { key: 'name', label: 'EMAIL_TEMPLATE.NAME', type: 'text', required: true, half: true },
    { key: 'subject', label: 'EMAIL_TEMPLATE.SUBJECT', type: 'text', required: true, hint: 'EMAIL_TEMPLATE.SUBJECT_HINT' },
    {
      key: 'body', label: 'EMAIL_TEMPLATE.BODY', type: 'textarea', required: true,
      hint: 'EMAIL_TEMPLATE.BODY_HINT',
    },
    { key: 'status', label: 'COMMON.STATUS', type: 'status', hint: 'EMAIL_TEMPLATE.STATUS_HINT' },
  ],
  badge: (item) => (item['isBuiltIn'] ? 'EMAIL_TEMPLATE.BUILT_IN' : null),
  toolbar: [
    {
      label: 'EMAIL_TEMPLATE.RESTORE',
      icon: 'settings_backup_restore',
      action: 'add',
      run: async (api) => {
        const { added } = await api.restoreEmailTemplates();
        return added ? { key: 'EMAIL_TEMPLATE.RESTORED', params: { count: added } } : 'EMAIL_TEMPLATE.RESTORE_NOTHING';
      },
    },
  ],
};
