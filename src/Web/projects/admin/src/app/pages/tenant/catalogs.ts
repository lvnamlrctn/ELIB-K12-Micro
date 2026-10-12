import { CrudConfig } from '../../shared/crud-page';

/**
 * Danh mục chỉ có tên — như các trang dùng app-base-entity của admin cũ (nationality.ts, chuc-vu.ts…).
 * ns: vùng khoá dịch kiểu ELIB-LRC có TITLE, NAME, SEARCH_PLACEHOLDER.
 */
export function named(ns: string, resource: string, perm: string, service?: string): CrudConfig {
  const nameLabel = `${ns}.NAME`, placeholder = `${ns}.SEARCH_PLACEHOLDER`;
  return {
    title: `${ns}.TITLE`,
    resource,
    service,
    perm,
    searchLabel: nameLabel,
    searchPlaceholder: placeholder,
    columns: [{ key: 'name', label: nameLabel }],
    fields: [{ key: 'name', label: nameLabel, type: 'text', required: true, placeholder }],
    importable: true,
  };
}

export const NATIONALITIES = named('NATIONALITY', 'nationalities', 'NATIONALITIES');
export const ACADEMIC_TITLES = named('PROF', 'academic-titles', 'PROFS');
export const ETHNICITIES = named('ETHNIC', 'ethnicities', 'ETHNICS');
export const DEGREES = named('DEGREE', 'degrees', 'DEGREES');
export const POSITIONS = named('CHUC_VU', 'positions', 'POSITIONS');

export const CURRENCIES: CrudConfig = {
  title: 'CURRENCY.TITLE',
  resource: 'currencies',
  perm: 'CURRENCIES',
  searchLabel: 'CURRENCY.SEARCH_LABEL',
  searchPlaceholder: 'CURRENCY.SEARCH_PH2',
  hasStatus: true,
  columns: [
    { key: 'code', label: 'CURRENCY.CODE', type: 'mono', width: '140px' },
    { key: 'name', label: 'CURRENCY.NAME' },
    { key: 'exchangeRate', label: 'CURRENCY.RATE_VND', type: 'number', width: '160px' },
  ],
  fields: [
    { key: 'code', label: 'CURRENCY.CODE', type: 'text', required: true, placeholder: 'CURRENCY.CODE_EG', half: true },
    { key: 'exchangeRate', label: 'CURRENCY.RATE_VND_FULL', type: 'number', required: true, placeholder: 'CURRENCY.RATE_EG', half: true },
    { key: 'name', label: 'CURRENCY.NAME', type: 'text', required: true, placeholder: 'CURRENCY.NAME_EG' },
    { key: 'status', label: 'COMMON.STATUS', type: 'status' },
  ],
  defaults: { exchangeRate: 1 },
};

export const SYSTEM_PARAMETERS: CrudConfig = {
  title: 'SYSTEM_PARAMETER.TITLE',
  resource: 'system-parameters',
  perm: 'SYSTEM_PARAMS',
  searchLabel: 'SYSTEM_PARAMETER.SEARCH_LABEL',
  searchPlaceholder: 'SYSTEM_PARAMETER.SEARCH_PH2',
  modalWidth: 'max-w-2xl',
  columns: [
    { key: 'code', label: 'SYSTEM_PARAMETER.CODE', type: 'mono', width: '260px' },
    { key: 'description', label: 'SYSTEM_PARAMETER.DESCRIPTION' },
    { key: 'value', label: 'SYSTEM_PARAMETER.VALUE', width: '240px' },
    { key: 'isPublic', label: 'SYSTEM_PARAMETER.PUBLIC_OPAC', type: 'bool', width: '130px' },
  ],
  fields: [
    { key: 'code', label: 'SYSTEM_PARAMETER.CODE', type: 'text', required: true, lockOnEdit: true, placeholder: 'SYSTEM_PARAMETER.CODE_EG', hint: 'SYSTEM_PARAMETER.CODE_HINT' },
    { key: 'description', label: 'SYSTEM_PARAMETER.DESCRIPTION_VI', type: 'text' },
    { key: 'descriptionEn', label: 'SYSTEM_PARAMETER.DESCRIPTION_EN2', type: 'text' },
    { key: 'value', label: 'SYSTEM_PARAMETER.VALUE', type: 'textarea' },
    { key: 'type', label: 'SYSTEM_PARAMETER.VALUE_TYPE', type: 'text', placeholder: 'text, bool, number, json', half: true },
    { key: 'service', label: 'SYSTEM_PARAMETER.SERVICE', type: 'text', placeholder: 'SYSTEM_PARAMETER.SERVICE_EG', half: true, lockWhen: (p) => !!p['isBuiltIn'] },
    {
      key: 'isPublic', label: 'SYSTEM_PARAMETER.PUBLIC_LONG', type: 'checkbox', lockWhen: (p) => !!p['isBuiltIn'],
      hint: 'SYSTEM_PARAMETER.PUBLIC_HINT',
    },
  ],
  defaults: { type: 'text', isPublic: false },
  badge: (p) => (p['isBuiltIn'] ? 'SYSTEM_PARAMETER.BUILT_IN' : null),
};
