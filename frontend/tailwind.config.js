/** @type {import('tailwindcss').Config} */
module.exports = {
  darkMode: 'class',
  content: ['./src/**/*.{html,ts}'],
  theme: {
    extend: {
      colors: {
        ink: {
          DEFAULT: 'rgb(var(--c-ink) / <alpha-value>)',
          hover: 'rgb(var(--c-ink-hover) / <alpha-value>)',
        },
        canvas: 'rgb(var(--c-canvas) / <alpha-value>)',
        surface: 'rgb(var(--c-surface) / <alpha-value>)',
        border: {
          DEFAULT: 'rgb(var(--c-border) / <alpha-value>)',
        },
        primary: {
          DEFAULT: 'rgb(var(--c-primary) / <alpha-value>)',
          hover: 'rgb(var(--c-primary-hover) / <alpha-value>)',
          light: 'rgb(var(--c-primary-hover) / <alpha-value>)',
        },
        sapphire: '#1E3A8A',
        sky: {
          glow: '#0EA5E9',
        },
        accent: {
          DEFAULT: 'rgb(var(--c-accent) / <alpha-value>)',
          light: 'rgb(var(--c-accent-light) / <alpha-value>)',
        },
        gold: '#FBBF24',
        text: {
          primary: 'rgb(var(--c-ink) / <alpha-value>)',
          secondary: 'rgb(var(--c-text-secondary) / <alpha-value>)',
          muted: 'rgb(var(--c-text-muted) / <alpha-value>)',
        },
        success: {
          DEFAULT: '#18AF6C',
          bg: '#EDFCF3',
        },
        warning: {
          DEFAULT: '#F68A0C',
          bg: '#FFF9EC',
        },
        danger: {
          DEFAULT: '#DB2C21',
          bg: '#FEF3F2',
        },
        sync: {
          online: '#10B981',
          offline: '#F59E0B',
        },
        sidebar: {
          DEFAULT: 'rgb(var(--c-sidebar) / <alpha-value>)',
          ink: 'rgb(var(--c-sidebar-ink) / <alpha-value>)',
        },
        // Teal ramp used by the ocean sidebar (mirrors the reference design tokens).
        turquoise: {
          50: '#EDFAFB',
          100: '#D0F2F5',
          200: '#A3E4EB',
          300: '#6BCEDB',
          400: '#33B0C4',
          500: '#118FA8',
          600: '#0B7389',
          700: '#0A5C6E',
          800: '#0B4A58',
          900: '#0A3C48',
          950: '#052730',
        },
      },
      fontFamily: {
        sans: ['Cairo', 'Tajawal', 'sans-serif'],
        display: ['Cairo', 'sans-serif'],
        en: ['Inter', 'sans-serif'],
        heading: ['Fraunces', 'Georgia', 'serif'],
      },
      borderRadius: {
        '2xl': '1rem',
        '3xl': '1.5rem',
      },
      boxShadow: {
        // Warm-tinted elevation — depth without grey mud
        warm: '0 2px 8px rgba(74, 63, 45, 0.07)',
        'warm-lg': '0 12px 28px rgba(74, 63, 45, 0.12)',
        'inner-line': 'inset 0 1px 0 rgba(255, 255, 255, 0.6)',
      },
      keyframes: {
        float: {
          '0%, 100%': { transform: 'translateY(0px)' },
          '50%': { transform: 'translateY(-20px)' },
        },
        'rotate-slow': {
          from: { transform: 'rotate(0deg)' },
          to: { transform: 'rotate(360deg)' },
        },
      },
      animation: {
        float: 'float 6s ease-in-out infinite',
        'float-slow': 'float 8s ease-in-out infinite',
        'rotate-slow': 'rotate-slow 20s linear infinite',
      },
    },
  },
  plugins: [],
};
