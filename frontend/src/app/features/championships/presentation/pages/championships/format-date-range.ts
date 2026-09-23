function parseLocal(iso: string): Date {
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(y, m - 1, d);
}

// Formats a start–end range: single day → one date; same month → "15–16 Nov 2023";
// otherwise → "20 Oct – 5 Nov 2023". Locale-aware (en-GB / ar).
export function formatDateRange(startIso: string, endIso: string, lang: 'en' | 'ar'): string {
  const locale = lang === 'ar' ? 'ar' : 'en-GB';
  const start = parseLocal(startIso);
  const end = parseLocal(endIso || startIso);
  const full: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'short', year: 'numeric' };

  if (!endIso || startIso === endIso) return start.toLocaleDateString(locale, full);

  const sameMonth = start.getMonth() === end.getMonth() && start.getFullYear() === end.getFullYear();
  if (sameMonth) {
    return `${start.toLocaleDateString(locale, { day: 'numeric' })}–${end.toLocaleDateString(locale, full)}`;
  }
  return `${start.toLocaleDateString(locale, { day: 'numeric', month: 'short' })} – ${end.toLocaleDateString(locale, full)}`;
}
