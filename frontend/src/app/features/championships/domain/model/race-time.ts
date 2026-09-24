// Swim finish-time conversions between the UI text form and integer milliseconds.
// Accepts 'ss.SS', 'm:ss.SS'/'mm:ss.SS', or 'h:mm:ss.SS' for races over an hour
// (fractional seconds up to 2 digits = centiseconds).

const SECONDS = /^([0-5]?\d)(?:\.(\d{1,2}))?$/; // ss(.SS), seconds 0–59
const WHOLE = /^\d{1,2}$/;                       // leading h / m group, 0–99

export function parseTimeToMs(text: string): number | null {
  const t = (text ?? '').trim();
  if (!t) return null;

  const parts = t.split(':');
  if (parts.length > 3) return null;

  const secMatch = SECONDS.exec(parts[parts.length - 1]);
  if (!secMatch) return null;
  const seconds = parseInt(secMatch[1], 10);
  const centis = secMatch[2] ? parseInt(secMatch[2].padEnd(2, '0'), 10) : 0;

  let minutes = 0;
  let hours = 0;
  if (parts.length >= 2) {
    const mStr = parts[parts.length - 2];
    if (!WHOLE.test(mStr)) return null;
    minutes = parseInt(mStr, 10);
    if (parts.length === 3 && minutes > 59) return null; // with hours, minutes are 0–59
  }
  if (parts.length === 3) {
    if (!WHOLE.test(parts[0])) return null;
    hours = parseInt(parts[0], 10);
  }

  const ms = ((hours * 60 + minutes) * 60 + seconds) * 1000 + centis * 10;
  return ms > 0 ? ms : null;
}

export function formatMsToTime(ms: number): string {
  if (!Number.isFinite(ms) || ms < 0) return '';
  const totalCentis = Math.round(ms / 10);
  const centis = totalCentis % 100;
  const totalSeconds = Math.floor(totalCentis / 100);
  const seconds = totalSeconds % 60;
  const totalMinutes = Math.floor(totalSeconds / 60);
  const minutes = totalMinutes % 60;
  const hours = Math.floor(totalMinutes / 60);

  const ss = seconds.toString().padStart(2, '0');
  const cc = centis.toString().padStart(2, '0');
  if (hours > 0) {
    return `${hours}:${minutes.toString().padStart(2, '0')}:${ss}.${cc}`;
  }
  return `${minutes}:${ss}.${cc}`;
}
