// arabic.ts — guard helpers for keeping form fields free of Arabic-script input.
// Covers Arabic, Arabic Supplement, Extended-A, and Presentation Forms A/B
// (including Arabic-Indic digits ٠–٩).
const ARABIC_PATTERN = /[؀-ۿݐ-ݿࢠ-ࣿﭐ-﷿ﹰ-﻿]/;

export function hasArabic(value: string): boolean {
  return ARABIC_PATTERN.test(value);
}

export function stripArabic(value: string): string {
  return value.replace(/[؀-ۿݐ-ݿࢠ-ࣿﭐ-﷿ﹰ-﻿]/g, '');
}
