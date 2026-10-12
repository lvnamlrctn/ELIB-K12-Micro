import { CrudConfig } from '../../shared/crud-page';

/** Máy chủ Z39.50 / SRU của thư viện khác (monolith: /admin/z3950-configs, quyền Z3950_CONFIGS). */
export const Z3950_SERVERS: CrudConfig = {
  title: 'Z3950_CAT.TITLE',
  resource: 'z3950-servers',
  service: 'search',
  perm: 'Z3950_CONFIGS',
  searchLabel: 'Z3950_CAT.SEARCH_LABEL',
  searchPlaceholder: 'Z3950_CAT.SEARCH_PH',
  hasStatus: true,
  columns: [
    { key: 'name', label: 'Z3950_CAT.LIBRARY', width: '220px' },
    { key: 'groupName', label: 'Z3950_CAT.GROUP', width: '160px' },
    { key: 'host', label: 'Z3950_CAT.HOST', type: 'mono' },
    { key: 'port', label: 'Z3950_CAT.PORT', type: 'mono', width: '80px' },
    { key: 'databaseName', label: 'CSDL', type: 'mono', width: '130px' },
    { key: 'recordSyntax', label: 'Z3950_CAT.SYNTAX', width: '100px' },
    { key: 'showOnOpac', label: 'OPAC', type: 'bool', width: '80px' },
  ],
  fields: [
    { key: 'name', label: 'Z3950_CAT.NAME', type: 'text', required: true, placeholder: 'Z3950_CAT.NAME_EG' },
    { key: 'groupName', label: 'Z3950_CAT.GROUP', type: 'text', half: true, placeholder: 'Z3950_CAT.GROUP_EG' },
    { key: 'recordSyntax', label: 'Z3950_CAT.SYNTAX_FULL', type: 'select', half: true,
      options: [{ value: 'USMARC', label: 'USMARC / MARC21' }, { value: 'UNIMARC', label: 'UNIMARC' }] },
    { key: 'host', label: 'Z3950_CAT.HOST_FULL', type: 'text', required: true, half: true, placeholder: 'Z3950_CAT.HOST_EG', hint: 'Z3950_CAT.HOST_HINT' },
    { key: 'port', label: 'Z3950_CAT.PORT', type: 'number', required: true, half: true, placeholder: '210' },
    { key: 'databaseName', label: 'Z3950_CAT.DATABASE', type: 'text', required: true, placeholder: 'Z3950_CAT.DATABASE_EG' },
    { key: 'userName', label: 'Z3950_CAT.USER', type: 'text', half: true, hint: 'Z3950_CAT.USER_HINT' },
    { key: 'password', label: 'Z3950_CAT.PASSWORD', type: 'password', half: true },
    { key: 'sruUrl', label: 'Z3950_CAT.SRU', type: 'text', placeholder: 'https://...', hint: 'Z3950_CAT.SRU_HINT' },
    { key: 'showOnOpac', label: 'Z3950_CAT.SHOW_ON_OPAC', type: 'checkbox' },
    { key: 'status', label: 'COMMON.STATUS', type: 'status' },
  ],
  defaults: { port: 210, recordSyntax: 'USMARC', showOnOpac: false },
  modalWidth: 'max-w-xl',
  rowActions: [
    {
      icon: 'cable',
      title: 'Z3950_CAT.TEST',
      action: 'view',
      run: async (api, row) => {
        const r = await api.testZ3950Server(row.publicId);
        if (!r.ok) throw new Error(r.error ?? 'Z3950_CAT.NOT_CONNECTED');
        return { key: 'Z3950_CAT.CONNECTED', params: { ms: r.elapsedMs } };
      },
    },
  ],
};
