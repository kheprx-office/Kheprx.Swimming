// theme: design tokens for use from TypeScript. Mirrors the CSS variables in
// theme.scss so component logic and stylesheets share one source of truth.
export const theme = {
  colors: {
    primary: '#0a84ff',
    text: '#111',
    muted: '#555',
    error: '#c00',
    border: '#ccc',
    background: '#fff',
    onPrimary: '#fff',
  },
  spacing: { xs: 4, sm: 8, md: 16, lg: 24, xl: 32 },
  radius: { sm: 6, md: 10 },
} as const;
