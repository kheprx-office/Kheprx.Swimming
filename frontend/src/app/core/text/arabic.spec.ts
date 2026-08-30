import { hasArabic, stripArabic } from '@core/text/arabic';

describe('arabic helpers', () => {
  describe('hasArabic', () => {
    it('detects Arabic letters (anywhere in the string)', () => {
      expect(hasArabic('عربي')).toBe(true);
      expect(hasArabic('abcعdef')).toBe(true);
    });
    it('detects Arabic-Indic digits', () => {
      expect(hasArabic('٠١٢')).toBe(true);
    });
    it('is false for Latin, ASCII digits, and symbols', () => {
      expect(hasArabic('user@example.com')).toBe(false);
      expect(hasArabic('29801014501234')).toBe(false);
      expect(hasArabic('P@ssw0rd-1_2')).toBe(false);
      expect(hasArabic('')).toBe(false);
    });
  });

  describe('stripArabic', () => {
    it('removes Arabic characters, keeping everything else', () => {
      expect(stripArabic('abعc')).toBe('abc');
      expect(stripArabic('عربيuser@x.com')).toBe('user@x.com');
      expect(stripArabic('01٠1234')).toBe('011234');
    });
    it('returns a clean string unchanged', () => {
      expect(stripArabic('user@example.com')).toBe('user@example.com');
    });
  });
});
