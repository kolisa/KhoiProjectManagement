module.exports = {
    content: [
        "./src/**/*.{js,jsx,ts,tsx}",
        "./public/index.html"
    ],
    theme: {
        extend: {
            // Every button/link/badge in this app is built with Tailwind's built-in `blue-*` classes
            // (bg-blue-600, hover:bg-blue-700, text-blue-600, etc.) - overriding the scale here rebrands
            // the entire app to KhoiHub's design-system accent. 100/400/600/700 are pinned to the exact
            // values from the "KhoiHub Clean Corporate Purple" palette (Lavender #EEE9FF / Primary Light
            // #7B68C4 / Primary #5D4AA4 / Primary Dark #4B3A8C); the remaining stops (50/200/300/500/
            // 800/900) are linearly interpolated/extrapolated between those four anchors in RGB space so
            // every stop stays a consistent family (50 lightest, 900 darkest).
            colors: {
                blue: {
                    50: '#F7F5FF',
                    100: '#EEE9FF',
                    200: '#C8BEEB',
                    300: '#A193D8',
                    400: '#7B68C4',
                    500: '#6C59B4',
                    600: '#5D4AA4',
                    700: '#4B3A8C',
                    800: '#392A74',
                    900: '#271A5C',
                },
                primary: {
                    50: '#F7F5FF',
                    100: '#EEE9FF',
                    200: '#C8BEEB',
                    300: '#A193D8',
                    400: '#7B68C4',
                    500: '#6C59B4',
                    600: '#5D4AA4',
                    700: '#4B3A8C',
                    800: '#392A74',
                    900: '#271A5C',
                },
                // Named tokens from the same palette, available for future use - not yet retrofitted
                // onto the existing ad hoc bg-green-50/bg-red-50/bg-amber-50 status badges across the app.
                accent: {
                    teal: '#1FA7A0',
                },
                success: '#22A06B',
                warning: '#F4B740',
                danger: '#E45D5D',
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