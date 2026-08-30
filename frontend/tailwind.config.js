/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./src/**/*.{html,ts}'],
  theme: {
    extend: {
      colors: {
        ink: {
          DEFAULT: '#0F172A',
          hover: '#1E293B',
        },
        canvas: '#F8FAFC',
        surface: '#FFFFFF',
        border: {
          DEFAULT: '#E2E8F0',
        },
        primary: {
          DEFAULT: '#1E3A8A',
          hover: '#0EA5E9',
          light: '#0EA5E9',
        },
        sapphire: '#1E3A8A',
        sky: {
          glow: '#0EA5E9',
        },
        accent: {
          DEFAULT: '#F59E0B',
          light: '#FBBF24',
        },
        gold: '#FBBF24',
        text: {
          primary: '#0F172A',
          secondary: '#475569',
          muted: '#94A3B8',
        },
        success: {
          DEFAULT: '#10B981',
          bg: '#ECFDF5',
        },
        warning: {
          DEFAULT: '#F59E0B',
          bg: '#FEF3C7',
        },
        danger: {
          DEFAULT: '#E11D48',
          bg: '#FFF1F2',
        },
        sync: {
          online: '#10B981',
          offline: '#F59E0B',
        },
      },
      fontFamily: {
        sans: ['Cairo', 'Tajawal', 'sans-serif'],
        display: ['Cairo', 'sans-serif'],
        en: ['Inter', 'sans-serif'],
      },
      borderRadius: {
        '2xl': '1rem',
        '3xl': '1.5rem',
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
