module.exports = {
    content: [
        "./src/**/*.{js,jsx,ts,tsx}",
        "./public/index.html"
    ],
    theme: {
        extend: {
            // Every button/link/badge in this app is built with Tailwind's built-in `blue-*` classes
            // (bg-blue-600, hover:bg-blue-700, text-blue-600, etc.) - overriding the scale here rebrands
            // the entire app to Khoi's design-system accent. 600/700 are pinned to the exact values from
            // the "Khoi Pro app UI redesign" spec (accent oklch(0.52 0.19 280) / hover oklch(0.45 0.19 280)
            // -> #5952D2 / #483BBA); the rest of the scale sweeps the same oklch hue (280) at fixed chroma
            // 0.19, tapering chroma toward the light/dark ends the way the spec's own tint colors do, so
            // every stop stays in-gamut and reads as one consistent family (50 lightest, 900 darkest).
            colors: {
                blue: {
                    50: '#F3F4FF',
                    100: '#E2E5FF',
                    200: '#C5CAFF',
                    300: '#A2A8FF',
                    400: '#8080FC',
                    500: '#6C68EA',
                    600: '#5952D2',
                    700: '#483BBA',
                    800: '#382A98',
                    900: '#261A71',
                },
                primary: {
                    50: '#F3F4FF',
                    100: '#E2E5FF',
                    200: '#C5CAFF',
                    300: '#A2A8FF',
                    400: '#8080FC',
                    500: '#6C68EA',
                    600: '#5952D2',
                    700: '#483BBA',
                    800: '#382A98',
                    900: '#261A71',
                }
            },
            fontFamily: {
                sans: ['"Instrument Sans"', 'Helvetica', 'Arial', 'system-ui', 'sans-serif'],
                mono: ['"JetBrains Mono"', 'ui-monospace', 'SFMono-Regular', 'Menlo', 'monospace'],
            },
            animation: {
                'fade-in': 'fadeIn 0.5s ease-in-out',
                'slide-up': 'slideUp 0.3s ease-out',
                'bounce-light': 'bounceLight 2s infinite',
            },
            keyframes: {
                fadeIn: {
                    '0%': { opacity: '0' },
                    '100%': { opacity: '1' },
                },
                slideUp: {
                    '0%': { transform: 'translateY(10px)', opacity: '0' },
                    '100%': { transform: 'translateY(0)', opacity: '1' },
                },
                bounceLight: {
                    '0%, 20%, 53%, 80%, 100%': { transform: 'translate3d(0,0,0)' },
                    '40%, 43%': { transform: 'translate3d(0, -5px, 0)' },
                    '70%': { transform: 'translate3d(0, -3px, 0)' },
                    '90%': { transform: 'translate3d(0, -1px, 0)' },
                }
            }
        },
    },
    plugins: [require('@tailwindcss/typography')],
}