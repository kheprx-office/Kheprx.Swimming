// Business-day helper (Egypt workweek: Sun–Thu billable; Fri + Sat weekend).
// Inclusive count of billable days in [start, end] (YYYY-MM-DD). Empty/invalid/reversed → 0.
export function businessDaysBetween(start: string, end: string): number {
  if (!start || !end) return 0;
  const s = new Date(start);
  const e = new Date(end);
  if (isNaN(s.getTime()) || isNaN(e.getTime()) || e < s) return 0;
  let count = 0;
  const cur = new Date(s);
  while (cur <= e) {
    const d = cur.getDay(); // 0=Sun … 4=Thu billable; 5=Fri, 6=Sat excluded
    if (d >= 0 && d <= 4) count++;
    cur.setDate(cur.getDate() + 1);
  }
  return count;
}
