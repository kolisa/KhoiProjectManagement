module.exports = {
    content: [
        "./src/**/*.{js,jsx,ts,tsx}",
        "./public/index.html"
    ],
    theme: {
        extend: {
            // Every button/link/badge in this app is built with Tailwind's built-in `blue-*` classes
            // (bg-blue-600, hover:bg-blue-700, text-blue-600, etc.) - overriding the scale here rebrands
            // the entire app to Khoi's actual color with zero changes to any component. 600 is pinned to
            // the exact value sampled from the khoi.africa logo (#0000D3); the rest of the scale is
            // interpolated tints/shades around it so contrast still behaves the way Tailwind's default
            // blue scale did (50 lightest, 900 darkest).
            colors: {
                blue: {
                    50: '#E6E6FB',
                    100: '#C2C2F5',
                    200: '#9999EE',
                    300: '#6666E6',
                    400: '#3333DC',
                    500: '#1414D8',
                    600: '#0000D3',
                    700: '#0000A6',
                    800: '#000080',
                    900: '#00004D',
                },
                primary: {
                    50: '#E6E6FB',
                    100: '#C2C2F5',
                    200: '#9999EE',
                    300: '#6666E6',
                    400: '#3333DC',
                    500: '#1414D8',
                    600: '#0000D3',
                    700: '#0000A6',
                    800: '#000080',
                    900: '#00004D',
                }
            },
            fontFamily: {
                sans: ['Inter', 'system-ui', 'sans-serif']
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