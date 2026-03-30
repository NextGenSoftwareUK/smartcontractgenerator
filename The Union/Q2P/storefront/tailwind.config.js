const plugin = require('tailwindcss/plugin');

/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./app/**/*.{js,ts,jsx,tsx}', './components/**/*.{js,ts,jsx,tsx}'],
  theme: {
    extend: {
      colors: {
        'union-black':  '#0d0d0d',
        'union-panel':  '#141414',
        'union-gold':   '#c49937',
        'union-gold-bright': '#e2b84c',
        'union-red':    '#9b1c1c',
        'union-text':   '#f2ede4',
        'union-muted':  '#7a6e65',
        'union-border': 'rgba(196,153,55,0.22)',
      },
      fontFamily: {
        display: ['var(--font-bebas)', 'Impact', 'sans-serif'],
        sans:    ['var(--font-barlow)', 'system-ui', 'sans-serif'],
      },
      keyframes: {
        fadeIn: {
          from: { opacity: 0 },
          to:   { opacity: 1 }
        },
        marquee: {
          '0%':   { transform: 'translateX(0%)' },
          '100%': { transform: 'translateX(-100%)' }
        },
        blink: {
          '0%':   { opacity: 0.2 },
          '20%':  { opacity: 1 },
          '100%': { opacity: 0.2 }
        },
        shimmer: {
          '0%':   { backgroundPosition: '-200% 0' },
          '100%': { backgroundPosition: '200% 0' }
        }
      },
      animation: {
        fadeIn:   'fadeIn .3s ease-in-out',
        carousel: 'marquee 60s linear infinite',
        blink:    'blink 1.4s both infinite',
        shimmer:  'shimmer 2s linear infinite'
      }
    }
  },
  future: {
    hoverOnlyWhenSupported: true
  },
  plugins: [
    require('@tailwindcss/container-queries'),
    require('@tailwindcss/typography'),
    plugin(({ matchUtilities, theme }) => {
      matchUtilities(
        {
          'animation-delay': (value) => ({ 'animation-delay': value })
        },
        { values: theme('transitionDelay') }
      );
    })
  ]
};
