import en from './en.json';
import ar from './ar.json';
import { Lang } from './language.store';

export type Dict = Record<string, unknown>;
export const DICTIONARIES: Record<Lang, Dict> = { en: en as Dict, ar: ar as Dict };
